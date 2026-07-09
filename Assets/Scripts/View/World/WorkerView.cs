using DG.Tweening;
using System;
using UnityEngine;

public class WorkerView : MonoBehaviour
{
    const string MinionPrefabPath = "prefab/minion";

    WorkerData worker;
    GameModel model;
    WorldLayout layout;
    GameConfigData config;
    Transform bodyTransform;
    Minion minion;
    bool lastMoving;
    Tween sacrificeTween;
    Tween eatenTween;
    bool sacrificeAnimStarted;
    bool eatenAnimStarted;
    bool eatenKnockbackStarted;

    public event System.Action<WorkerData> SacrificeAnimationComplete;
    public event System.Action<WorkerData> EatenAnimationComplete;

    public Vector3 WorldPosition => transform.position;

    public void Setup(WorkerData data, GameModel gameModel, WorldLayout worldLayout, GameConfigData gameConfig)
    {
        worker = data;
        model = gameModel;
        layout = worldLayout;
        config = gameConfig;

        var innerGo = new GameObject("Inner");
        innerGo.transform.SetParent(transform, false);
        bodyTransform = innerGo.transform;

        if (!TryCreateMinionFromPrefab(bodyTransform))
        {
            ColorSpriteFactory.CreateSprite(
                "Body",
                bodyTransform,
                ResourceSpriteLoader.GetMinion(),
                Color.white,
                Vector2.one);
        }
        minion = bodyTransform.GetComponentInChildren<Minion>();
        transform.position = worker.Position;
        RefreshScale();
    }

    bool TryCreateMinionFromPrefab(Transform parent)
    {
        var prefab = Resources.Load<GameObject>(MinionPrefabPath);
        if (prefab == null)
            return false;

        var minionGo = Instantiate(prefab, parent, false);
        minionGo.name = "Body";
        if (minionGo.GetComponentInChildren<Minion>() == null)
            minionGo.AddComponent<Minion>();
        return true;
    }

    void Update()
    {
        if (worker == null)
            return;

        if (IsAttachedToCustomerHand())
            return;

        if (worker.State == WorkerState.Sacrificing)
        {
            transform.position = new Vector3(worker.Position.x, worker.Position.y, 0f);
            return;
        }

        if (worker.State == WorkerState.BeingEaten)
            return;

        sacrificeAnimStarted = false;
        eatenAnimStarted = false;
        eatenKnockbackStarted = false;
        UpdateMovement();
        UpdateMinionAnim();
        bodyTransform.rotation = Quaternion.identity;
        RefreshScale();
    }

    void UpdateMinionAnim()
    {
        if (minion == null)
            return;

        MinionAnimState state;
        if (IsWorkerOperating())
            state = MinionAnimState.Work;
        else if (lastMoving)
            state = MinionAnimState.Walk;
        else
            state = MinionAnimState.Idle;

        minion.SetAnimState(state);
    }

    bool IsAttachedToCustomerHand()
    {
        var hand = CustomerHand.Instance;
        if (hand == null)
            return false;

        return transform.IsChildOf(hand.GrabPoint);
    }

    void UpdateMovement()
    {
        if (worker.PositionLocked || IsWorkerOperating())
        {
            lastMoving = false;
            return;
        }

        Vector2 target = GetMoveTarget();
        float speed = config.workerMoveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position,
            new Vector3(target.x, target.y, 0f),
            speed);

        lastMoving = IsMoving(target);
    }

    bool IsMoving(Vector2 target)
    {
        if (worker.State == WorkerState.WalkingToZone)
            return true;

        if (worker.SacrificeTarget != null)
            return true;

        if (worker.AssignedZone != ZoneType.Idle)
        {
            var zone = model.GetZone(worker.AssignedZone);
            if (zone.Phase == ZonePhase.AwaitingWorkers
                || zone.Phase == ZonePhase.GoingToSource
                || zone.Phase == ZonePhase.Returning
                || zone.Phase == ZonePhase.Delivering)
                return true;
        }

        return Vector2.Distance(transform.position, new Vector3(target.x, target.y, 0f)) > 0.03f;
    }

    Vector2 GetMoveTarget()
    {
        if (worker.State == WorkerState.WalkingToZone)
            return worker.TargetPosition;

        return worker.Position;
    }

    void UpdateSacrificeAnimation()
    {
        if (sacrificeAnimStarted)
            return;

        sacrificeAnimStarted = true;

        Vector2 customerPos = worker.Position;
        if (worker.SacrificeTarget != null)
        {
            int index = model.Customers.IndexOf(worker.SacrificeTarget);
            if (index >= 0)
            {
                var customer = worker.SacrificeTarget;
                int slotIndex = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : index;
                customerPos = layout.GetCustomerPosition(slotIndex, model.Customers.Count);
            }
        }

        var target = new Vector3(customerPos.x, customerPos.y + 0.3f, 0f);
        sacrificeTween = DOTween.Sequence()
            .Append(transform.DOMove(target, 0.5f).SetEase(Ease.Linear))
            .Join(bodyTransform.DOLocalMoveY(0.12f, 0.12f).SetLoops(4, LoopType.Yoyo).SetEase(Ease.InOutSine))
            .Append(bodyTransform.DOScale(Vector3.zero, 0.3f))
            .OnComplete(() => SacrificeAnimationComplete?.Invoke(worker));
    }

    public void PlayKnockedToCustomer(Vector3 customerPos, Action onComplete)
    {
        if (eatenKnockbackStarted)
            return;

        eatenKnockbackStarted = true;
        eatenAnimStarted = true;

        var target = new Vector3(customerPos.x, customerPos.y + 0.3f, 0f);
        eatenTween = DOTween.Sequence()
            .Append(transform.DOMove(target, 0.5f).SetEase(Ease.InQuad))
            .Join(bodyTransform.DOLocalMoveY(0.12f, 0.12f).SetLoops(4, LoopType.Yoyo).SetEase(Ease.InOutSine))
            .Append(bodyTransform.DOScale(Vector3.zero, 0.3f))
            .OnComplete(() =>
            {
                onComplete?.Invoke();
                EatenAnimationComplete?.Invoke(worker);
            });
    }

    bool IsWorkerOperating()
    {
        if (worker.AssignedZone == ZoneType.Idle)
            return false;

        var zone = model.GetZone(worker.AssignedZone);
        return zone.Phase == ZonePhase.Working && worker.HasArrivedAtZone;
    }

    Vector3 GetBaseScaleVector()
    {
        if (config == null || worker == null)
            return Vector3.one;

        float smallScale = Mathf.Max(0.05f, config.smallWorkerScale);
        if (worker.IsSmall || worker.RemainingGrowTime > 0f)
        {
            float growDuration = Mathf.Max(0.01f, config.smallWorkerGrowTime);
            float t = 1f - Mathf.Clamp01(worker.RemainingGrowTime / growDuration);
            float scale = Mathf.Lerp(smallScale, 1f, t);
            return Vector3.one * scale;
        }

        return Vector3.one;
    }

    void RefreshScale()
    {
        if (worker.State == WorkerState.Sacrificing || worker.State == WorkerState.BeingEaten)
            return;

        bodyTransform.localScale = GetBaseScaleVector();
    }

    void OnDestroy()
    {
        sacrificeTween?.Kill();
        eatenTween?.Kill();
    }
}
