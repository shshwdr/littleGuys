using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct FoodHandPickupRequest
{
    public CustomerData Customer;
    public FoodStage Stage;
    public FoodVisual Visual;
    public string RecipeId;
    public int OrderId;
}

public class TransportService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly CustomerSpawnService customerService;
    readonly ProductionService production;

    public event Action<WorkerData> WorkerRemoved;
    public event Action<FoodHandPickupRequest> FoodReadyForHandPickup;

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
        var idleWorkers = model.Workers
            .Where(w => w.AssignedZone == ZoneType.Idle && !w.IsSacrificing)
            .ToList();
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
        if (!zone.IsUnlocked)
            return;

        var workers = GetZoneWorkers(type);
        if (workers.Count == 0)
        {
            TickZoneWithoutWorkers(zone, type);
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
                TickCarrying(zone, type, workers, dt, layout.GetInputPosition(type), "Returning");
                break;
            case ZonePhase.Working:
                MoveWorkersToZoneSlots(workers, type, dt);
                break;
            case ZonePhase.Delivering:
                TickDelivering(zone, type, workers, dt);
                break;
            case ZonePhase.AwaitingHandPickup:
                TickAwaitingHandPickup(zone, type, workers, dt);
                break;
        }
    }

    void TickZoneWithoutWorkers(ZoneData zone, ZoneType type)
    {
        zone.WorkSpeed.Value = 0f;

        switch (zone.Phase)
        {
            case ZonePhase.Returning:
            case ZonePhase.Delivering:
            case ZonePhase.AwaitingHandPickup:
                if (zone.HasSharedItem)
                    zone.SharedItemPosition = layout.PlaceItemOnGround(
                        zone.SharedItemPosition,
                        zone.SharedMoveTarget);
                break;

            case ZonePhase.Working:
                if (zone.HasSharedItem)
                {
                    zone.SharedItemPosition = zone.ConsumeWorkerAsInput
                        ? layout.GetInputPosition(zone.Type)
                        : layout.GetWorkItemPosition(zone.Type);
                }
                zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";
                break;

            case ZonePhase.GoingToSource:
                zone.StatusText.Value = "Fetching";
                break;

            case ZonePhase.Idle:
                if (zone.HasActiveStep)
                    zone.StatusText.Value = "Waiting";
                break;
        }
    }

    void TickZoneIdle(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Waiting";
        zone.WorkSpeed.Value = 0f;
        ClearSharedItem(zone);
        UnlockWorkerPositions(workers);
        MoveWorkersToZoneSlots(workers, type, dt);

        if (TryStartDelivery(zone, type, workers))
            return;

        if (!production.TryActivateActiveTask(zone, type))
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
        zone.SharedItemPosition = layout.GetInputPosition(type);
        model.SetZonePhase(zone, ZonePhase.Working);
        zone.TaskProgress.Value = 0f;
        zone.StatusText.Value = "0%";
        zone.WorkRotation = 0f;

        var workers = GetZoneWorkers(type);
        LockWorkersForProcessing(workers);
    }

    void BeginFetch(ZoneData zone, ZoneType type)
    {
        model.SetZonePhase(zone, ZonePhase.GoingToSource);
        zone.SharedMoveTarget = GetFetchItemPosition(type, zone);
        foreach (var worker in GetZoneWorkers(type))
        {
            worker.HasJoinedLift = false;
            worker.PositionLocked = false;
            worker.HasArrivedAtZone = false;
            worker.State = WorkerState.WalkingToZone;
        }
    }

    void TickGoingToSource(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Fetching";
        zone.WorkSpeed.Value = model.Config.workerMoveSpeed;

        Vector2 gatherItemPos = zone.SharedMoveTarget;
        MoveWorkersToLiftFormation(workers, gatherItemPos, dt, joinLift: false);

        if (!FirstWorkerAtFormation(workers, gatherItemPos))
            return;

        if (!TakeOneFromSource(type, zone, out var visual))
        {
            ClearSharedItem(zone);
            model.SetZonePhase(zone, ZonePhase.Idle);
            production.CancelActiveTask(zone, type);
            return;
        }

        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedFoodVisual = visual;
        zone.SharedItemPosition = layout.ElevateCarriedItem(gatherItemPos);
        zone.SharedMoveTarget = layout.GetInputPosition(type);
        model.SetZonePhase(zone, ZonePhase.Returning);

        foreach (var worker in workers)
        {
            worker.HasJoinedLift = false;
            worker.PositionLocked = false;
        }
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

        Vector2 target = GetFoodOutputPosition();
        TickCarrying(zone, type, workers, dt, target, "Delivering");

        if (!HasReached(zone.SharedItemPosition, target))
            return;

        var request = new FoodHandPickupRequest
        {
            Customer = zone.DeliveryCustomer,
            Stage = zone.SharedItemStage,
            Visual = zone.SharedFoodVisual,
            RecipeId = zone.CurrentRecipeId,
            OrderId = zone.CurrentOrderId
        };
        FoodReadyForHandPickup?.Invoke(request);
        ResetAfterDelivery(zone);
        UnlockWorkerPositions(workers);
        MoveWorkersToZoneSlots(workers, type, dt);
    }

    void BeginAwaitingHandPickup(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        zone.SharedItemPosition = GetFoodOutputPosition();
        zone.StatusText.Value = "Waiting";
        zone.WorkSpeed.Value = 0f;
        model.SetZonePhase(zone, ZonePhase.AwaitingHandPickup);
        UnlockWorkerPositions(workers);
        MoveWorkersToZoneSlots(workers, type, 0f);
    }

    void TickAwaitingHandPickup(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.SharedItemPosition = GetFoodOutputPosition();
        zone.StatusText.Value = "Waiting";

        if (zone.DeliveryCustomer != null && zone.DeliveryCustomer.IsServed)
        {
            ResetAfterDelivery(zone);
            UnlockWorkerPositions(workers);
            MoveWorkersToZoneSlots(workers, type, dt);
            return;
        }

        UnlockWorkerPositions(workers);
        MoveWorkersToZoneSlots(workers, type, dt);
    }

    public void CompleteAwaitingHandPickup(int orderId)
    {
        var zone = model.GetZone(ZoneType.Plate);
        if (zone.Phase != ZonePhase.AwaitingHandPickup || zone.CurrentOrderId != orderId)
            return;

        ResetAfterDelivery(zone);
    }

    Vector2 GetFoodOutputPosition()
    {
        return layout.GetFoodOutputPosition();
    }

    int GetRecipeSatiety(string recipeId)
    {
        var recipe = model.GetRecipe(recipeId);
        return recipe != null ? recipe.Satiety : 0;
    }

    void ResetAfterDelivery(ZoneData zone)
    {
        zone.DeliveryCustomer = null;
        ClearSharedItem(zone);
        model.SetZonePhase(zone, ZonePhase.Idle);
        zone.CurrentOrderId = 0;
        zone.CurrentRecipeId = null;
    }

    void TickSharedLift(ZoneData zone, List<WorkerData> workers, float dt)
    {
        if (!zone.HasSharedItem)
            return;

        MoveWorkersToLiftFormation(workers, zone.SharedItemPosition, dt, joinLift: true);

        var joinedWorkers = workers.Where(w => w.HasJoinedLift).ToList();
        if (joinedWorkers.Count == 0)
        {
            zone.WorkSpeed.Value = 0f;
            return;
        }

        float speed = model.Config.GetMoveSpeed(joinedWorkers.Count);
        zone.WorkSpeed.Value = speed;
        zone.SharedItemPosition = Vector2.MoveTowards(
            zone.SharedItemPosition,
            zone.SharedMoveTarget,
            speed * dt);

        if (zone.Phase == ZonePhase.Returning && HasReached(zone.SharedItemPosition, zone.SharedMoveTarget))
        {
            model.SetZonePhase(zone, ZonePhase.Working);
            zone.TaskProgress.Value = 0f;
            zone.StatusText.Value = "0%";
            zone.WorkRotation = 0f;
            zone.SharedItemPosition = zone.ConsumeWorkerAsInput
                ? layout.GetInputPosition(zone.Type)
                : layout.GetWorkItemPosition(zone.Type);
            LockWorkersForProcessing(workers);
        }
    }

    bool TryStartDelivery(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        if (type != ZoneType.Plate)
            return false;

        var customer = customerService.GetFirstWaitingCustomer();
        if (customer == null)
            return false;

        if (!ZoneOutputStore.TryTake(zone, null, FoodStage.Plated, out var item))
            return false;

        zone.DeliveryCustomer = customer;
        zone.HasSharedItem = true;
        zone.SharedItemStage = item.Stage;
        zone.SharedFoodVisual = item.Visual;
        zone.SharedItemPosition = layout.ElevateCarriedItem(layout.GetOutputPosition(type));
        zone.CurrentRecipeId = item.RecipeId;
        zone.CurrentOrderId = item.OrderId;
        model.SetZonePhase(zone, ZonePhase.Delivering);

        foreach (var worker in workers)
        {
            worker.HasJoinedLift = false;
            worker.PositionLocked = false;
            worker.HasArrivedAtZone = false;
            worker.State = WorkerState.WalkingToZone;
        }

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

        var upstream = production.GetUpstreamZone(type, recipeId);
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
            return layout.GetOutputPosition(ZoneType.Ingredient);

        var upstream = production.GetUpstreamZone(type, recipeId);
        return layout.GetOutputPosition(upstream);
    }

    void MoveWorkersToLiftFormation(List<WorkerData> workers, Vector2 objectCenter, float dt, bool joinLift)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            if (worker.PositionLocked)
                worker.PositionLocked = false;

            Vector2 slot = layout.GetLiftWorkerPosition(objectCenter, i, workers.Count);
            worker.TargetPosition = slot;
            worker.Position = Vector2.MoveTowards(
                worker.Position,
                slot,
                model.Config.workerMoveSpeed * dt);

            if (Vector2.Distance(worker.Position, slot) <= model.Config.arriveThreshold)
            {
                worker.Position = slot;
                worker.HasArrivedAtZone = true;
                worker.State = WorkerState.InZoneSync;
                if (joinLift)
                    worker.HasJoinedLift = true;
            }
            else
            {
                worker.HasArrivedAtZone = false;
                worker.State = WorkerState.WalkingToZone;
                worker.HasJoinedLift = false;
            }
        }
    }

    void MoveWorkersToZoneSlots(List<WorkerData> workers, ZoneType type, float dt)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            if (worker.PositionLocked)
                continue;

            worker.HasJoinedLift = false;
            worker.WorkRotation = 0f;
            worker.TargetPosition = layout.GetWorkerSlotPosition(type, i, workers.Count);
            MoveWorkerFree(worker, dt);

            if (!worker.HasArrivedAtZone)
                worker.State = WorkerState.WalkingToZone;
            else
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

    bool FirstWorkerAtFormation(List<WorkerData> workers, Vector2 objectCenter)
    {
        if (workers.Count == 0)
            return false;

        Vector2 slot = layout.GetLiftWorkerPosition(objectCenter, 0, workers.Count);
        return Vector2.Distance(workers[0].Position, slot) <= model.Config.arriveThreshold;
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
        model.NotifyWorkerAssignmentChanged();
        WorkerRemoved?.Invoke(worker);
    }

    static void LockWorkersForProcessing(List<WorkerData> workers)
    {
        foreach (var worker in workers)
        {
            if (!worker.HasArrivedAtZone)
                continue;

            worker.PositionLocked = true;
            worker.WorkRotation = 0f;
            worker.State = WorkerState.InZoneSync;
        }
    }

    static void UnlockWorkerPositions(List<WorkerData> workers)
    {
        foreach (var worker in workers)
            worker.PositionLocked = false;
    }
}
