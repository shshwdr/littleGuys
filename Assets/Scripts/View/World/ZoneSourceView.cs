using UnityEngine;

/// <summary>
/// Permanent source pile visual (e.g. raw veg at Ingredient zone output).
/// </summary>
public class ZoneSourceView : MonoBehaviour
{
    [SerializeField] FoodVisual visual = FoodVisual.Veg;
    [SerializeField] FoodStage stage = FoodStage.Raw;
    [SerializeField] Transform displayPoint;

    SpriteRenderer pileRenderer;

    public void Setup(Vector2 position, GameConfigData config)
    {
        float size = config.foodSpriteSize * 1.2f;
        EnsureRenderer(config, size);
        transform.position = new Vector3(position.x, position.y, -0.06f);
        pileRenderer.enabled = true;
        pileRenderer.sprite = ResourceSpriteLoader.GetFoodVisual(visual);
        pileRenderer.color = FoodVisualColors.GetTint(visual, stage);
    }

    public void SetupFromPoint(Transform point, GameConfigData config)
    {
        displayPoint = point;
        Setup(point != null ? point.position : transform.position, config);
    }

    void EnsureRenderer(GameConfigData config, float size)
    {
        if (pileRenderer != null)
            return;

        pileRenderer = GetComponentInChildren<SpriteRenderer>();
        if (pileRenderer != null)
            return;

        pileRenderer = ColorSpriteFactory.CreateSprite(
            "SourcePile",
            transform,
            ResourceSpriteLoader.GetFoodVisual(visual),
            Color.white,
            new Vector2(size, size));
    }
}
