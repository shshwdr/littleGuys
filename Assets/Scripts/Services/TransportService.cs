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

        UpdateWorkerStandPositions();
        foreach (var worker in model.Workers)
            TickWorker(worker, dt);
    }

    void TickWorker(WorkerData worker, float dt)
    {
        if (worker.AssignedZone == ZoneType.Idle)
        {
            TickIdleWorker(worker, dt);
            return;
        }

        if (model.ActiveRecipe.Value == null)
        {
            MoveToZoneHome(worker, dt);
            worker.State = WorkerState.Standing;
            worker.Carrying = FoodStage.None;
            worker.DeliveryTarget = null;
            return;
        }

        if (worker.State == WorkerState.WalkingToSource)
        {
            TickFetchingFromSource(worker, dt);
            return;
        }

        if (worker.Carrying == FoodStage.Plated && worker.DeliveryTarget != null)
        {
            TickDelivery(worker, dt);
            return;
        }

        if (worker.Carrying != FoodStage.None)
        {
            TickCarryingToZone(worker, dt);
            return;
        }

        if (worker.AssignedZone == ZoneType.Plate && TryBeginDelivery(worker))
            return;

        if (TryBeginFetch(worker))
            return;

        MoveToZoneHome(worker, dt);
        worker.State = WorkerState.Standing;
    }

    void TickIdleWorker(WorkerData worker, float dt)
    {
        worker.HasArrivedAtZone = false;
        worker.Carrying = FoodStage.None;
        worker.DeliveryTarget = null;
        worker.State = WorkerState.Standing;

        int index = GetZoneWorkerIndex(worker);
        int total = model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        worker.TargetPosition = layout.GetWorkerSlotPosition(ZoneType.Idle, index, total);
        MoveToward(worker, dt, ZoneType.Idle);
    }

    void TickFetchingFromSource(WorkerData worker, float dt)
    {
        worker.TargetPosition = GetSourcePosition(worker.AssignedZone);
        if (!MoveToward(worker, dt, worker.AssignedZone))
            return;

        if (!CanFetch(worker.AssignedZone))
        {
            worker.State = WorkerState.Standing;
            return;
        }

        var zone = model.GetZone(worker.AssignedZone);
        TakeFromSource(worker.AssignedZone);
        worker.Carrying = zone.StepInput;
        worker.State = WorkerState.CarryingToZone;
    }

    void TickDelivery(WorkerData worker, float dt)
    {
        worker.State = WorkerState.WalkingToCustomer;
        if (worker.DeliveryTarget == null || worker.DeliveryTarget.IsServed)
        {
            worker.Carrying = FoodStage.None;
            worker.DeliveryTarget = null;
            worker.State = WorkerState.Standing;
            return;
        }

        int customerIndex = model.Customers.IndexOf(worker.DeliveryTarget);
        if (customerIndex < 0)
        {
            worker.Carrying = FoodStage.None;
            worker.DeliveryTarget = null;
            return;
        }

        worker.TargetPosition = layout.GetCustomerPosition(customerIndex);
        if (MoveToward(worker, dt, worker.AssignedZone))
        {
            customerService.ServeCustomer(worker.DeliveryTarget);
            worker.Carrying = FoodStage.None;
            worker.DeliveryTarget = null;
            worker.State = WorkerState.Standing;
        }
    }

    void TickCarryingToZone(WorkerData worker, float dt)
    {
        worker.State = WorkerState.CarryingToZone;
        worker.TargetPosition = layout.GetZonePosition(worker.AssignedZone);

        if (!MoveToward(worker, dt, worker.AssignedZone))
            return;

        var zone = model.GetZone(worker.AssignedZone);
        zone.InputBuffer++;
        worker.Carrying = FoodStage.None;
        worker.State = WorkerState.Standing;
    }

    bool TryBeginDelivery(WorkerData worker)
    {
        var zone = model.GetZone(ZoneType.Plate);
        if (!zone.HasActiveStep || zone.OutputBuffer <= 0)
            return false;

        var customer = customerService.GetFirstWaitingCustomer();
        if (customer == null)
            return false;

        zone.OutputBuffer--;
        worker.Carrying = FoodStage.Plated;
        worker.DeliveryTarget = customer;
        worker.State = WorkerState.WalkingToCustomer;
        worker.HasArrivedAtZone = false;
        return true;
    }

    bool TryBeginFetch(WorkerData worker)
    {
        var zone = model.GetZone(worker.AssignedZone);
        if (!zone.HasActiveStep)
            return false;

        if (!CanFetch(worker.AssignedZone))
            return false;

        worker.State = WorkerState.WalkingToSource;
        worker.HasArrivedAtZone = false;
        worker.TargetPosition = GetSourcePosition(worker.AssignedZone);
        return true;
    }

    bool CanFetch(ZoneType zoneType)
    {
        switch (zoneType)
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

    void TakeFromSource(ZoneType zoneType)
    {
        switch (zoneType)
        {
            case ZoneType.Cook:
                model.GetZone(ZoneType.Chop).OutputBuffer--;
                break;
            case ZoneType.Plate:
                model.GetZone(ZoneType.Cook).OutputBuffer--;
                break;
        }
    }

    Vector2 GetSourcePosition(ZoneType zoneType)
    {
        switch (zoneType)
        {
            case ZoneType.Chop:
                return layout.GetZonePosition(ZoneType.Ingredient);
            case ZoneType.Cook:
                return layout.GetZonePosition(ZoneType.Chop);
            case ZoneType.Plate:
                return layout.GetZonePosition(ZoneType.Cook);
            default:
                return layout.GetZonePosition(zoneType);
        }
    }

    void MoveToZoneHome(WorkerData worker, float dt)
    {
        if (worker.State == WorkerState.WalkingToZone)
        {
            worker.TargetPosition = layout.GetZonePosition(worker.AssignedZone);
            if (MoveToward(worker, dt, worker.AssignedZone))
            {
                worker.HasArrivedAtZone = true;
                worker.State = WorkerState.Standing;
            }
            return;
        }

        int index = GetZoneWorkerIndex(worker);
        int total = model.Workers.Count(w => w.AssignedZone == worker.AssignedZone);
        worker.TargetPosition = layout.GetWorkerSlotPosition(worker.AssignedZone, index, total);

        if (MoveToward(worker, dt, worker.AssignedZone))
            worker.HasArrivedAtZone = true;
    }

    bool MoveToward(WorkerData worker, float dt, ZoneType speedZone)
    {
        float speed = model.Config.GetMoveSpeed(model.GetZone(speedZone).WorkerCount.Value);
        if (speed <= 0f)
            speed = model.Config.workerMoveSpeed * 0.1f;

        worker.Position = Vector2.MoveTowards(
            worker.Position,
            worker.TargetPosition,
            speed * dt);

        return Vector2.Distance(worker.Position, worker.TargetPosition) <= model.Config.arriveThreshold;
    }

    void UpdateWorkerStandPositions()
    {
        foreach (var group in model.Workers.Where(w => w.AssignedZone != ZoneType.Idle).GroupBy(w => w.AssignedZone))
        {
            var list = group.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var worker = list[i];
                if (worker.State != WorkerState.Standing || worker.Carrying != FoodStage.None)
                    continue;

                worker.TargetPosition = layout.GetWorkerSlotPosition(worker.AssignedZone, i, list.Count);
            }
        }
    }

    int GetZoneWorkerIndex(WorkerData worker)
    {
        var list = model.Workers.Where(w => w.AssignedZone == worker.AssignedZone).ToList();
        return list.IndexOf(worker);
    }
}
