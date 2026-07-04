using System.Linq;
using UnityEngine;

public class ZoneBufferPileView : MonoBehaviour
{
    [SerializeField] ZoneType sourceZone;
    [SerializeField] FoodStage pileStage;
    [SerializeField] FoodVisual pileVisual = FoodVisual.None;

    SpriteRenderer pileRenderer;
    GameModel model;
    bool isSetup;

    public void Setup(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, Vector2 position, FoodStage stage, FoodVisual visual)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        pileStage = stage;
        pileVisual = visual;
        isSetup = true;

        float size = config.foodSpriteSize * 1.15f;
        EnsureRenderer(config, visual, size);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        pileRenderer.enabled = false;
    }

    public void BindExisting(ZoneType upstreamZone, GameModel gameModel, GameConfigData config)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        isSetup = true;

        float size = config.foodSpriteSize * 1.15f;
        EnsureRenderer(config, pileVisual, size);
        if (pileRenderer == null)
            return;
    }

    void EnsureRenderer(GameConfigData config, FoodVisual visual, float size)
    {
        if (pileRenderer != null)
            return;

        pileRenderer = GetComponentInChildren<SpriteRenderer>();
        if (pileRenderer != null)
            return;

        pileRenderer = ColorSpriteFactory.CreateSprite(
            "BufferPile",
            transform,
            ResourceSpriteLoader.GetFoodVisual(visual),
            Color.white,
            new Vector2(size, size));
    }

    void Update()
    {
        if (!isSetup || model == null || pileRenderer == null)
            return;

        var zone = model.GetZone(sourceZone);
        var item = zone.OutputItems.FirstOrDefault(output =>
            output.Stage == pileStage &&
            (pileVisual == FoodVisual.None || output.Visual == pileVisual));

        bool hasItem = item != null;
        pileRenderer.enabled = hasItem;

        if (!hasItem)
            return;

        pileRenderer.sprite = ResourceSpriteLoader.GetFoodVisual(item.Visual);
        pileRenderer.color = item.Occupied ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
    }
}
