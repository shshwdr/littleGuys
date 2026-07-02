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
            case FoodStage.Plated: return new Color(0.9f, 0.2f, 0.6f);
            default: return Color.white;
        }
    }
}
