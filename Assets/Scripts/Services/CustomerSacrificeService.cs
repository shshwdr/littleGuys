using System;
using System.Linq;
using UnityEngine;

public class CustomerSacrificeService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly WorkerAssignService assignService;

    public event Action<WorkerData> WorkerRemoved;

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
        if (customer == null || customer.IsServed)
            return false;

        return model.Workers.Any(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
    }

    public bool CanRecall(CustomerData customer)
    {
        if (customer == null || customer.IsServed)
            return false;

        return model.Workers.Any(w =>
            w.SacrificeTarget == customer && w.State != WorkerState.Sacrificing);
    }

    public bool TryAssignWorker(CustomerData customer)
    {
        if (!CanAssign(customer))
            return false;

        var worker = model.Workers.First(w => w.AssignedZone == ZoneType.Idle && w.CanAssign);
        worker.SacrificeTarget = customer;
        worker.HasArrivedAtZone = false;
        worker.State = WorkerState.WalkingToZone;
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

            int index = model.Customers.IndexOf(customer);
            int slotIndex = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : index;
            Vector2 target = layout.GetCustomerSacrificePosition(slotIndex, model.Customers.Count);
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

            worker.State = WorkerState.Sacrificing;
            worker.Position = target;
        }
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
        if (worker.AssignedZone != ZoneType.Idle)
            model.GetZone(worker.AssignedZone).WorkerCount.Value--;

        model.Workers.Remove(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        model.NotifyWorkerAssignmentChanged();
        WorkerRemoved?.Invoke(worker);
    }

    void CancelSacrifice(WorkerData worker)
    {
        worker.SacrificeTarget = null;
        assignService.AssignWorkerToZone(worker, ZoneType.Idle);
    }
}
