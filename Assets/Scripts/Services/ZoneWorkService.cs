using System.Linq;
using UnityEngine;

public class ZoneWorkService
{
    public event System.Action<ZoneType> ZoneStepCompleted;
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly ProductionService production;

    static readonly ZoneType[] WorkZones =
    {
        ZoneType.Chop, ZoneType.Cook, ZoneType.Wok, ZoneType.Plate
    };

    public ZoneWorkService(GameModel model, WorldLayout layout, ProductionService production)
    {
        this.model = model;
        this.layout = layout;
        this.production = production;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        foreach (var type in WorkZones)
            UpdateZone(type, dt);
    }

    void UpdateZone(ZoneType type, float dt)
    {
        var zone = model.GetZone(type);
        if (!zone.IsUnlocked)
            return;

        if (zone.Phase != ZonePhase.Working || !zone.HasActiveStep)
            return;

        var workers = model.Workers.Where(w => w.AssignedZone == type).ToList();
        if (workers.Count == 0)
            return;

        var arrivedWorkers = workers.Where(w => w.HasArrivedAtZone).ToList();
        foreach (var worker in workers)
        {
            if (worker.HasArrivedAtZone)
            {
                worker.PositionLocked = true;
                worker.State = WorkerState.InZoneSync;
                worker.WorkRotation = 0f;
            }
            else
            {
                worker.PositionLocked = false;
            }
        }

        if (arrivedWorkers.Count < workers.Count)
        {
            zone.StatusText.Value = "Waiting";
            zone.WorkSpeed.Value = 0f;
            return;
        }

        int operatorCount = zone.SoloWorkerCount > 0
            ? Mathf.Min(zone.SoloWorkerCount, arrivedWorkers.Count)
            : arrivedWorkers.Count;

        zone.WorkRotation += model.Config.workRotationSpeed * dt;
        zone.WorkSpeed.Value = operatorCount / zone.BaseDuration;
        zone.TaskProgress.Value += (operatorCount / zone.BaseDuration) * dt;

        Vector2 itemCenter = layout.GetWorkItemPosition(type);
        zone.SharedItemPosition = itemCenter;
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        if (zone.ConsumeWorkerAsInput)
            zone.SharedFoodVisual = FoodVisual.Minion;

        if (zone.TaskProgress.Value < 1f)
        {
            zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";
            return;
        }

        zone.TaskProgress.Value = 0f;
        zone.WorkRotation = 0f;
        foreach (var worker in workers)
            worker.PositionLocked = false;

        int outputCount = 1;
        if (type == ZoneType.Chop && model.Config.doubleCutEnabled && Random.value < 0.5f)
            outputCount = 2;
        else if (type == ZoneType.Cook && model.Config.doubleCookEnabled && Random.value < 0.5f)
            outputCount = 2;

        for (int i = 0; i < outputCount; i++)
            ZoneOutputStore.Add(zone, zone.CurrentOrderId, zone.CurrentRecipeId, zone.StepOutputId, zone.StepOutput, zone.StepOutputVisual);
        ClearSharedItem(zone);
        model.SetZonePhase(zone, ZonePhase.Idle);
        zone.StatusText.Value = "0%";
        production.CompleteZoneStep(zone, type);
        ZoneStepCompleted?.Invoke(type);
    }

    static void ClearSharedItem(ZoneData zone)
    {
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
        zone.SharedFoodVisual = FoodVisual.None;
        zone.SharedItemId = "";
    }
}
