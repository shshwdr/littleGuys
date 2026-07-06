using System;
using System.Linq;
using UnityEngine;

public class CustomerSacrificeService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly WorkerAssignService assignService;

    public event Action<WorkerData> WorkerRemoved;
    public event Action<WorkerData> SacrificeReadyForPickup;

    public CustomerSacrificeService(GameModel model, WorldLayout layout, WorkerAssignService assignService)
    {
        this.model = model;
        this.layout = layout;
        this.assignService = assignService;
    }

    public int GetAssignedCount(CustomerData customer)
    {
        if (customer == null)
            return 0;

        return model.Workers.Count(w => w.SacrificeTarget == customer);
    }

    public bool CanAssign(CustomerData customer)
    {
        if (!CanSacrificeButton(customer))
            return false;

        if (customer.IsInSilhouettePerformance)
            return false;

        return true;
    }

    public bool CanSacrificeButton(CustomerData customer)
    {
        if (customer == null || customer.IsServed)
            return false;

        if (GetAssignedCount(customer) > 0)
            return false;

        return model.Workers.Any(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
    }

    public bool CanRecall(CustomerData customer)
    {
        if (customer == null || customer.IsServed || customer.IsInSilhouettePerformance)
            return false;

        return model.Workers.Any(w =>
            w.SacrificeTarget == customer && w.State != WorkerState.Sacrificing);
    }

    public bool TryAssignWorker(CustomerData customer)
    {
        if (!CanSacrificeButton(customer))
            return false;

        var worker = model.Workers.First(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
        worker.SacrificeTarget = customer;
        worker.HasArrivedAtZone = false;
        worker.State = WorkerState.WalkingToZone;
        RefreshSacrificeQueue();
        model.NotifyWorkerAssignmentChanged();
        return true;
    }

    public bool TryRecallWorker(CustomerData customer)
    {
        if (!CanRecall(customer))
            return false;

        var worker = model.Workers.First(w =>
            w.SacrificeTarget == customer && w.State != WorkerState.Sacrificing);

        worker.SacrificeTarget = null;
        worker.SacrificeQueueIndex = -1;
        RefreshSacrificeQueue();
        assignService.AssignWorkerToZone(worker, ZoneType.Idle);
        return true;
    }

    public void Tick(float dt)
    {
        foreach (var worker in model.Workers.ToList())
        {
            if (worker.SacrificeTarget == null || worker.State == WorkerState.Sacrificing)
                continue;

            var customer = worker.SacrificeTarget;
            if (customer.IsServed || !model.Customers.Contains(customer))
            {
                CancelSacrifice(worker);
                continue;
            }

            Vector2 target = GetSacrificeTargetPosition(worker.SacrificeQueueIndex);
            worker.TargetPosition = target;
            worker.Position = Vector2.MoveTowards(
                worker.Position,
                target,
                model.Config.workerMoveSpeed * dt);

            worker.State = WorkerState.WalkingToZone;
            worker.HasArrivedAtZone =
                Vector2.Distance(worker.Position, target) <= model.Config.arriveThreshold;

            if (!worker.HasArrivedAtZone)
                continue;

            if (worker.SacrificeQueueIndex != 0)
                continue;

            worker.State = WorkerState.Sacrificing;
            worker.Position = target;
            SacrificeReadyForPickup?.Invoke(worker);
        }
    }

    public void RefreshSacrificeQueue()
    {
        var queued = model.Workers
            .Where(w => w.SacrificeTarget != null
                        && (w.State == WorkerState.WalkingToZone || w.State == WorkerState.Sacrificing))
            .OrderBy(w => w.Id)
            .ToList();

        for (int i = 0; i < queued.Count; i++)
            queued[i].SacrificeQueueIndex = i;

        foreach (var worker in model.Workers)
        {
            if (worker.SacrificeTarget == null)
                worker.SacrificeQueueIndex = -1;
        }
    }

    Vector2 GetSacrificeTargetPosition(int queueIndex)
    {
        Vector2 basePos = layout.GetSacrificeQueueBasePosition();
        return layout.GetSacrificeQueuePosition(basePos, queueIndex);
    }

    public void FinalizeSacrifice(WorkerData worker)
    {
        if (worker == null)
            return;

        var customer = worker.SacrificeTarget;
        if (customer != null && !customer.IsServed)
        {
            float restore = customer.MaxPatience * model.Config.customerSacrificePatienceRestore;
            customer.Patience.Value = Mathf.Min(customer.MaxPatience, customer.Patience.Value + restore);
        }

        worker.SacrificeTarget = null;
        worker.SacrificeQueueIndex = -1;
        if (worker.AssignedZone != ZoneType.Idle)
            model.GetZone(worker.AssignedZone).WorkerCount.Value--;

        model.Workers.Remove(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        RefreshSacrificeQueue();
        model.NotifyWorkerAssignmentChanged();
        WorkerRemoved?.Invoke(worker);
    }

    void CancelSacrifice(WorkerData worker)
    {
        worker.SacrificeTarget = null;
        worker.SacrificeQueueIndex = -1;
        RefreshSacrificeQueue();
        assignService.AssignWorkerToZone(worker, ZoneType.Idle);
    }
}
