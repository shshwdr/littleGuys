using UnityEngine;

public class ZoneBufferPileView : MonoBehaviour
{
    SpriteRenderer pileRenderer;
    ZoneType sourceZone;
    GameModel model;
    FoodStage stage;

    public void Setup(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, Vector2 position, FoodStage pileStage)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        stage = pileStage;

        float size = config.foodSpriteSize * 1.15f;
        pileRenderer = ColorSpriteFactory.CreateSprite(
            "BufferPile",
            transform,
            ResourceSpriteLoader.GetFood(),
            FoodVisualColors.Get(stage),
            new Vector2(size, size));
        transform.position = new Vector3(position.x, position.y, -0.06f);
        pileRenderer.enabled = false;
    }

    void Update()
    {
        if (model == null)
            return;

        var zone = model.GetZone(sourceZone);
        bool carryingAway = zone.HasSharedItem && zone.Phase != ZonePhase.Working;
        pileRenderer.enabled = zone.OutputBuffer > 0 && !carryingAway;
        pileRenderer.color = FoodVisualColors.Get(stage);
    }
}
