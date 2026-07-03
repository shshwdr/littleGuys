using UnityEngine;

public static class FoodVisualColors
{
    public static Color Get(FoodStage stage)
    {
        switch (stage)
        {
            case FoodStage.Raw: return new Color(0.2f, 0.8f, 0.2f);
            case FoodStage.Chopped: return new Color(0.9f, 0.9f, 0.2f);
            case FoodStage.Cooked: return new Color(0.9f, 0.5f, 0.1f);
            case FoodStage.Fried: return new Color(0.85f, 0.35f, 0.1f);
            case FoodStage.Plated: return new Color(0.9f, 0.2f, 0.6f);
            default: return Color.white;
        }
    }

    public static Color GetTint(FoodVisual visual, FoodStage stage)
    {
        switch (visual)
        {
            case FoodVisual.Veg:
            case FoodVisual.Meat:
            case FoodVisual.Minion:
                return Color.white;
            default:
                return Get(stage);
        }
    }
}
