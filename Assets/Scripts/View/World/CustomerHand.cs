using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CustomerHand : MonoBehaviour
{
    public static CustomerHand Instance { get; private set; }

    const string DefaultIdentifier = "normal";
    const string AboveDoorSortingLayerName = "aboveDoor";
    const int AttachedSortingOrder = 1;

    static string pendingSpawnIdentifier;
    static Vector3? pendingGrabAnchorWorld;

    [Header("Layout")]
    [SerializeField] Transform originPos;
    [SerializeField] Transform grabPoint;
    [SerializeField] Transform minPosition;

    [Header("Hand Sprites")]
    [SerializeField] GameObject openHand;
    [SerializeField] GameObject closedHand;

    [Header("Timing")]
    [SerializeField] float resetDuration;
    [SerializeField] float extendDuration = 0.5f;
    [SerializeField] float holdDuration;
    [SerializeField] float retractDuration = 0.5f;

    readonly Queue<MoveRequest> moveQueue = new Queue<MoveRequest>();
    Tween activeTween;
    Tween holdTween;
    bool isMoving;
    bool handVisible;
    bool handOpen = true;
    Vector3 cachedOrigin;
    float cachedMinGrabWorldY = float.NegativeInfinity;
    string currentIdentifier;
    readonly Dictionary<Transform, List<SortingSnapshot>> attachedSortingStates = new Dictionary<Transform, List<SortingSnapshot>>();

    public Transform GrabPoint => grabPoint != null ? grabPoint : transform;
    public Vector3 OriginPosition => cachedOrigin;
    public bool IsMoving => isMoving;
    public float ResetDuration => resetDuration;
    public float ExtendDuration => extendDuration;
    public float HoldDuration => holdDuration;
    public float RetractDuration => retractDuration;

    public static CustomerHand EnsureInScene(Transform parent = null, string identifier = DefaultIdentifier)
    {
        var existing = FindObjectOfType<CustomerHand>();
        if (existing != null)
            return SetIdentifier(identifier, parent ?? existing.transform.parent);

        return Spawn(NormalizeIdentifier(identifier), parent, Vector3.zero);
    }

    /// <summary>
    /// 按 identifier 切换整只手的 prefab（Resources/customerHand/{identifier}）。
    /// grabAnchorWorld：grabPoint 的归位世界坐标，通常与 handPos 对齐。
    /// </summary>
    public static CustomerHand SetIdentifier(string identifier, Transform parent = null, Vector3? grabAnchorWorld = null)
    {
        identifier = NormalizeIdentifier(identifier);

        if (Instance != null && Instance.currentIdentifier == identifier)
        {
            if (grabAnchorWorld.HasValue)
                Instance.AlignGrabTo(grabAnchorWorld.Value);
            return Instance;
        }

        Transform targetParent = parent;
        Vector3? anchor = grabAnchorWorld;

        if (Instance != null)
        {
            targetParent = parent != null ? parent : Instance.transform.parent;
            if (!anchor.HasValue)
                anchor = Instance.cachedOrigin;
            var old = Instance;
            Instance = null;
            Destroy(old.gameObject);
        }

        return Spawn(identifier, targetParent, Vector3.zero, anchor);
    }

    public void AlignGrabTo(Vector3 grabWorldPosition)
    {
        cachedOrigin = grabWorldPosition;
        ResetToOriginImmediate();
        if (minPosition != null)
            cachedMinGrabWorldY = minPosition.position.y;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CustomerHand in scene; destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentIdentifier = !string.IsNullOrEmpty(pendingSpawnIdentifier)
            ? pendingSpawnIdentifier
            : NormalizeIdentifier(gameObject.name);
        ResolveLayoutReferences();
        ResolveVisualReferences();
        DisableOrphanHandSprites();

        if (pendingGrabAnchorWorld.HasValue)
            cachedOrigin = pendingGrabAnchorWorld.Value;
        else
            CacheOrigin();

        ResetToOriginImmediate();
        SetHandVisible(false);
    }

    void OnDestroy()
    {
        activeTween?.Kill();
        holdTween?.Kill();
        if (Instance == this)
            Instance = null;
    }

    public static GameObject LoadHandPrefab(string identifier)
    {
        identifier = NormalizeIdentifier(identifier);

        var prefab = LoadIdentifierPrefab($"customerHand/{identifier}");
        if (prefab != null)
            return prefab;

        int underscore = identifier.IndexOf('_');
        if (underscore > 0)
        {
            string baseId = identifier.Substring(0, underscore);
            prefab = LoadIdentifierPrefab($"customerHand/{baseId}");
            if (prefab != null)
                return prefab;
        }

        return LoadIdentifierPrefab($"customerHand/{DefaultIdentifier}");
    }

    static GameObject LoadIdentifierPrefab(string resourcePath)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        if (prefab.GetComponent<CustomerHand>() != null)
            return prefab;

        if (prefab.GetComponentInChildren<CustomerHand>(true) != null)
            return prefab;

        return null;
    }

    static CustomerHand Spawn(string identifier, Transform parent, Vector3 position, Vector3? grabAnchorWorld = null)
    {
        var prefab = LoadHandPrefab(identifier);
        if (prefab == null)
        {
            Debug.LogWarning($"CustomerHand prefab not found for identifier: {identifier}");
            return null;
        }

        pendingSpawnIdentifier = identifier;
        pendingGrabAnchorWorld = grabAnchorWorld;
        var instance = Instantiate(prefab, parent);
        if (!grabAnchorWorld.HasValue)
        {
            if (parent != null)
                instance.transform.localPosition = Vector3.zero;
            else
                instance.transform.position = position;
        }
        instance.name = identifier;
        pendingSpawnIdentifier = null;
        pendingGrabAnchorWorld = null;
        return instance.GetComponent<CustomerHand>()
            ?? instance.GetComponentInChildren<CustomerHand>(true);
    }

    static string NormalizeIdentifier(string identifier)
    {
        return string.IsNullOrEmpty(identifier) ? "normal" : identifier;
    }

    void ResolveLayoutReferences()
    {
        originPos = FindTransform(transform, originPos, "originPos");
        grabPoint = FindTransform(transform, grabPoint, "grabPoint");
        minPosition = FindTransform(transform, minPosition, "minPosition");
    }

    void ResolveVisualReferences()
    {
        openHand = FindGameObject(transform, openHand, "open", "openHand");
        closedHand = FindGameObject(transform, closedHand, "close", "closedHand", "closed");
    }

    void DisableOrphanHandSprites()
    {
        var controlled = new HashSet<GameObject>();
        if (openHand != null)
            controlled.Add(openHand);
        if (closedHand != null)
            controlled.Add(closedHand);

        foreach (var child in GetComponentsInChildren<Transform>(true))
        {
            if (child == null)
                continue;

            string name = child.name;
            if (name != "open" && name != "openHand" && name != "close" && name != "closedHand" && name != "closed")
                continue;

            if (!controlled.Contains(child.gameObject))
                child.gameObject.SetActive(false);
        }
    }

    static Transform FindTransform(Transform root, Transform current, params string[] names)
    {
        if (current != null)
            return current;

        foreach (var name in names)
        {
            var direct = root.Find(name);
            if (direct != null)
                return direct;
        }

        foreach (var name in names)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name)
                    return child;
            }
        }

        return null;
    }

    static GameObject FindGameObject(Transform root, GameObject current, params string[] names)
    {
        var transform = FindTransform(root, current != null ? current.transform : null, names);
        return transform != null ? transform.gameObject : null;
    }

    void CacheOrigin()
    {
        if (originPos != null)
            cachedOrigin = originPos.position;
        else
            cachedOrigin = GrabPoint.position;

        if (minPosition != null)
            cachedMinGrabWorldY = minPosition.position.y;
    }

    public void SetTargetTo(Vector3 targetWorldPosition, float duration, Action onComplete = null)
    {
        EnqueueMove(targetWorldPosition, duration, onComplete);
    }

    public void SetTargetTo(Transform target, float duration, Action onComplete = null)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        SetTargetTo(target.position, duration, onComplete);
    }

    public void ReturnToOrigin(float duration, Action onComplete = null)
    {
        SetTargetTo(OriginPosition, duration, onComplete);
    }

    public void ReturnToOrigin(Action onComplete = null)
    {
        ReturnToOrigin(retractDuration, onComplete);
    }

    /// <summary>
    /// 归位 → 伸出前回调 → 伸出 → 到达回调 → 等待 → 缩回。
    /// </summary>
    public void PlayHandSequence(
        Vector3 targetPosition,
        Action onBeforeExtend,
        Action onAtTarget,
        Action onComplete)
    {
        SetHandVisible(true);
        float resetTime = IsGrabAtOrigin() ? 0f : resetDuration;
        SetTargetTo(cachedOrigin, resetTime, () =>
        {
            onBeforeExtend?.Invoke();
            SetTargetTo(targetPosition, extendDuration, () =>
            {
                onAtTarget?.Invoke();
                WaitHold(() => ReturnToOrigin(retractDuration, onComplete));
            });
        });
    }

    void WaitHold(Action onComplete)
    {
        holdTween?.Kill();
        if (holdDuration <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        holdTween = DOVirtual.DelayedCall(holdDuration, () => onComplete?.Invoke());
    }

    public void ResetToOriginImmediate()
    {
        activeTween?.Kill();
        holdTween?.Kill();
        moveQueue.Clear();
        isMoving = false;
        MoveRootSoGrabReaches(cachedOrigin);
    }

    public void AttachToGrab(Transform item)
    {
        if (item == null)
            return;

        item.SetParent(GrabPoint, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        ApplyAttachedSorting(item);
    }

    public void DetachAtWorldPosition(Transform item, Vector3 worldPosition)
    {
        if (item == null)
            return;

        RestoreAttachedSorting(item);
        item.SetParent(null, true);
        item.position = worldPosition;
    }

    public void SetHandOpen(bool open)
    {
        handOpen = open;
        ApplyHandSpriteState();
    }

    public void SetHandVisible(bool visible)
    {
        handVisible = visible;
        ApplyHandSpriteState();
    }

    void ApplyHandSpriteState()
    {
        if (!handVisible)
        {
            if (openHand != null)
                openHand.SetActive(false);
            if (closedHand != null)
                closedHand.SetActive(false);
            return;
        }

        if (openHand != null && closedHand != null && openHand == closedHand)
        {
            openHand.SetActive(true);
            return;
        }

        if (openHand != null)
            openHand.SetActive(handOpen);

        if (closedHand != null)
            closedHand.SetActive(!handOpen);
    }

    bool IsGrabAtOrigin()
    {
        return Vector3.Distance(GrabPoint.position, cachedOrigin) < 0.05f;
    }

    void EnqueueMove(Vector3 targetWorldPosition, float duration, Action onComplete)
    {
        moveQueue.Enqueue(new MoveRequest
        {
            Target = ClampTarget(targetWorldPosition),
            Duration = Mathf.Max(0f, duration),
            OnComplete = onComplete
        });

        if (!isMoving)
            ProcessNextMove();
    }

    void ProcessNextMove()
    {
        if (moveQueue.Count == 0)
        {
            isMoving = false;
            return;
        }

        isMoving = true;
        var request = moveQueue.Dequeue();

        activeTween?.Kill();
        if (request.Duration <= 0f)
        {
            MoveRootSoGrabReaches(request.Target);
            request.OnComplete?.Invoke();
            ProcessNextMove();
            return;
        }

        Vector3 startGrab = GrabPoint.position;
        Vector3 endGrab = ClampTarget(request.Target);
        activeTween = DOVirtual.Vector3(startGrab, endGrab, request.Duration, grabPos =>
            {
                MoveRootSoGrabReaches(grabPos);
            })
            .SetEase(Ease.InOutQuad)
            .OnComplete(() =>
            {
                request.OnComplete?.Invoke();
                ProcessNextMove();
            });
    }

    Vector3 ClampTarget(Vector3 target)
    {
        if (minPosition != null && cachedMinGrabWorldY > float.NegativeInfinity && target.y < cachedMinGrabWorldY)
            target.y = cachedMinGrabWorldY;

        return target;
    }

    Vector3 GetRootPositionForGrabAt(Vector3 grabWorldTarget)
    {
        grabWorldTarget = ClampTarget(grabWorldTarget);
        Vector3 grabOffset = transform.position - GrabPoint.position;
        return grabWorldTarget + grabOffset;
    }

    void MoveRootSoGrabReaches(Vector3 grabWorldTarget)
    {
        transform.position = GetRootPositionForGrabAt(grabWorldTarget);
    }

    void ApplyAttachedSorting(Transform root)
    {
        RestoreAttachedSorting(root);

        ResolveHandSorting(out int layerId, out int sortingOrder);
        var snapshots = new List<SortingSnapshot>();

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            snapshots.Add(new SortingSnapshot(renderer, renderer.sortingLayerID, renderer.sortingOrder));
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = sortingOrder;
        }

        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            snapshots.Add(new SortingSnapshot(canvas, canvas.sortingLayerID, canvas.sortingOrder));
            canvas.sortingLayerID = layerId;
            canvas.sortingOrder = sortingOrder;
        }

        if (snapshots.Count > 0)
            attachedSortingStates[root] = snapshots;
    }

    void ResolveHandSorting(out int layerId, out int sortingOrder)
    {
        layerId = ResolveAboveDoorLayerId();
        sortingOrder = AttachedSortingOrder;

        SpriteRenderer handRenderer = null;
        if (openHand != null)
            handRenderer = openHand.GetComponent<SpriteRenderer>();
        if (handRenderer == null && closedHand != null)
            handRenderer = closedHand.GetComponent<SpriteRenderer>();

        if (handRenderer == null)
            return;

        layerId = handRenderer.sortingLayerID;
        sortingOrder = handRenderer.sortingOrder;
    }

    void RestoreAttachedSorting(Transform root)
    {
        if (root == null || !attachedSortingStates.TryGetValue(root, out var snapshots))
            return;

        foreach (var snapshot in snapshots)
            snapshot.Restore();

        attachedSortingStates.Remove(root);
    }

    static int ResolveAboveDoorLayerId()
    {
        int layerId = SortingLayer.NameToID(AboveDoorSortingLayerName);
        if (layerId != 0)
            return layerId;

        layerId = SortingLayer.NameToID("AboveDoor");
        return layerId != 0 ? layerId : SortingLayer.NameToID("Default");
    }

    struct SortingSnapshot
    {
        readonly Component target;
        readonly int sortingLayerId;
        readonly int sortingOrder;

        public SortingSnapshot(Component target, int sortingLayerId, int sortingOrder)
        {
            this.target = target;
            this.sortingLayerId = sortingLayerId;
            this.sortingOrder = sortingOrder;
        }

        public void Restore()
        {
            if (target == null)
                return;

            switch (target)
            {
                case Renderer renderer:
                    renderer.sortingLayerID = sortingLayerId;
                    renderer.sortingOrder = sortingOrder;
                    break;
                case Canvas canvas:
                    canvas.sortingLayerID = sortingLayerId;
                    canvas.sortingOrder = sortingOrder;
                    break;
            }
        }
    }

    struct MoveRequest
    {
        public Vector3 Target;
        public float Duration;
        public Action OnComplete;
    }
}
