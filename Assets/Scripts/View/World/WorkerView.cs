using DG.Tweening;
using UnityEngine;

public class WorkerView : MonoBehaviour
{
    WorkerData worker;
    GameModel model;
    WorldLayout layout;
    GameConfigData config;
    Transform bodyTransform;
    float baseScale = 0.4f;
    Tween walkBounceTween;
    Tween punchTween;
    Tween sacrificeTween;
    bool isWorkingAnim;
    bool sacrificeAnimStarted;

    public event System.Action<WorkerData> SacrificeAnimationComplete;

    public void Setup(WorkerData data, GameModel gameModel, WorldLayout worldLayout, GameConfigData gameConfig)
    {
        worker = data;
        model = gameModel;
        layout = worldLayout;
        config = gameConfig;

        var innerGo = new GameObject("Inner");
        innerGo.transform.SetParent(transform, false);
        bodyTransform = innerGo.transform;

        ColorSpriteFactory.CreateSprite(
            "Body",
            bodyTransform,
            ResourceSpriteLoader.GetMinion(),
            Color.white,
            Vector2.one);
        transform.position = worker.Position;
        RefreshScale();
    }

    void Update()
    {
        if (worker == null)
            return;

        if (worker.State == WorkerState.Sacrificing)
        {
            UpdateSacrificeAnimation();
            return;
        }

        sacrificeAnimStarted = false;
        UpdateMovement();
        UpdateWorkingAnimation();
        bodyTransform.rotation = Quaternion.identity;
        RefreshScale();
    }

    void UpdateMovement()
    {
        if (worker.PositionLocked || IsWorkerOperating())
        {
            StopWalkBounce();
            return;
        }

        Vector2 target = GetMoveTarget();
        float speed = config.workerMoveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position,
            new Vector3(target.x, target.y, 0f),
            speed);

        UpdateWalkBounce(ShouldWalkBounce(target));
    }

    bool ShouldWalkBounce(Vector2 target)
    {
        if (worker.State == WorkerState.WalkingToZone)
            return true;

        if (worker.SacrificeTarget != null)
            return true;

        if (worker.AssignedZone != ZoneType.Idle)
        {
            var zone = model.GetZone(worker.AssignedZone);
            if (zone.Phase == ZonePhase.GoingToSource
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

    void UpdateWalkBounce(bool moving)
    {
        if (!moving)
        {
            StopWalkBounce();
            return;
        }

        if (walkBounceTween != null && walkBounceTween.IsActive())
            return;

        bodyTransform.localPosition = Vector3.zero;
        walkBounceTween = bodyTransform
            .DOLocalMoveY(0.08f, 0.22f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    void StopWalkBounce()
    {
        walkBounceTween?.Kill();
        walkBounceTween = null;
        bodyTransform.localPosition = Vector3.zero;
    }

    void UpdateWorkingAnimation()
    {
        bool shouldWork = IsWorkerOperating();
        if (shouldWork == isWorkingAnim)
            return;

        isWorkingAnim = shouldWork;
        punchTween?.Kill();

        if (!shouldWork)
        {
            bodyTransform.localScale = GetBaseScaleVector();
            return;
        }

        StopWalkBounce();
        bodyTransform.localScale = GetBaseScaleVector();
        punchTween = bodyTransform
            .DOPunchScale(Vector3.one * 0.12f, 0.45f, 2, 0.4f)
            .SetLoops(-1, LoopType.Restart);
    }

    void UpdateSacrificeAnimation()
    {
        if (sacrificeAnimStarted)
            return;

        sacrificeAnimStarted = true;
        StopWalkBounce();
        punchTween?.Kill();

        Vector2 customerPos = worker.Position;
        if (worker.SacrificeTarget != null)
        {
            int index = model.Customers.IndexOf(worker.SacrificeTarget);
            if (index >= 0)
            {
                var customer = worker.SacrificeTarget;
                int slotIndex = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : index;
                customerPos = layout.GetCustomerSacrificePosition(slotIndex, model.Customers.Count);
            }
        }

        var target = new Vector3(customerPos.x, customerPos.y + 0.3f, 0f);
        sacrificeTween = DOTween.Sequence()
            .Append(transform.DOMove(target, 0.5f).SetEase(Ease.Linear))
            .Join(bodyTransform.DOLocalMoveY(0.12f, 0.12f).SetLoops(4, LoopType.Yoyo).SetEase(Ease.InOutSine))
            .Append(bodyTransform.DOScale(Vector3.zero, 0.3f))
            .OnComplete(() => SacrificeAnimationComplete?.Invoke(worker));
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
        float sizeScale = worker.IsSmall ? config.smallWorkerScale : 1f;
        float s = baseScale * sizeScale;
        return new Vector3(s, s, 1f);
    }

    void RefreshScale()
    {
        if (isWorkingAnim || worker.State == WorkerState.Sacrificing)
            return;

        bodyTransform.localScale = GetBaseScaleVector();
    }

    void OnDestroy()
    {
        walkBounceTween?.Kill();
        punchTween?.Kill();
        sacrificeTween?.Kill();
    }
}
