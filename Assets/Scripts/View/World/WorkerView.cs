using UnityEngine;

public class WorkerView : MonoBehaviour
{
    WorkerData worker;
    SpriteRenderer bodyRenderer;

    public void Setup(WorkerData data, GameConfigData config)
    {
        worker = data;
        bodyRenderer = ColorSpriteFactory.CreateSprite(
            "Body",
            transform,
            ResourceSpriteLoader.GetMinion(),
            Color.white,
            new Vector2(0.4f, 0.4f));
        transform.position = worker.Position;
    }

    void Update()
    {
        if (worker == null)
            return;

        transform.position = new Vector3(worker.Position.x, worker.Position.y, 0f);
    }
}
