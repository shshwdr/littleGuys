using System.Linq;
using UnityEngine;

public class ZoneBufferPileView : MonoBehaviour
{
    SpriteRenderer pileRenderer;
    ZoneType sourceZone;
    GameModel model;
    FoodStage stage;
    FoodVisual visualFilter = FoodVisual.None;

    public void Setup(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, Vector2 position, FoodStage pileStage, FoodVisual pileVisual)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        stage = pileStage;
        visualFilter = pileVisual;

        float size = config.foodSpriteSize * 1.15f;
        pileRenderer = ColorSpriteFactory.CreateSprite(
            "BufferPile",
            transform,
            ResourceSpriteLoader.GetFoodVisual(pileVisual),
            Color.white,
            new Vector2(size, size));
        transform.position = new Vector3(position.x, position.y, -0.06f);
        pileRenderer.enabled = false;
    }

    void Update()
    {
        if (model == null)
            return;

        var zone = model.GetZone(sourceZone);
        var item = zone.OutputItems.FirstOrDefault(output =>
            output.Stage == stage &&
            (visualFilter == FoodVisual.None || output.Visual == visualFilter));

        bool hasItem = item != null;
        pileRenderer.enabled = hasItem;

        if (!hasItem)
            return;

        pileRenderer.sprite = ResourceSpriteLoader.GetFoodVisual(item.Visual);
        pileRenderer.color = item.Occupied ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
    }
}
