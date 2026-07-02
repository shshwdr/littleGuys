using System.Linq;
using UniRx;

public class WorkerAssignService
{
    readonly GameModel model;

    public WorkerAssignService(GameModel model)
    {
        this.model = model;
    }

    public bool CanAddWorker(ZoneType zone)
    {
        if (zone == ZoneType.Ingredient || zone == ZoneType.Idle)
            return false;

        var zoneData = model.GetZone(zone);
        if (zoneData.WorkerCount.Value >= model.Config.maxWorkersPerZone)
            return false;

        return model.Workers.Any(w => w.AssignedZone == ZoneType.Idle);
    }

    public bool CanRemoveWorker(ZoneType zone)
    {
        if (zone == ZoneType.Ingredient || zone == ZoneType.Idle)
            return false;

        return model.GetZone(zone).WorkerCount.Value > 0;
    }

    public void TryAddWorker(ZoneType zone)
    {
        if (!CanAddWorker(zone))
            return;

        var worker = model.Workers.First(w => w.AssignedZone == ZoneType.Idle);
        AssignWorkerToZone(worker, zone);
    }

    public void TryRemoveWorker(ZoneType zone)
    {
        if (!CanRemoveWorker(zone))
            return;

        var worker = model.Workers.First(w => w.AssignedZone == zone);
        AssignWorkerToZone(worker, ZoneType.Idle);
    }

    public void AssignWorkerToZone(WorkerData worker, ZoneType zone)
    {
        var oldZone = worker.AssignedZone;
        if (oldZone == zone)
            return;

        if (oldZone != ZoneType.Idle)
            model.GetZone(oldZone).WorkerCount.Value--;

        worker.AssignedZone = zone;
        worker.HasArrivedAtZone = false;
        worker.Carrying = FoodStage.None;
        worker.DeliveryTarget = null;
        worker.State = WorkerState.WalkingToZone;

        if (zone != ZoneType.Idle)
            model.GetZone(zone).WorkerCount.Value++;

        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);

        model.WorkerAssignmentChanged.OnNext(Unit.Default);
    }

    public void RefreshZoneCounts()
    {
        foreach (var pair in model.Zones)
        {
            if (pair.Key == ZoneType.Ingredient || pair.Key == ZoneType.Idle)
                continue;

            pair.Value.WorkerCount.Value = model.Workers.Count(w => w.AssignedZone == pair.Key);
        }
    }
}
