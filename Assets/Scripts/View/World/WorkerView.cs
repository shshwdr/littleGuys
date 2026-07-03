using UnityEngine;

public class WorkerView : MonoBehaviour
{
    WorkerData worker;
    GameConfigData config;
    Transform bodyTransform;
    float baseScale = 0.4f;

    public void Setup(WorkerData data, GameConfigData gameConfig)
    {
        worker = data;
        config = gameConfig;

        var renderer = ColorSpriteFactory.CreateSprite(
            "Body",
            transform,
            ResourceSpriteLoader.GetMinion(),
            Color.white,
            Vector2.one);
        bodyTransform = renderer.transform;
        transform.position = worker.Position;
        RefreshScale();
    }

    void Update()
    {
        if (worker == null)
            return;

        transform.position = new Vector3(worker.Position.x, worker.Position.y, 0f);
        bodyTransform.rotation = Quaternion.Euler(0f, 0f, worker.WorkRotation);
        RefreshScale();
    }

    void RefreshScale()
    {
        float sizeScale = worker.IsSmall ? config.smallWorkerScale : 1f;
        bodyTransform.localScale = new Vector3(baseScale * sizeScale, baseScale * sizeScale, 1f);
    }
}
