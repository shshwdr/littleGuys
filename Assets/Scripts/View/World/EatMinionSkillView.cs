using System;
using DG.Tweening;
using UnityEngine;

public class EatMinionSkillView : MonoBehaviour
{
    static GameObject skillPrefab;

    Transform skillBody;
    Tween activeTween;

    public static EatMinionSkillView Play(
        Vector3 customerPos,
        Vector3 workerPos,
        WorkerView workerView,
        Vector3 workerFlyTarget,
        Action onComplete)
    {
        if (skillPrefab == null)
            skillPrefab = Resources.Load<GameObject>("skill/eatMinion");
        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Costumers/sfx_boss_skill");

        GameObject go;
        if (skillPrefab != null)
            go = Instantiate(skillPrefab);
        else
            go = new GameObject("EatMinionSkill");

        go.transform.position = customerPos;
        var view = go.GetComponent<EatMinionSkillView>();
        if (view == null)
            view = go.AddComponent<EatMinionSkillView>();

        view.Run(customerPos, workerPos, workerView, workerFlyTarget, onComplete);             
        return view;
    }

    void Awake()
    {
        skillBody = transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    void Run(Vector3 customerPos, Vector3 workerPos, WorkerView workerView, Vector3 workerFlyTarget, Action onComplete)
    {
        activeTween?.Kill();
        var body = skillBody != null ? skillBody : transform;
        const float workerFlyDuration = 0.5f;

        activeTween = DOTween.Sequence()
            .Append(transform.DOMove(workerPos, 0.35f).SetEase(Ease.OutQuad))
            .Append(body.DOPunchScale(Vector3.one * 0.35f, 0.25f, 4, 0.5f))
            .AppendCallback(() =>
            {
                if (workerView != null)
                    workerView.PlayKnockedToCustomer(workerFlyTarget, null);
            })
            .AppendInterval(workerFlyDuration)
            .Append(transform.DOMove(customerPos, 0.35f).SetEase(Ease.InQuad))
            .AppendCallback(() => onComplete?.Invoke())
            .OnComplete(() => Destroy(gameObject));
    }

    void OnDestroy()
    {
        activeTween?.Kill();
    }
}
