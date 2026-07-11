using UnityEngine;

public class WorkerData
{
    public int Id;
    public ZoneType AssignedZone = ZoneType.Idle;
    public WorkerState State = WorkerState.Standing;
    public bool HasJoinedLift;
    public bool HasArrivedAtZone;
    public bool IsSmall;
    public bool PositionLocked;
    public float RemainingGrowTime;
    public float WorkRotation;
    public Vector2 Position;
    public Vector2 TargetPosition;
    public CustomerData SacrificeTarget;
    public int SacrificeQueueIndex = -1;

    public bool CanAssign => !IsSmall && RemainingGrowTime <= 0f && SacrificeTarget == null
        && State != WorkerState.Sacrificing && State != WorkerState.BeingEaten;
    public bool IsSacrificing => SacrificeTarget != null || State == WorkerState.Sacrificing;
}
