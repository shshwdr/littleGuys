using System;
using System.Linq;
using UnityEngine;

public class SplitterService
{
    readonly GameModel model;
    readonly WorldLayout layout;

    public event Action<WorkerData> WorkerRemoved;
    public event Action<WorkerData> WorkerAdded;

    public SplitterService(GameModel model, WorldLayout layout)
    {
        this.model = model;
        this.layout = layout;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        var zone = model.GetZone(ZoneType.Splitter);
        var workers = GetSplitterWorkers();

        if (zone.Phase != ZonePhase.Working)
        {
            foreach (var worker in workers)
                worker.PositionLocked = false;
        }

        MoveWorkersToZone(workers, dt);

        if (zone.Phase == ZonePhase.Working)
        {
            TickWorking(zone, workers, dt);
            return;
        }

        zone.Phase = ZonePhase.Idle;
        ClearMaterial(zone);

        if (workers.Count < 2)
        {
            zone.TaskProgress.Value = 0f;
            zone.StatusText.Value = "Need 2+";
            zone.WorkSpeed.Value = 0f;
            return;
        }

        if (!AllArrived(workers))
        {
            zone.StatusText.Value = "Waiting";
            zone.WorkSpeed.Value = 0f;
            return;
        }

        StartSplit(zone, workers);
    }

    void TickWorking(ZoneData zone, System.Collections.Generic.List<WorkerData> workers, float dt)
    {
        var arrivedWorkers = workers.Where(w => w.HasArrivedAtZone).ToList();
        if (arrivedWorkers.Count == 0)
        {
            zone.StatusText.Value = "Waiting";
            zone.WorkSpeed.Value = 0f;
            return;
        }

        foreach (var worker in workers)
        {
            if (worker.HasArrivedAtZone)
            {
                worker.PositionLocked = true;
                worker.State = WorkerState.InZoneSync;
                worker.WorkRotation = 0f;
            }
            else
            {
                worker.PositionLocked = false;
            }
        }

        int operatorCount = arrivedWorkers.Count;
        zone.WorkRotation += model.Config.workRotationSpeed * dt;
        zone.WorkSpeed.Value = operatorCount / zone.BaseDuration;
        zone.TaskProgress.Value += (operatorCount / zone.BaseDuration) * dt;
        zone.StatusText.Value = $"{Mathf.RoundToInt(zone.TaskProgress.Value * 100f)}%";

        Vector2 center = layout.GetWorkItemPosition(ZoneType.Splitter);
        zone.SharedItemPosition = center;
        zone.HasSharedItem = true;
        zone.SharedFoodVisual = FoodVisual.Minion;
        zone.SharedItemStage = FoodStage.Raw;

        if (zone.TaskProgress.Value < 1f)
            return;

        zone.TaskProgress.Value = 0f;
        zone.Phase = ZonePhase.Idle;
        zone.StatusText.Value = "Done";
        zone.WorkRotation = 0f;
        ClearMaterial(zone);

        foreach (var worker in workers)
            worker.PositionLocked = false;

        for (int i = 0; i < 2; i++)
            SpawnSmallWorker(i);
    }

    void StartSplit(ZoneData zone, System.Collections.Generic.List<WorkerData> workers)
    {
        var material = workers[0];
        RemoveWorker(material);

        workers = GetSplitterWorkers();

        zone.HasActiveStep = true;
        zone.BaseDuration = model.Config.splitterDuration;
        zone.Phase = ZonePhase.Working;
        zone.TaskProgress.Value = 0f;
        zone.StatusText.Value = "0%";
        zone.WorkRotation = 0f;
        zone.HasSharedItem = true;
        zone.SharedFoodVisual = FoodVisual.Minion;
        zone.SharedItemStage = FoodStage.Raw;
        zone.SharedItemPosition = layout.GetWorkItemPosition(ZoneType.Splitter);

        foreach (var worker in workers)
        {
            if (!worker.HasArrivedAtZone)
                continue;

            worker.PositionLocked = true;
            worker.WorkRotation = 0f;
        }
    }

    void MoveWorkersToZone(System.Collections.Generic.List<WorkerData> workers, float dt)
    {
        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            if (worker.PositionLocked)
                continue;

            worker.TargetPosition = layout.GetWorkerSlotPosition(ZoneType.Splitter, i, workers.Count);
            worker.Position = Vector2.MoveTowards(
                worker.Position,
                worker.TargetPosition,
                model.Config.workerMoveSpeed * dt);

            worker.HasArrivedAtZone =
                Vector2.Distance(worker.Position, worker.TargetPosition) <= model.Config.arriveThreshold;

            if (!worker.HasArrivedAtZone)
                worker.State = WorkerState.WalkingToZone;
            else if (worker.State == WorkerState.WalkingToZone)
                worker.State = WorkerState.InZoneSync;
        }
    }

    static bool AllArrived(System.Collections.Generic.List<WorkerData> workers)
    {
        return workers.Count > 0 && workers.All(w => w.HasArrivedAtZone);
    }

    System.Collections.Generic.List<WorkerData> GetSplitterWorkers()
    {
        return model.Workers.Where(w => w.AssignedZone == ZoneType.Splitter).ToList();
    }

    void SpawnSmallWorker(int slotIndex)
    {
        var worker = new WorkerData
        {
            Id = model.NextWorkerId++,
            AssignedZone = ZoneType.Idle,
            State = WorkerState.WalkingToZone,
            HasArrivedAtZone = false,
            IsSmall = true,
            RemainingGrowTime = model.Config.smallWorkerGrowTime,
            Position = layout.GetWorkerSlotPosition(ZoneType.Splitter, slotIndex, 2)
        };

        model.Workers.Add(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        model.NotifyWorkerAssignmentChanged();
        WorkerAdded?.Invoke(worker);
    }

    void RemoveWorker(WorkerData worker)
    {
        if (worker.AssignedZone != ZoneType.Idle)
            model.GetZone(worker.AssignedZone).WorkerCount.Value--;

        model.Workers.Remove(worker);
        model.GetZone(ZoneType.Idle).WorkerCount.Value =
            model.Workers.Count(w => w.AssignedZone == ZoneType.Idle);
        model.NotifyWorkerAssignmentChanged();
        WorkerRemoved?.Invoke(worker);
    }

    static void ClearMaterial(ZoneData zone)
    {
        zone.HasSharedItem = false;
        zone.SharedItemStage = FoodStage.None;
        zone.SharedFoodVisual = FoodVisual.None;
        zone.HasActiveStep = false;
    }

    public bool IsSplitting()
    {
        return model.GetZone(ZoneType.Splitter).Phase == ZonePhase.Working;
    }

    public int GetSplitterWorkerCount()
    {
        return model.Workers.Count(w => w.AssignedZone == ZoneType.Splitter);
    }
}
