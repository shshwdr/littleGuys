using System.Linq;
using UnityEngine;

public class ZoneWorkService
{
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
        if (zone.Phase != ZonePhase.Working || !zone.HasActiveStep)
            return;

        var workers = model.Workers.Where(w => w.AssignedZone == type).ToList();
        if (workers.Count == 0)
            return;

        int operatorCount = zone.SoloWorkerCount > 0
            ? Mathf.Min(zone.SoloWorkerCount, workers.Count)
            : workers.Count;

        zone.WorkRotation += model.Config.workRotationSpeed * dt;
        zone.WorkSpeed.Value = operatorCount / zone.BaseDuration;
        zone.TaskProgress.Value += (operatorCount / zone.BaseDuration) * dt;

        Vector2 workCenter = layout.GetItemCenterAboveZone(type);
        zone.SharedItemPosition = workCenter;
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedFoodVisual = zone.StepInputVisual;

        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            bool isOperator = i < operatorCount;
            worker.WorkRotation = zone.WorkRotation;
            worker.State = WorkerState.InZoneSync;
            worker.HasJoinedLift = true;

            if (isOperator)
                worker.Position = workCenter;
            else
                worker.Position = layout.GetLiftWorkerPosition(workCenter, i, workers.Count);
        }

        if (zone.TaskProgress.Value < 1f)
        {
            zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";
            return;
        }

        zone.TaskProgress.Value = 0f;
        zone.WorkRotation = 0f;
        ZoneOutputStore.Add(zone, zone.CurrentOrderId, zone.CurrentRecipeId, zone.StepOutput, zone.StepOutputVisual);
        ClearSharedItem(zone);
        zone.Phase = ZonePhase.Idle;
        zone.StatusText.Value = "0%";
        production.CompleteZoneStep(zone);
    }

    static void ClearSharedItem(ZoneData zone)
    {
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
        zone.SharedFoodVisual = FoodVisual.None;
    }
}
