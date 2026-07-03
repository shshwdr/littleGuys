using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TransportService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly CustomerSpawnService customerService;
    readonly ProductionService production;

    public event Action<WorkerData> WorkerRemoved;

    static readonly ZoneType[] WorkZones =
    {
        ZoneType.Chop, ZoneType.Cook, ZoneType.Wok, ZoneType.Plate
    };

    public TransportService(
        GameModel model,
        WorldLayout layout,
        CustomerSpawnService customerService,
        ProductionService production)
    {
        this.model = model;
        this.layout = layout;
        this.customerService = customerService;
        this.production = production;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        TickIdleWorkers(dt);

        foreach (var type in WorkZones)
            TickWorkZone(type, dt);
    }

    void TickIdleWorkers(float dt)
    {
        var idleWorkers = model.Workers.Where(w => w.AssignedZone == ZoneType.Idle).ToList();
        for (int i = 0; i < idleWorkers.Count; i++)
        {
            var worker = idleWorkers[i];
            worker.HasJoinedLift = false;
            worker.WorkRotation = 0f;
            worker.TargetPosition = layout.GetWorkerSlotPosition(ZoneType.Idle, i, idleWorkers.Count);
            MoveWorkerFree(worker, dt);
            worker.State = worker.HasArrivedAtZone ? WorkerState.Standing : WorkerState.WalkingToZone;
        }
    }

    void TickWorkZone(ZoneType type, float dt)
    {
        var zone = model.GetZone(type);
        var workers = GetZoneWorkers(type);
        if (workers.Count == 0)
        {
            zone.Phase = ZonePhase.Idle;
            production.ClearZoneTask(zone);
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

        if (!production.TryActivateQueueHead(zone, type))
            return;

        if (zone.SpawnInputInZone)
        {
            if (!AllWorkersArrivedAtZone(workers))
                return;

            if (zone.ConsumeWorkerAsInput && workers.Count < 2)
                return;

            if (zone.ConsumeWorkerAsInput)
                ConsumeWorkerAsMaterial(workers[0]);

            BeginInZoneProcessing(zone, type);
        }
        else if (production.CanFetchForActiveTask(zone, type))
        {
            BeginFetch(zone, type);
        }
    }

    void BeginInZoneProcessing(ZoneData zone, ZoneType type)
    {
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedFoodVisual = zone.StepInputVisual;
        zone.SharedItemPosition = layout.GetItemCenterAboveZone(type);
        zone.Phase = ZonePhase.Working;
        zone.TaskProgress.Value = 0f;
        zone.StatusText.Value = "0%";
        zone.WorkRotation = 0f;
    }

    void BeginFetch(ZoneData zone, ZoneType type)
    {
        zone.Phase = ZonePhase.GoingToSource;
        zone.SharedMoveTarget = GetFetchItemPosition(type, zone);
        foreach (var worker in GetZoneWorkers(type))
            worker.HasJoinedLift = false;
    }

    void TickGoingToSource(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Fetching";
        zone.WorkSpeed.Value = model.Config.workerMoveSpeed;

        Vector2 gatherItemPos = zone.SharedMoveTarget;
        MoveWorkersToLiftFormation(workers, gatherItemPos, dt, joinLift: false);

        if (!AllReadyWorkersAtFormation(workers, gatherItemPos))
            return;

        if (!TakeOneFromSource(type, zone, out var visual))
        {
            zone.Phase = ZonePhase.Idle;
            production.ClearZoneTask(zone);
            return;
        }

        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedFoodVisual = visual;
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
            ResetAfterDelivery(zone);
            return;
        }

        int customerIndex = model.Customers.IndexOf(zone.DeliveryCustomer);
        if (customerIndex < 0)
        {
            ResetAfterDelivery(zone);
            return;
        }

        Vector2 target = layout.GetCustomerPosition(customerIndex) + new Vector2(0f, model.Config.carryYOffset * 0.5f);
        TickCarrying(zone, type, workers, dt, target, "Delivering");

        if (!HasReached(zone.SharedItemPosition, target))
            return;

        customerService.ServeCustomer(zone.DeliveryCustomer);
        production.OnOrderDelivered(zone.CurrentOrderId);
        ResetAfterDelivery(zone);
    }

    void ResetAfterDelivery(ZoneData zone)
    {
        zone.DeliveryCustomer = null;
        ClearSharedItem(zone);
        zone.Phase = ZonePhase.Idle;
        zone.CurrentOrderId = 0;
        zone.CurrentRecipeId = null;
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

        if (zone.Phase == ZonePhase.Returning && HasReached(zone.SharedItemPosition, zone.SharedMoveTarget))
        {
            zone.Phase = ZonePhase.Working;
            zone.TaskProgress.Value = 0f;
            zone.StatusText.Value = "0%";
            zone.WorkRotation = 0f;
            zone.SharedItemPosition = zone.SharedMoveTarget;
        }
    }

    bool TryStartDelivery(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        if (type != ZoneType.Plate)
            return false;

        var customer = customerService.GetFirstWaitingCustomer();
        if (customer == null)
            return false;

        if (!ZoneOutputStore.TryTake(zone, customer.RecipeId, FoodStage.Plated, out var item))
            return false;

        zone.DeliveryCustomer = customer;
        zone.HasSharedItem = true;
        zone.SharedItemStage = item.Stage;
        zone.SharedFoodVisual = item.Visual;
        zone.SharedItemPosition = layout.GetItemCenterAboveZone(type);
        zone.CurrentRecipeId = item.RecipeId;
        zone.CurrentOrderId = item.OrderId;
        zone.Phase = ZonePhase.Delivering;

        foreach (var worker in workers)
            worker.HasJoinedLift = worker.State != WorkerState.WalkingToZone;

        return true;
    }

    bool TakeOneFromSource(ZoneType type, ZoneData zone, out FoodVisual visual)
    {
        visual = FoodVisual.None;
        string recipeId = zone.CurrentRecipeId;

        if (type == ZoneType.Chop && !zone.ConsumeWorkerAsInput)
        {
            visual = FoodVisual.Veg;
            return true;
        }

        var upstream = ProductionService.GetUpstreamZone(type, recipeId);
        var upstreamZone = model.GetZone(upstream);
        var step = production.GetStepForZone(recipeId, type);
        var stage = step != null ? step.Input : FoodStage.None;

        if (!ZoneOutputStore.TryTake(upstreamZone, recipeId, stage, out var item, zone.CurrentOrderId))
            return false;

        visual = item.Visual;
        return true;
    }

    Vector2 GetFetchItemPosition(ZoneType type, ZoneData zone)
    {
        string recipeId = zone.CurrentRecipeId;

        if (type == ZoneType.Chop && !zone.ConsumeWorkerAsInput)
            return layout.GetSourceItemPosition(ZoneType.Chop);

        var upstream = ProductionService.GetUpstreamZone(type, recipeId);
        return layout.GetItemCenterAboveZone(upstream);
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
            worker.WorkRotation = 0f;
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
        zone.SharedFoodVisual = FoodVisual.None;
    }

    static bool AllWorkersArrivedAtZone(List<WorkerData> workers)
    {
        return workers.Count > 0 && workers.All(w => w.HasArrivedAtZone);
    }

    List<WorkerData> GetZoneWorkers(ZoneType type)
    {
        return model.Workers.Where(w => w.AssignedZone == type).ToList();
    }

    void ConsumeWorkerAsMaterial(WorkerData worker)
    {
        if (worker.AssignedZone != ZoneType.Idle)
            model.GetZone(worker.AssignedZone).WorkerCount.Value--;

        model.Workers.Remove(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        WorkerRemoved?.Invoke(worker);
    }
}
