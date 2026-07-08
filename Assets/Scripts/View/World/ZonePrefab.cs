using UniRx;
using UnityEngine;
using System.Collections.Generic;

public class ZonePrefab : MonoBehaviour
{
    [SerializeField] ZoneType zoneType;
    [SerializeField] Transform inputPoint;
    [SerializeField] Transform workPoint;
    [SerializeField] Transform outputPoint;
    [SerializeField] Transform workerRoot;
    [SerializeField] string displayLabel;
    [SerializeField] bool startsUnlocked = true;
    [SerializeField] bool showWorldUi = true;
    [SerializeField] ZoneWorldUIView worldUi;
    [SerializeField] ZoneItemView itemView;
    [SerializeField] ZoneSourceView sourceView;
    [SerializeField] ZoneBufferPileView[] bufferPiles;
    readonly List<Vector2> outputPositions = new List<Vector2>();

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

    public void Setup(GameModel model, WorkerAssignService assignService, CompositeDisposable disposables)
    {
        SetupWorldUi(model, assignService, disposables);

        EnsureItemView(model);
        EnsureSourceView(model);
        EnsureDefaultBufferPiles(model);
        BindBufferPiles(model);
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

        switch (zoneType)
        {
            case ZoneType.Chop:
                bufferPiles = new[]
                {
                    CreateBufferPile(model, "ChopOutputPileVeg", FoodStage.Chopped, FoodVisual.Veg),
                    CreateBufferPile(model, "ChopOutputPileMeat", FoodStage.Chopped, FoodVisual.Meat)
                };
                break;
            case ZoneType.Cook:
                bufferPiles = new[] { CreateBufferPile(model, "CookOutputPile", FoodStage.Cooked, FoodVisual.Veg) };
                break;
            case ZoneType.Wok:
                bufferPiles = new[] { CreateBufferPile(model, "WokOutputPile", FoodStage.Fried, FoodVisual.Meat) };
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
