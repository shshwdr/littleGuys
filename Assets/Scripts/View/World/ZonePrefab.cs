using UniRx;
using UnityEngine;
using System.Collections.Generic;

public class ZonePrefab : MonoBehaviour
{
    [SerializeField] ZoneType zoneType;
    [SerializeField] Transform inputPoint;
    [SerializeField] Transform workPoint;
    [SerializeField] Transform outputPoint;
    [SerializeField] Transform minionWorkPoint;
    [SerializeField] Transform workerRoot;
    [SerializeField] string displayLabel;
    [SerializeField] bool startsUnlocked = true;
    [SerializeField] bool showWorldUi = true;
    [SerializeField] ZoneWorldUIView worldUi;
    [SerializeField] ZoneItemView itemView;
    [SerializeField] ZoneSourceView sourceView;
    [SerializeField] ZoneBufferPileView[] bufferPiles;

    [Header("Work Animation")]
    [Tooltip("Shown while this machine is being worked on. Hidden otherwise.")]
    [SerializeField] GameObject workObject;
    [Tooltip("Shown while this machine is idle (default visible state).")]
    [SerializeField] GameObject notWorkObject;
    [Tooltip("Loops while working. Auto-resolved from workObject if left empty.")]
    [SerializeField] SpriteAnimPlayer workAnimPlayer;

    readonly List<Vector2> outputPositions = new List<Vector2>();
    readonly List<Vector2> minionWorkPositions = new List<Vector2>();

    GameModel model;
    bool workVisualInitialized;
    bool isShowingWork;
    bool isWorkSfxPlaying;

    public ZoneType ZoneType => zoneType;
    public bool StartsUnlocked => startsUnlocked;
    public string DisplayLabel => string.IsNullOrEmpty(displayLabel) ? zoneType.ToString() : displayLabel;
    public ZoneItemView ItemView => itemView;

    public Vector2 RootPosition => transform.position;

    public Vector2 GetInputPosition() => ToVector2(inputPoint != null ? inputPoint : workPoint != null ? workPoint : transform);
    public Vector2 GetWorkPosition() => ToVector2(workPoint != null ? workPoint : transform);
    public Vector2 GetOutputPosition() => ToVector2(outputPoint != null ? outputPoint : workPoint != null ? workPoint : transform);
    public IReadOnlyList<Vector2> GetOutputPositions()
    {
        RefreshOutputPositions();
        return outputPositions;
    }

    public Vector2 GetOutputPosition(int slotIndex)
    {
        RefreshOutputPositions();
        if (outputPositions.Count == 0)
            return GetOutputPosition();

        int clamped = Mathf.Clamp(slotIndex, 0, outputPositions.Count - 1);
        return outputPositions[clamped];
    }

    public Vector2 GetWorkerRootPosition() => ToVector2(workerRoot != null ? workerRoot : transform);

    public IReadOnlyList<Vector2> GetMinionWorkPositions()
    {
        RefreshMinionWorkPositions();
        return minionWorkPositions;
    }

    public Vector2 GetMinionWorkPosition(int index)
    {
        RefreshMinionWorkPositions();
        if (minionWorkPositions.Count == 0)
            return GetWorkPosition();

        return minionWorkPositions[((index % minionWorkPositions.Count) + minionWorkPositions.Count) % minionWorkPositions.Count];
    }

    public void Setup(GameModel model, WorkerAssignService assignService, CompositeDisposable disposables)
    {
        this.model = model;

        SetupWorldUi(model, assignService, disposables);

        EnsureItemView(model);
        EnsureSourceView(model);
        EnsureDefaultBufferPiles(model);
        BindBufferPiles(model);
        SetupWorkAnimation(disposables);
    }

    void SetupWorkAnimation(CompositeDisposable disposables)
    {
        InitWorkVisual();

        if (zoneType != ZoneType.Ingredient)
            return;

        // Ingredient has no work phase: it plays a single work animation each
        // time a minion comes to pick something up, then reverts to notWork.
        if (model == null)
            return;

        model.ZoneSourcePicked
            .Where(picked => picked == zoneType)
            .Subscribe(_ => PlayWorkOnce())
            .AddTo(disposables);
    }

    void Update()
    {
        if (model == null)
            return;

        // Ingredient is event-driven (see SetupWorkAnimation), other zones
        // simply reflect their working phase.
        if (zoneType == ZoneType.Ingredient)
            return;

        SetWorkVisual(IsMachineWorking());
    }

    void OnDestroy()
    {
        StopWorkSfx();
    }

    bool IsMachineWorking()
    {
        if (model == null || !model.Zones.ContainsKey(zoneType))
            return false;

        return model.GetZone(zoneType).Phase == ZonePhase.Working;
    }

    void InitWorkVisual()
    {
        workVisualInitialized = true;
        isShowingWork = false;

        if (workObject != null)
            workObject.SetActive(false);
        if (notWorkObject != null)
            notWorkObject.SetActive(true);
    }

    void SetWorkVisual(bool working)
    {
        if (workVisualInitialized && working == isShowingWork)
            return;

        workVisualInitialized = true;
        isShowingWork = working;

        if (notWorkObject != null)
            notWorkObject.SetActive(!working);
        if (workObject != null)
            workObject.SetActive(working);

        if (working)
        {
            ResolveWorkAnimPlayer()?.Play(true);
            StartWorkSfx();
        }
        else
        {
            StopWorkSfx();
        }
    }

    void StartWorkSfx()
    {
        if (isWorkSfxPlaying)
            return;

        string path = GetWorkSfxEvent();
        if (string.IsNullOrEmpty(path))
            return;

        isWorkSfxPlaying = true;
        // TODO: FMODUnity.RuntimeManager.CreateInstance(path) + start()
        Debug.Log($"[ZoneSFX] Start {zoneType}: {path}");
    }

    void StopWorkSfx()
    {
        if (!isWorkSfxPlaying)
            return;

        string path = GetWorkSfxEvent();
        isWorkSfxPlaying = false;
        // TODO: stop + release EventInstance
        Debug.Log($"[ZoneSFX] Stop {zoneType}: {path}");
    }

    string GetWorkSfxEvent()
    {
        switch (zoneType)
        {
            case ZoneType.Chop: return "event:/SFX/Machines/sfx_chop_loop";
            case ZoneType.Cook: return "event:/SFX/Machines/sfx_mixer_loop";
            case ZoneType.Plate: return "event:/SFX/Machines/sfx_plate_loop";
            default: return null;
        }
    }

    void PlayWorkOnce()
    {
        if (workObject == null)
        {
            // Nothing to animate; keep notWork visible.
            return;
        }

        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Machines/sfx_open_fridge");

        if (notWorkObject != null)
            notWorkObject.SetActive(false);
        workObject.SetActive(true);
        isShowingWork = true;
        workVisualInitialized = true;

        var player = ResolveWorkAnimPlayer();
        if (player != null)
        {
            player.Play(false, ShowNotWork);
        }
        else
        {
            ShowNotWork();
        }
    }

    void ShowNotWork()
    {
        isShowingWork = false;
        workVisualInitialized = true;

        if (workObject != null)
            workObject.SetActive(false);
        if (notWorkObject != null)
            notWorkObject.SetActive(true);
    }

    SpriteAnimPlayer ResolveWorkAnimPlayer()
    {
        if (workAnimPlayer != null)
            return workAnimPlayer;

        if (workObject != null)
            workAnimPlayer = workObject.GetComponentInChildren<SpriteAnimPlayer>(true);

        return workAnimPlayer;
    }

    public void HideWorldUi()
    {
        if (worldUi == null)
            worldUi = GetComponentInChildren<ZoneWorldUIView>(true);

        if (worldUi != null)
            worldUi.gameObject.SetActive(false);
    }

    void SetupWorldUi(GameModel model, WorkerAssignService assignService, CompositeDisposable disposables)
    {
        if (worldUi == null)
            worldUi = GetComponentInChildren<ZoneWorldUIView>(true);

        if (worldUi == null)
            return;

        bool shouldShow = ShouldShowWorldUi();
        worldUi.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        worldUi.Setup(zoneType, model, assignService, RootPosition, DisplayLabel);
        worldUi.Bind(disposables);
    }

    void Reset()
    {
        showWorldUi = zoneType != ZoneType.Ingredient;
    }

    bool ShouldShowWorldUi()
    {
        if (!showWorldUi)
            return false;

        return zoneType != ZoneType.Ingredient;
    }

    void EnsureItemView(GameModel model)
    {
        if (itemView == null)
            itemView = GetComponentInChildren<ZoneItemView>(true);

        if (itemView == null && NeedsSharedItemView(zoneType))
        {
            var go = new GameObject("ZoneItem");
            go.transform.SetParent(transform, false);
            itemView = go.AddComponent<ZoneItemView>();
        }

        if (itemView != null)
            itemView.Setup(zoneType, model, model.Config);
    }

    void EnsureSourceView(GameModel model)
    {
        if (zoneType != ZoneType.Ingredient)
            return;

        if (sourceView == null)
            sourceView = GetComponentInChildren<ZoneSourceView>(true);

        // Ingredient zone should not keep a permanent food visual.
        if (sourceView != null)
            sourceView.gameObject.SetActive(false);
    }

    void EnsureDefaultBufferPiles(GameModel model)
    {
        if (bufferPiles != null && bufferPiles.Length > 0)
            return;

        // identifier 模式：每个加工区一个通用产出堆，展示该区所有产出（按 identifier 渲染）。
        switch (zoneType)
        {
            case ZoneType.Chop:
            case ZoneType.Cook:
            case ZoneType.Wok:
                bufferPiles = new[] { CreateBufferPile(model, zoneType + "OutputPile", FoodStage.None, FoodVisual.None) };
                break;
        }
    }

    ZoneBufferPileView CreateBufferPile(GameModel model, string name, FoodStage stage, FoodVisual visual)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var pile = go.AddComponent<ZoneBufferPileView>();
        pile.Setup(zoneType, model, model.Config, GetOutputPositions(), stage, visual);
        return pile;
    }

    void BindBufferPiles(GameModel model)
    {
        if (bufferPiles == null)
            return;

        var outputPos = GetOutputPosition();
        var outputSlots = GetOutputPositions();
        foreach (var pile in bufferPiles)
        {
            if (pile == null)
                continue;

            pile.transform.position = new Vector3(outputPos.x, outputPos.y, pile.transform.position.z);
            pile.BindExisting(zoneType, model, model.Config, outputSlots);
        }
    }

    void RefreshOutputPositions()
    {
        outputPositions.Clear();

        var baseOutput = outputPoint != null ? outputPoint : workPoint != null ? workPoint : transform;
        if (baseOutput == null)
            return;

        outputPositions.Add(baseOutput.position);
        for (int i = 0; i < baseOutput.childCount; i++)
            outputPositions.Add(baseOutput.GetChild(i).position);
    }

    void RefreshMinionWorkPositions()
    {
        minionWorkPositions.Clear();

        var baseWork = minionWorkPoint != null ? minionWorkPoint : null;
        if (baseWork == null)
            return;

        minionWorkPositions.Add(baseWork.position);
        for (int i = 0; i < baseWork.childCount; i++)
            minionWorkPositions.Add(baseWork.GetChild(i).position);
    }

    static bool NeedsSharedItemView(ZoneType type)
    {
        return type == ZoneType.Chop
            || type == ZoneType.Cook
            || type == ZoneType.Wok
            || type == ZoneType.Plate
            || type == ZoneType.Splitter;
    }

    static Vector2 ToVector2(Transform t) => t != null ? (Vector2)t.position : Vector2.zero;

    void OnDrawGizmosSelected()
    {
        DrawPoint(inputPoint, Color.green, "Input");
        DrawPoint(workPoint, Color.yellow, "Work");
        DrawPoint(outputPoint, Color.cyan, "Output");
        DrawPoint(minionWorkPoint, Color.magenta, "MinionWork");
        DrawPoint(workerRoot, Color.white, "Workers");
    }

    static void DrawPoint(Transform point, Color color, string label)
    {
        if (point == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(point.position, 0.15f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(point.position + Vector3.up * 0.25f, label);
#endif
    }
}
