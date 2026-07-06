using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CustomerSil : MonoBehaviour
{
    public static CustomerSil Instance { get; private set; }

    [Header("References")]
    [SerializeField] GameObject doorLight;
    [SerializeField] SpriteRenderer customerSprite;
    [SerializeField] Transform maskGameobject;
    [SerializeField] Transform startPos;
    [SerializeField] Transform endPos;

    [Header("Boss Head")]
    [SerializeField] SpriteRenderer bossHeadSprite;
    [SerializeField] Transform bossHeadStartPos;
    [SerializeField] Transform bossHeadEndPos;

    [Header("Timing")]
    [SerializeField] float doorMoveDuration = 0.45f;
    [SerializeField] float holdDuration = 3f;
    [SerializeField] float bossHeadMoveDuration = 0.5f;
    [SerializeField] float bossHeadHoldDuration = 1f;
    [SerializeField] float bossHeadReturnDuration = 0.5f;

    readonly Queue<SilRequest> requestQueue = new Queue<SilRequest>();
    Tween activeTween;
    Coroutine activeRoutine;
    bool isPlaying;
    SilRequest currentRequest;

    public bool IsPlaying => isPlaying;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CustomerSil in scene; destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        ResetMaskToStart();
        SetVisualsActive(false);
        SetBossHeadActive(false);
    }

    void OnDestroy()
    {
        activeTween?.Kill();
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        if (Instance == this)
            Instance = null;
    }

    void ResolveReferences()
    {
        if (maskGameobject == null)
        {
            var maskTransform = transform.Find("GameObject");
            if (maskTransform != null)
                maskGameobject = maskTransform;
        }

        if (startPos == null)
        {
            var start = transform.Find("start");
            if (start != null)
                startPos = start;
        }

        if (endPos == null)
        {
            var end = transform.Find("end");
            if (end != null)
                endPos = end;
        }

        if (transform.childCount > 1 && customerSprite == null)
            customerSprite = transform.GetChild(1).GetComponent<SpriteRenderer>();

        if (transform.childCount > 2 && doorLight == null)
            doorLight = transform.GetChild(2).gameObject;

        if (customerSprite == null)
        {
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == bossHeadSprite)
                    continue;

                if (renderer.GetComponentInParent<SpriteMask>() != null)
                    continue;

                if (renderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask
                    && renderer.transform.localScale.x < 2f)
                {
                    customerSprite = renderer;
                    break;
                }
            }
        }

        if (doorLight == null)
        {
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == customerSprite || renderer == bossHeadSprite)
                    continue;

                if (renderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask)
                {
                    doorLight = renderer.gameObject;
                    break;
                }
            }
        }

        if (bossHeadSprite == null)
        {
            var bossHead = transform.Find("bossHead");
            if (bossHead != null)
                bossHeadSprite = bossHead.GetComponent<SpriteRenderer>();
        }

        if (bossHeadStartPos == null)
        {
            var start = transform.Find("bossHeadStart");
            if (start != null)
                bossHeadStartPos = start;
        }

        if (bossHeadEndPos == null)
        {
            var end = transform.Find("bossHeadEnd");
            if (end != null)
                bossHeadEndPos = end;
        }
    }

    public void QueueEntrance(string identifier, Action<Action> onDoorOpened, Action onComplete = null)
    {
        Enqueue(new SilRequest
        {
            Mode = SilMode.HandAction,
            Identifier = identifier,
            OnDoorOpenedWithClose = onDoorOpened,
            OnComplete = onComplete
        });
    }

    public void QueueExit(string identifier, Action onDoorClosed)
    {
        Enqueue(new SilRequest
        {
            Mode = SilMode.Exit,
            Identifier = identifier,
            OnComplete = onDoorClosed
        });
    }

    public void QueueEatMinion(string identifier, Action<Action> onDoorOpened, Action onComplete = null)
    {
        QueueHandAction(identifier, onDoorOpened, onComplete);
    }

    public void QueueHandAction(string identifier, Action<Action> onDoorOpened, Action onComplete = null)
    {
        Enqueue(new SilRequest
        {
            Mode = SilMode.HandAction,
            Identifier = identifier,
            OnDoorOpenedWithClose = onDoorOpened,
            OnComplete = onComplete
        });
    }

    public void QueueBossEntrance(string identifier, Action<Action> onDoorOpened, Action onComplete = null)
    {
        Enqueue(new SilRequest
        {
            Mode = SilMode.BossEntrance,
            Identifier = identifier,
            OnDoorOpenedWithClose = onDoorOpened,
            OnComplete = onComplete
        });
    }

    void Enqueue(SilRequest request)
    {
        requestQueue.Enqueue(request);
        if (!isPlaying)
            ProcessNext();
    }

    void ProcessNext()
    {
        if (requestQueue.Count == 0)
        {
            isPlaying = false;
            currentRequest = default;
            return;
        }

        isPlaying = true;
        currentRequest = requestQueue.Dequeue();

        switch (currentRequest.Mode)
        {
            case SilMode.HandAction:
                PlayHandAction(currentRequest);
                break;
            case SilMode.BossEntrance:
                PlayBossEntrance(currentRequest);
                break;
            default:
                PlayOpenHoldClose(currentRequest);
                break;
        }
    }

    void PlayOpenHoldClose(SilRequest request)
    {
        PrepareForPlay(request.Identifier);

        activeTween?.Kill();
        activeTween = DOTween.Sequence()
            .Append(MoveMask(GetEndPosition()))
            .AppendInterval(holdDuration)
            .Append(MoveMask(GetStartPosition()))
            .OnComplete(() => FinishRequest(request));
    }

    void PlayHandAction(SilRequest request)
    {
        PrepareForPlay(request.Identifier);

        activeTween?.Kill();
        activeTween = MoveMask(GetEndPosition())
            .OnComplete(() =>
            {
                if (request.OnDoorOpenedWithClose == null)
                {
                    StartClose(() => FinishRequest(request));
                    return;
                }

                request.OnDoorOpenedWithClose.Invoke(() => StartClose(() => FinishRequest(request)));
            });
    }

    void PlayBossEntrance(SilRequest request)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(BossEntranceRoutine(request));
    }

    IEnumerator BossEntranceRoutine(SilRequest request)
    {
        PrepareForPlay(request.Identifier);
        SetBossHeadActive(false);

        yield return MoveMaskCoroutine(GetEndPosition(), doorMoveDuration);
        yield return BossHeadRoutine(request.Identifier);

        if (request.OnDoorOpenedWithClose == null)
        {
            yield return MoveMaskCoroutine(GetStartPosition(), doorMoveDuration);
            FinishRequest(request);
            activeRoutine = null;
            yield break;
        }

        request.OnDoorOpenedWithClose.Invoke(() => StartClose(() =>
        {
            FinishRequest(request);
            activeRoutine = null;
        }));
    }

    IEnumerator BossHeadRoutine(string identifier)
    {
        if (bossHeadSprite == null || bossHeadStartPos == null || bossHeadEndPos == null)
            yield break;

        var sprite = ResourceSpriteLoader.GetBossHead(identifier);
        if (sprite == null)
            yield break;

        Transform head = bossHeadSprite.transform;
        Vector3 startLocal = bossHeadStartPos.localPosition;
        Vector3 endLocal = bossHeadEndPos.localPosition;

        bossHeadSprite.sprite = sprite;
        head.localPosition = startLocal;
        SetBossHeadActive(true);

        yield return MoveLocalCoroutine(head, endLocal, bossHeadMoveDuration);

        var altSprite = ResourceSpriteLoader.GetBossHeadAlt(identifier);
        if (altSprite != null)
            bossHeadSprite.sprite = altSprite;

        if (bossHeadHoldDuration > 0f)
            yield return new WaitForSeconds(bossHeadHoldDuration);

        yield return MoveLocalCoroutine(head, startLocal, bossHeadReturnDuration);

        SetBossHeadActive(false);
    }

    IEnumerator MoveLocalCoroutine(Transform target, Vector3 localTarget, float duration)
    {
        if (target == null)
            yield break;

        if (duration <= 0f)
        {
            target.localPosition = localTarget;
            yield break;
        }

        Vector3 from = target.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.localPosition = Vector3.Lerp(from, localTarget, t);
            yield return null;
        }

        target.localPosition = localTarget;
    }

    IEnumerator MoveMaskCoroutine(Vector3 worldTarget, float duration)
    {
        if (maskGameobject == null)
            yield break;

        if (duration <= 0f)
        {
            maskGameobject.position = worldTarget;
            yield break;
        }

        Vector3 from = maskGameobject.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            maskGameobject.position = Vector3.Lerp(from, worldTarget, t);
            yield return null;
        }

        maskGameobject.position = worldTarget;
    }

    void StartClose(Action onComplete)
    {
        activeTween?.Kill();
        activeTween = MoveMask(GetStartPosition())
            .OnComplete(() => onComplete?.Invoke());
    }

    void FinishRequest(SilRequest request)
    {
        SetVisualsActive(false);
        SetBossHeadActive(false);

        if (bossHeadSprite != null && bossHeadStartPos != null)
            bossHeadSprite.transform.localPosition = bossHeadStartPos.localPosition;

        ResetMaskToStart();
        request.OnComplete?.Invoke();
        ProcessNext();
    }

    void PrepareForPlay(string identifier)
    {
        ApplySilhouette(identifier);
        SetVisualsActive(true);
        ResetMaskToStart();
    }

    void ApplySilhouette(string identifier)
    {
        if (customerSprite == null)
            return;

        customerSprite.sprite = ResourceSpriteLoader.GetCustomerSil(identifier);
        customerSprite.enabled = customerSprite.sprite != null;
    }

    void SetVisualsActive(bool active)
    {
        if (doorLight != null)
            doorLight.SetActive(active);

        if (customerSprite != null)
            customerSprite.enabled = active && customerSprite.sprite != null;
    }

    void SetBossHeadActive(bool active)
    {
        if (bossHeadSprite == null)
            return;

        bossHeadSprite.enabled = active && bossHeadSprite.sprite != null;
    }

    void ResetMaskToStart()
    {
        if (maskGameobject == null)
            return;

        maskGameobject.position = GetStartPosition();
    }

    Vector3 GetStartPosition()
    {
        return startPos != null ? startPos.position : maskGameobject.position;
    }

    Vector3 GetEndPosition()
    {
        if (endPos != null)
            return endPos.position;

        if (startPos != null)
            return startPos.position + Vector3.down * 8f;

        return maskGameobject.position + Vector3.down * 8f;
    }

    Tween MoveMask(Vector3 worldPosition)
    {
        return maskGameobject
            .DOMove(worldPosition, doorMoveDuration)
            .SetEase(Ease.InOutQuad);
    }

    enum SilMode
    {
        Entrance,
        Exit,
        HandAction,
        BossEntrance
    }

    struct SilRequest
    {
        public SilMode Mode;
        public string Identifier;
        public Action OnComplete;
        public Action<Action> OnDoorOpenedWithClose;
    }
}
