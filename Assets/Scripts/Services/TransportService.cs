using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct FoodHandPickupRequest
{
    public CustomerData Customer;
    public FoodStage Stage;
    public FoodVisual Visual;
    public string Identifier;
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
            case ZonePhase.AwaitingWorkers:
                TickAwaitingWorkers(zone, type, workers, dt);
                break;
            case ZonePhase.Working:
                MoveWorkersToZoneSlots(workers, type, dt);
                break;
            case ZonePhase.Delivering:
                TickDelivering(zone, type, workers, dt);
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

            case ZonePhase.AwaitingWorkers:
                zone.StatusText.Value = "Positioning";
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
            if (zone.ConsumeWorkerAsInput && workers.Count < 2)
                return;

            BeginAwaitingWorkers(zone, type, workers);
        }
        else if (production.CanFetchForActiveTask(zone, type))
        {
            BeginFetch(zone, type);
        }
    }

    void BeginAwaitingWorkers(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        UnlockWorkerPositions(workers);
        ResetWorkersForTrip(workers);
        model.SetZonePhase(zone, ZonePhase.AwaitingWorkers);
        zone.StatusText.Value = "Positioning";
        zone.WorkSpeed.Value = 0f;
    }

    void TickAwaitingWorkers(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
    {
        zone.StatusText.Value = "Positioning";
        zone.WorkSpeed.Value = 0f;
        MoveWorkersToWorkPositions(workers, type, dt, zone.ConsumeWorkerAsInput);

        if (!AllWorkersArrivedAtZone(workers))
            return;

        if (zone.ConsumeWorkerAsInput)
        {
            ConsumeWorkerAsMaterial(workers[0]);
            workers = GetZoneWorkers(type);
            BeginInZoneProcessing(zone, type);
            return;
        }

        BeginWorkingFromCollected(zone, type, workers);
    }

    void BeginInZoneProcessing(ZoneData zone, ZoneType type)
    {
        zone.HasSharedItem = true;
        zone.SharedItemStage = zone.StepInput;
        zone.SharedFoodVisual = zone.ConsumeWorkerAsInput ? FoodVisual.Minion : zone.StepInputVisual;
        zone.SharedItemId = "";
        zone.SharedItemPosition = layout.GetWorkItemPosition(type);
        model.SetZonePhase(zone, ZonePhase.Working);
        zone.TaskProgress.Value = 0f;
        zone.StatusText.Value = "0%";
        zone.WorkRotation = 0f;

        var workers = GetZoneWorkers(type);
        LockWorkersForProcessing(workers);
    }

    void BeginFetch(ZoneData zone, ZoneType type)
    {
        zone.FetchInputIndex = 0;
        zone.CollectedInputs.Clear();
        model.SetZonePhase(zone, ZonePhase.GoingToSource);
        zone.SharedMoveTarget = GetFetchItemPosition(zone);
        ResetWorkersForTrip(GetZoneWorkers(type));
    }

    void ResetWorkersForTrip(List<WorkerData> workers)
    {
        foreach (var worker in workers)
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

        if (!TryTakeCurrentInput(zone, out var id, out var stage, out var visual))
        {
            ClearSharedItem(zone);
            model.SetZonePhase(zone, ZonePhase.Idle);
            production.CancelActiveTask(zone, type);
            return;
        }

        zone.HasSharedItem = true;
        zone.SharedItemStage = stage;
        zone.SharedFoodVisual = visual;
        zone.SharedItemId = id;
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
        TickSharedLift(zone, type, workers, dt);
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
            Identifier = zone.SharedItemId,
            RecipeId = zone.CurrentRecipeId,
            OrderId = zone.CurrentOrderId
        };

        // 成品放到取餐位后，view 会生成独立的成品显示并等手来取；
        // 这里预扣该顾客的份额，避免下一份被重复投喂给同一顾客。
        customerService.ReservePendingSatiety(
            zone.DeliveryCustomer,
            customerService.ComputeDeliverySatiety(zone.CurrentRecipeId));

        // 立刻收尾，plate 工人无需等顾客取走食物即可开始下一步行动。
        ResetAfterDelivery(zone);
        UnlockWorkerPositions(workers);
        MoveWorkersToZoneSlots(workers, type, dt);
        FoodReadyForHandPickup?.Invoke(request);
    }

    Vector2 GetFoodOutputPosition()
    {
        return layout.GetFoodOutputPosition();
    }

    void ResetAfterDelivery(ZoneData zone)
    {
        zone.DeliveryCustomer = null;
        ClearSharedItem(zone);
        model.SetZonePhase(zone, ZonePhase.Idle);
        zone.CurrentOrderId = 0;
        zone.CurrentRecipeId = null;
    }

    void TickSharedLift(ZoneData zone, ZoneType type, List<WorkerData> workers, float dt)
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
            DepositAndAdvance(zone, type, workers);
    }

    // 存下当前取回的原料，若还有原料未取则继续外出取，否则集齐开始加工。
    void DepositAndAdvance(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        // 放下的原料留在区内显示（按索引横向错开），直到开始加工才移除。
        Vector2 depositPos = layout.GetInputPosition(type) + new Vector2(zone.FetchInputIndex * 0.35f, 0f);
        zone.CollectedInputs.Add(new CollectedInput
        {
            Id = zone.SharedItemId,
            Stage = zone.SharedItemStage,
            Visual = zone.SharedFoodVisual,
            Position = depositPos
        });
        zone.FetchInputIndex++;
        ClearSharedItem(zone);

        if (!production.AllInputsCollected(zone))
        {
            model.SetZonePhase(zone, ZonePhase.GoingToSource);
            zone.SharedMoveTarget = GetFetchItemPosition(zone);
            ResetWorkersForTrip(workers);
            return;
        }

        BeginAwaitingWorkers(zone, type, workers);
    }

    void BeginWorkingFromCollected(ZoneData zone, ZoneType type, List<WorkerData> workers)
    {
        var representative = zone.CollectedInputs.FirstOrDefault();
        model.SetZonePhase(zone, ZonePhase.Working);
        zone.TaskProgress.Value = 0f;
        zone.StatusText.Value = "0%";
        zone.WorkRotation = 0f;
        zone.HasSharedItem = true;
        zone.SharedItemStage = representative != null ? representative.Stage : zone.StepInput;
        zone.SharedFoodVisual = representative != null ? representative.Visual : zone.StepInputVisual;
        zone.SharedItemId = representative != null ? representative.Id : "";
        zone.SharedItemPosition = layout.GetWorkItemPosition(type);
        LockWorkersForProcessing(workers);
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
        zone.SharedItemId = item.Identifier;
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

    // 取当前索引指向的原料。原料区（Ingredient）是无限来源，直接取；
    // 否则从来源机器的产出堆按 identifier 取出一个。
    bool TryTakeCurrentInput(ZoneData zone, out string id, out FoodStage stage, out FoodVisual visual)
    {
        id = "";
        stage = FoodStage.None;
        visual = FoodVisual.None;

        var input = production.CurrentFetchInput(zone);
        if (input == null)
            return false;

        if (input.FromIngredientSource)
        {
            id = input.Id;
            stage = input.Stage;
            visual = FoodVisual.None;
            model.ZoneSourcePicked.OnNext(ZoneType.Ingredient);
            return true;
        }

        var sourceZone = model.GetZone(input.Source);
        if (!ZoneOutputStore.TryTake(sourceZone, input.Id, input.Stage, out var item))
            return false;

        id = item.Identifier;
        stage = item.Stage;
        visual = item.Visual;

        if (input.Source == ZoneType.Ingredient)
            model.ZoneSourcePicked.OnNext(ZoneType.Ingredient);

        return true;
    }

    Vector2 GetFetchItemPosition(ZoneData zone)
    {
        var input = production.CurrentFetchInput(zone);
        if (input == null)
            return layout.GetInputPosition(zone.Type);

        return layout.GetOutputPosition(input.Source);
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
        MoveWorkersToWorkPositions(workers, type, dt, materialWorkerAtWork: false);
    }

    void MoveWorkersToWorkPositions(List<WorkerData> workers, ZoneType type, float dt, bool materialWorkerAtWork)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            if (worker.PositionLocked)
                continue;

            worker.HasJoinedLift = false;
            worker.WorkRotation = 0f;
            worker.TargetPosition = GetWorkerTargetPosition(type, i, materialWorkerAtWork);
            MoveWorkerFree(worker, dt);

            if (!worker.HasArrivedAtZone)
                worker.State = WorkerState.WalkingToZone;
            else
                worker.State = WorkerState.InZoneSync;
        }
    }

    Vector2 GetWorkerTargetPosition(ZoneType type, int workerIndex, bool materialWorkerAtWork)
    {
        if (materialWorkerAtWork && workerIndex == 0)
            return layout.GetWorkItemPosition(type);

        int workSlotIndex = materialWorkerAtWork ? workerIndex - 1 : workerIndex;
        return layout.GetMinionWorkPosition(type, workSlotIndex);
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

    bool HasReached(Vector2 current, Vector2 target)
    {
        return Vector2.Distance(current, target) <= model.Config.arriveThreshold;
    }

    static void ClearSharedItem(ZoneData zone)
    {
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
        zone.SharedFoodVisual = FoodVisual.None;
        zone.SharedItemId = "";
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
