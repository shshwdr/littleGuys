using System.Linq;
using UniRx;

public class WorkerAssignService
{
    readonly GameModel model;
    readonly SplitterService splitterService;

    public WorkerAssignService(GameModel model, SplitterService splitterService)
    {
        this.model = model;
        this.splitterService = splitterService;
    }

    public bool CanAddWorker(ZoneType zone)
    {
        if (zone == ZoneType.Ingredient || zone == ZoneType.Idle)
            return false;

        var zoneData = model.GetZone(zone);
        if (zoneData.WorkerCount.Value >= model.Config.maxWorkersPerZone)
            return false;

        return model.Workers.Any(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
    }

    public bool CanRemoveWorker(ZoneType zone)
    {
        if (zone == ZoneType.Ingredient || zone == ZoneType.Idle)
            return false;

        int count = model.Workers.Count(w => w.AssignedZone == zone);
        if (count <= 0)
            return false;

        if (zone == ZoneType.Splitter && splitterService.IsSplitting())
            return count > 1;

        var zoneData = model.GetZone(zone);
        if (zoneData.Phase == ZonePhase.Working && zoneData.ConsumeWorkerAsInput)
            return count > 1;

        return true;
    }

    public void TryAddWorker(ZoneType zone)
    {
        if (!CanAddWorker(zone))
            return;

        var worker = model.Workers.First(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
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
        worker.HasJoinedLift = false;
        worker.WorkRotation = 0f;
        worker.State = WorkerState.WalkingToZone;

        if (zone != ZoneType.Idle)
            model.GetZone(zone).WorkerCount.Value++;

        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);

        model.WorkerAssignmentChanged.OnNext(Unit.Default);
    }
}
