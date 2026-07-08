using UnityEngine;

public class Food : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;

    void Awake()
    {
        EnsureRenderer();
    }

    public void SetVisual(FoodVisual visual, FoodStage stage)
    {
        EnsureRenderer();
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = LoadFoodSprite(visual);
        spriteRenderer.color = FoodVisualColors.GetTint(visual, stage);
    }

    public SpriteRenderer GetRenderer()
    {
        EnsureRenderer();
        return spriteRenderer;
    }

    void EnsureRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    static Sprite LoadFoodSprite(FoodVisual visual)
    {
        string path = GetResourcePath(visual);
        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        var fallback = Resources.Load<Sprite>("food");
        if (fallback != null)
            return fallback;

        return ResourceSpriteLoader.GetFoodVisual(visual);
    }

    static string GetResourcePath(FoodVisual visual)
    {
        switch (visual)
        {
            case FoodVisual.Veg:
                return "food/veg";
            case FoodVisual.Meat:
                return "food/meat";
            case FoodVisual.Minion:
                return "food/minion";
            default:
                return "food";
        }
    }
}
