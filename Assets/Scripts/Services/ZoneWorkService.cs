using System.Linq;
using UnityEngine;

public class ZoneWorkService
{
    readonly GameModel model;

    public ZoneWorkService(GameModel model)
    {
        this.model = model;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        if (model.ActiveRecipe.Value == null)
            return;

        UpdateZone(ZoneType.Chop, dt);
        UpdateZone(ZoneType.Cook, dt);
        UpdateZone(ZoneType.Plate, dt);
    }

    void UpdateZone(ZoneType type, float dt)
    {
        var zone = model.GetZone(type);
        if (!zone.HasActiveStep)
            return;

        int assignedCount = zone.WorkerCount.Value;
        zone.WorkSpeed.Value = model.Config.GetMoveSpeed(assignedCount);

        int workersReady = CountWorkersReadyToWork(type);
        if (zone.InputBuffer <= 0 || workersReady <= 0)
        {
            zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";
            return;
        }

        float workRate = workersReady / zone.BaseDuration;
        zone.TaskProgress.Value += workRate * dt;

        while (zone.TaskProgress.Value >= 1f && zone.InputBuffer > 0)
        {
            zone.TaskProgress.Value -= 1f;
            zone.InputBuffer--;
            zone.OutputBuffer++;
        }

        if (zone.TaskProgress.Value < 0f)
            zone.TaskProgress.Value = 0f;

        zone.StatusText.Value = $"{Mathf.RoundToInt(Mathf.Clamp01(zone.TaskProgress.Value) * 100f)}%";
    }

    public int CountArrivedWorkers(ZoneType type)
    {
        return model.Workers.Count(w =>
            w.AssignedZone == type &&
            w.HasArrivedAtZone &&
            w.State != WorkerState.WalkingToZone);
    }

    public int CountWorkersReadyToWork(ZoneType type)
    {
        return model.Workers.Count(w =>
            w.AssignedZone == type &&
            w.HasArrivedAtZone &&
            w.Carrying == FoodStage.None &&
            w.State == WorkerState.Standing);
    }
}
