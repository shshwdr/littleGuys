using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomerEffectService
{
    readonly GameModel model;
    readonly WorldLayout layout;

    public event Action<CustomerData, WorkerData> EatMinionPerformanceRequested;

    public CustomerEffectService(GameModel model, WorldLayout layout)
    {
        this.model = model;
        this.layout = layout;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        foreach (var customer in model.Customers.ToList())
        {
            if (customer.IsServed || customer.IsInSilhouettePerformance || string.IsNullOrEmpty(customer.Effect))
                continue;

            if (customer.Effect != "eatMinion")
                continue;

            if (customer.IsEffectActive || customer.EffectValue <= 0)
                continue;

            customer.EffectTimer += dt;
            customer.EffectProgress.Value = Mathf.Clamp01(customer.EffectTimer / customer.EffectValue);

            if (customer.EffectTimer < customer.EffectValue)
                continue;

            customer.EffectTimer = 0f;
            customer.EffectProgress.Value = 0f;
            TryEatMinion(customer);
        }
    }

    void TryEatMinion(CustomerData customer)
    {
        var eligible = GetEligibleWorkers();
        if (eligible.Count == 0)
            return;

        var worker = eligible[UnityEngine.Random.Range(0, eligible.Count)];
        customer.IsEffectActive = true;
        RemoveWorkerFromModel(worker);
        worker.State = WorkerState.BeingEaten;
        worker.SacrificeTarget = customer;
        EatMinionPerformanceRequested?.Invoke(customer, worker);
        
    }

    public void OnEatAnimationComplete(CustomerData customer)
    {
        if (customer == null)
            return;

        customer.IsEffectActive = false;
        customer.EffectTimer = 0f;
        customer.EffectProgress.Value = 0f;
    }

    List<WorkerData> GetEligibleWorkers()
    {
        var result = new List<WorkerData>();
        foreach (var worker in model.Workers)
        {
            if (!IsEligibleForEatMinion(worker))
                continue;

            result.Add(worker);
        }

        return result;
    }

    bool IsEligibleForEatMinion(WorkerData worker)
    {
        if (worker.IsSmall || worker.RemainingGrowTime > 0f)
            return false;

        if (worker.IsSacrificing || worker.State == WorkerState.BeingEaten)
            return false;

        if (worker.State == WorkerState.WalkingToZone || !worker.HasArrivedAtZone)
            return false;

        if (worker.AssignedZone == ZoneType.Idle)
            return worker.State == WorkerState.Standing;

        var zone = model.GetZone(worker.AssignedZone);

        if (worker.AssignedZone == ZoneType.Splitter && zone.Phase != ZonePhase.Working)
            return worker.State == WorkerState.InZoneSync || worker.State == WorkerState.Standing;

        if (zone.Phase != ZonePhase.Working)
            return false;

        if (zone.ConsumeWorkerAsInput && worker.AssignedZone == ZoneType.Chop)
        {
            var chopWorkers = model.Workers.Where(w => w.AssignedZone == ZoneType.Chop).ToList();
            if (chopWorkers.Count > 0 && chopWorkers[0] == worker)
                return false;
        }

        return worker.State == WorkerState.InZoneSync;
    }

    void RemoveWorkerFromModel(WorkerData worker)
    {
        if (worker.AssignedZone != ZoneType.Idle)
            model.GetZone(worker.AssignedZone).WorkerCount.Value--;

        model.Workers.Remove(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        model.NotifyWorkerAssignmentChanged();
    }

    public void FinalizeEatenWorker(WorkerData worker)
    {
        if (worker == null)
            return;

        var customer = worker.SacrificeTarget;
        worker.SacrificeTarget = null;
        OnEatAnimationComplete(customer);
    }
}
