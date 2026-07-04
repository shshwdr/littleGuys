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
        if (!zoneData.IsUnlocked)
            return false;

        if (zoneData.WorkerCount.Value >= model.Config.maxWorkersPerZone)
            return false;

        return model.Workers.Any(w => IsWorkerAvailableForAssign(w, zone));
    }

    bool IsWorkerAvailableForAssign(WorkerData worker, ZoneType targetZone)
    {
        if (!worker.CanAssign)
            return false;

        if (worker.AssignedZone == ZoneType.Idle)
            return true;

        if (worker.AssignedZone == targetZone || worker.AssignedZone == ZoneType.Ingredient)
            return false;

        return model.GetZone(worker.AssignedZone).Phase == ZonePhase.Idle;
    }

    public bool CanRemoveWorker(ZoneType zone)
    {
        if (zone == ZoneType.Ingredient || zone == ZoneType.Idle)
            return false;

        var zoneData = model.GetZone(zone);
        if (!zoneData.IsUnlocked)
            return false;

        int count = model.Workers.Count(w => w.AssignedZone == zone);
        if (count <= 0)
            return false;

        if (zone == ZoneType.Splitter && splitterService.IsSplitting())
            return count > 1;

        if (zoneData.Phase == ZonePhase.Working && zoneData.ConsumeWorkerAsInput)
            return count > 1;

        return true;
    }

    public void TryAddWorker(ZoneType zone)
    {
        if (!CanAddWorker(zone))
            return;

        var worker = model.Workers.FirstOrDefault(w => w.AssignedZone == ZoneType.Idle && w.CanAssign)
            ?? model.Workers.First(w => IsWorkerAvailableForAssign(w, zone));
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
        worker.SacrificeTarget = null;
        worker.HasArrivedAtZone = false;
        worker.HasJoinedLift = false;
        worker.PositionLocked = false;
        worker.WorkRotation = 0f;
        worker.State = WorkerState.WalkingToZone;

        if (zone != ZoneType.Idle)
            model.GetZone(zone).WorkerCount.Value++;

        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);

        model.NotifyWorkerAssignmentChanged();
    }
}
