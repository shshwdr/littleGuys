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
        if (!zone.HasActiveStep || zone.Phase != ZonePhase.Working)
            return;

        int workerCount = model.Workers.Count(w => w.AssignedZone == type);
        if (workerCount <= 0)
            return;

        zone.WorkSpeed.Value = workerCount / zone.BaseDuration;
        zone.TaskProgress.Value += (workerCount / zone.BaseDuration) * dt;

        if (zone.TaskProgress.Value < 1f)
        {
            zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";
            return;
        }

        zone.TaskProgress.Value = 0f;
        zone.OutputBuffer++;
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
        zone.Phase = ZonePhase.Idle;
        zone.StatusText.Value = "0%";
    }
}
