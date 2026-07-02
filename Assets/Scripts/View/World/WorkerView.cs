using UnityEngine;

public class WorkerView : MonoBehaviour
{
    WorkerData worker;
    SpriteRenderer bodyRenderer;
    SpriteRenderer carryRenderer;

    public void Setup(WorkerData data, GameConfigData config)
    {
        worker = data;
        bodyRenderer = ColorSpriteFactory.CreateSquare("Body", transform, new Color(0.2f, 0.5f, 0.9f), new Vector2(0.4f, 0.4f));
        carryRenderer = ColorSpriteFactory.CreateSquare("Carry", transform, Color.white, new Vector2(0.25f, 0.25f));
        carryRenderer.transform.localPosition = new Vector3(0f, config.carryYOffset, -0.1f);
        carryRenderer.enabled = false;

        transform.position = worker.Position;
    }

    void Update()
    {
        if (worker == null)
            return;

        transform.position = new Vector3(worker.Position.x, worker.Position.y, 0f);
        UpdateCarryVisual();
    }

    void UpdateCarryVisual()
    {
        if (worker.Carrying == FoodStage.None)
        {
            carryRenderer.enabled = false;
            return;
        }

        carryRenderer.enabled = true;
        carryRenderer.color = GetFoodColor(worker.Carrying);
    }

    static Color GetFoodColor(FoodStage stage)
    {
        switch (stage)
        {
            case FoodStage.Raw: return new Color(0.2f, 0.8f, 0.2f);
            case FoodStage.Chopped: return new Color(0.9f, 0.9f, 0.2f);
            case FoodStage.Cooked: return new Color(0.9f, 0.5f, 0.1f);
            case FoodStage.Plated: return new Color(0.9f, 0.2f, 0.6f);
            default: return Color.white;
        }
    }
}
