using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TransportService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly CustomerSpawnService customerService;

    public TransportService(
        GameModel model,
        WorldLayout layout,
        CustomerSpawnService customerService)
    {
        this.model = model;
        this.layout = layout;
        this.customerService = customerService;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        TickIdleWorkers(dt);

        if (model.ActiveRecipe.Value == null)
        {
            ResetWorkZonesToStandby();
            return;
        }

        TickWorkZone(ZoneType.Chop, dt);
        TickWorkZone(ZoneType.Cook, dt);
        TickWorkZone(ZoneType.Plate, dt);
    }

    void TickIdleWorkers(float dt)
    {
        var idleWorkers = model.Workers.Where(w => w.AssignedZone == ZoneType.Idle).ToList();
        for (int i = 0; i < idleWorkers.Count; i++)
        {
            var worker = idleWorkers[i];
            worker.State = WorkerState.Standing;
            worker.HasJoinedLift = false;
            worker.HasArrivedAtZone = false;
            worker.TargetPosition = layout.GetWorkerSlotPosition(ZoneType.Idle, i, idleWorkers.Count);
            MoveWorkerFree(worker, dt);
        }
    }

    void ResetWorkZonesToStandby()
    {
        foreach (var type in new[] { ZoneType.Chop, ZoneType.Cook, ZoneType.Plate })
        {
            var zone = model.GetZone(type);
            ClearSharedItem(zone);
            zone.Phase = ZonePhase.Idle;
            zone.DeliveryCustomer = null;

            foreach (var worker in GetZoneWorkers(type))
            {
                worker.HasJoinedLift = false;
                worker.State = WorkerState.InZoneSync;
            }
        }
    }

    void TickWorkZone(ZoneType type, float dt)
    {
        var zone = model.GetZone(type);
        var workers = GetZoneWorkers(type);

        if (!zone.HasActiveStep || workers.Count == 0)
        {
            zone.Phase = ZonePhase.Idle;
            ClearSharedItem(zone);
            return;
        }

        switch (zone.Phase)
        {
            case ZonePhase.Idle:
                TickZoneIdle(zone, type, workers, dt);
                break;
            case ZonePhase.GoingToSource:
                TickGoingToSource(zone, type, workers, dt);
                break;
            case ZonePhase.Returning:
                TickCarrying(zone, type, workers, dt, layout.GetItemCenterAboveZone(type), "Returning");
                break;
            case ZonePhase.Working:
                TickWorkingStand(zone, type, workers, dt);
                break;
            case ZonePhase.Delivering:
                TickDelivering(zone, type, workers, dt);
                break;
        }
    }

    void TickZoneIdle(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Waiting";
        zone.WorkSpeed.Value = 0f;
        ClearSharedItem(zone);
        MoveWorkersToZoneSlots(workers, type, dt);

        if (TryStartDelivery(zone, type, workers))
            return;

        if (CanStartFetch(type))
        {
            zone.Phase = ZonePhase.GoingToSource;
            zone.SharedMoveTarget = layout.GetSourceItemPosition(type);
            foreach (var worker in workers)
                worker.HasJoinedLift = false;
        }
    }

    void TickGoingToSource(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Fetching";
        zone.WorkSpeed.Value = model.Config.workerMoveSpeed;

        Vector2 gatherItemPos = layout.GetSourceItemPosition(type);
        MoveWorkersToLiftFormation(workers, gatherItemPos, dt, joinLift: false);

        if (!AllReadyWorkersAtFormation(workers, gatherItemPos))
            return;

        if (!TakeOneFromSource(type))
        {
            zone.Phase = ZonePhase.Idle;
            return;
        }

        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedItemPosition = gatherItemPos;
        zone.SharedMoveTarget = layout.GetItemCenterAboveZone(type);
        zone.Phase = ZonePhase.Returning;

        foreach (var worker in workers)
            worker.HasJoinedLift = worker.State != WorkerState.WalkingToZone;
    }

    void TickCarrying(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt, Vector2 target, string status)
    {
        zone.StatusText.Value = status;
        zone.SharedMoveTarget = target;
        TickSharedLift(zone, workers, dt);
    }

    void TickDelivering(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        if (zone.DeliveryCustomer == null || zone.DeliveryCustomer.IsServed)
        {
            ClearSharedItem(zone);
            zone.Phase = ZonePhase.Idle;
            zone.DeliveryCustomer = null;
            return;
        }

        int customerIndex = model.Customers.IndexOf(zone.DeliveryCustomer);
        if (customerIndex < 0)
        {
            ClearSharedItem(zone);
            zone.Phase = ZonePhase.Idle;
            zone.DeliveryCustomer = null;
            return;
        }

        Vector2 target = layout.GetCustomerPosition(customerIndex) + new Vector2(0f, model.Config.carryYOffset * 0.5f);
        TickCarrying(zone, type, workers, dt, target, "Delivering");

        if (!HasReached(zone.SharedItemPosition, target))
            return;

        customerService.ServeCustomer(zone.DeliveryCustomer);
        zone.DeliveryCustomer = null;
        ClearSharedItem(zone);
        zone.Phase = ZonePhase.Idle;
    }

    void TickSharedLift(ZoneData zone, List<WorkerData> workers, float dt)
    {
        if (!zone.HasSharedItem)
            return;

        MoveWorkersToLiftFormation(workers, zone.SharedItemPosition, dt, joinLift: true);

        if (workers.Count > 0)
        {
            float speed = model.Config.GetMoveSpeed(workers.Count);
            zone.WorkSpeed.Value = speed;
            zone.SharedItemPosition = Vector2.MoveTowards(
                zone.SharedItemPosition,
                zone.SharedMoveTarget,
                speed * dt);
        }
        else
        {
            zone.WorkSpeed.Value = 0f;
        }

        if (zone.Phase == ZonePhase.Returning && HasReached(zone.SharedItemPosition, zone.SharedMoveTarget))
        {
            zone.Phase = ZonePhase.Working;
            zone.TaskProgress.Value = 0f;
            zone.StatusText.Value = "0%";
            zone.SharedItemPosition = zone.SharedMoveTarget;
        }
    }

    void TickWorkingStand(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.SharedItemPosition = layout.GetItemCenterAboveZone(type);
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;

        MoveWorkersToLiftFormation(workers, zone.SharedItemPosition, dt, joinLift: true);

        for (int i = 0; i < workers.Count; i++)
        {
            if (workers[i].State == WorkerState.WalkingToZone)
                continue;

            workers[i].State = WorkerState.InZoneSync;
            workers[i].HasJoinedLift = true;
        }
    }

    bool TryStartDelivery(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        if (type != ZoneType.Plate || zone.OutputBuffer <= 0)
            return false;

        var customer = customerService.GetFirstWaitingCustomer();
        if (customer == null)
            return false;

        zone.OutputBuffer--;
        zone.DeliveryCustomer = customer;
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepOutput;
        zone.SharedItemPosition = layout.GetItemCenterAboveZone(type);
        zone.Phase = ZonePhase.Delivering;

        foreach (var worker in workers)
            worker.HasJoinedLift = worker.State != WorkerState.WalkingToZone;

        return true;
    }

    bool CanStartFetch(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Chop:
                return true;
            case ZoneType.Cook:
                return model.GetZone(ZoneType.Chop).OutputBuffer > 0;
            case ZoneType.Plate:
                return model.GetZone(ZoneType.Cook).OutputBuffer > 0;
            default:
                return false;
        }
    }

    bool TakeOneFromSource(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Chop:
                return true;
            case ZoneType.Cook:
                var chop = model.GetZone(ZoneType.Chop);
                if (chop.OutputBuffer <= 0)
                    return false;
                chop.OutputBuffer--;
                return true;
            case ZoneType.Plate:
                var cook = model.GetZone(ZoneType.Cook);
                if (cook.OutputBuffer <= 0)
                    return false;
                cook.OutputBuffer--;
                return true;
            default:
                return false;
        }
    }

    void MoveWorkersToLiftFormation(List<WorkerData> workers, Vector2 objectCenter, float dt, bool joinLift)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            Vector2 slot = layout.GetLiftWorkerPosition(objectCenter, i, workers.Count);

            if (joinLift && worker.HasJoinedLift)
            {
                worker.Position = slot;
                worker.State = WorkerState.InZoneSync;
                continue;
            }

            worker.Position = Vector2.MoveTowards(worker.Position, slot, model.Config.workerMoveSpeed * dt);

            if (Vector2.Distance(worker.Position, slot) <= model.Config.arriveThreshold)
            {
                worker.Position = slot;
                worker.State = WorkerState.InZoneSync;
                if (joinLift)
                    worker.HasJoinedLift = true;
            }
            else if (worker.State != WorkerState.WalkingToZone)
            {
                worker.State = WorkerState.InZoneSync;
            }
        }
    }

    void MoveWorkersToZoneSlots(List<WorkerData> workers, ZoneType type, float dt)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            worker.HasJoinedLift = false;
            worker.TargetPosition = layout.GetWorkerSlotPosition(type, i, workers.Count);
            MoveWorkerFree(worker, dt);

            if (worker.State == WorkerState.WalkingToZone && worker.HasArrivedAtZone)
                worker.State = WorkerState.InZoneSync;
            else if (worker.State != WorkerState.WalkingToZone)
                worker.State = WorkerState.InZoneSync;
        }
    }

    void MoveWorkerFree(WorkerData worker, float dt)
    {
        worker.Position = Vector2.MoveTowards(
            worker.Position,
            worker.TargetPosition,
            model.Config.workerMoveSpeed * dt);
        worker.HasArrivedAtZone =
            Vector2.Distance(worker.Position, worker.TargetPosition) <= model.Config.arriveThreshold;
    }

    bool AllReadyWorkersAtFormation(List<WorkerData> workers, Vector2 objectCenter)
    {
        bool anyReady = false;

        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            if (worker.State == WorkerState.WalkingToZone)
                continue;

            anyReady = true;
            Vector2 slot = layout.GetLiftWorkerPosition(objectCenter, i, workers.Count);
            if (Vector2.Distance(worker.Position, slot) > model.Config.arriveThreshold)
                return false;
        }

        return anyReady;
    }

    bool HasReached(Vector2 current, Vector2 target)
    {
        return Vector2.Distance(current, target) <= model.Config.arriveThreshold;
    }

    static void ClearSharedItem(ZoneData zone)
    {
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
    }

    List<WorkerData> GetZoneWorkers(ZoneType type)
    {
        return model.Workers.Where(w => w.AssignedZone == type).ToList();
    }
}
