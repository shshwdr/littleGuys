using UnityEngine;

public class ZoneItemView : MonoBehaviour
{
    SpriteRenderer itemRenderer;
    ZoneType zoneType;
    GameModel model;

    public void Setup(ZoneType type, GameModel gameModel, GameConfigData config)
    {
        zoneType = type;
        model = gameModel;

        float size = config.foodSpriteSize * 1.15f;
        itemRenderer = ColorSpriteFactory.CreateSprite(
            "SharedItem",
            transform,
            ResourceSpriteLoader.GetFood(),
            Color.white,
            new Vector2(size, size));
        itemRenderer.enabled = false;
    }

    void Update()
    {
        if (model == null)
            return;

        var zone = model.GetZone(zoneType);
        bool show = zone.HasSharedItem && zone.SharedItemStage != FoodStage.None;
        itemRenderer.enabled = show;
        if (!show)
            return;

        transform.position = new Vector3(zone.SharedItemPosition.x, zone.SharedItemPosition.y, -0.08f);
        itemRenderer.color = FoodVisualColors.Get(zone.SharedItemStage);
    }
}
