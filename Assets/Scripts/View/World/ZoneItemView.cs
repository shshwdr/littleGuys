using UnityEngine;

public class ZoneItemView : MonoBehaviour
{
    const string FoodPrefabPath = "prefab/food";

    [SerializeField] SpriteRenderer itemRenderer;
    Food food;

    Transform itemTransform;
    Transform carryParent;
    ZoneType zoneType;
    GameModel model;
    bool isSetup;
    bool externallyControlled;

    public Transform CarryTransform => itemRenderer != null ? itemRenderer.transform : transform;

    public void SetExternallyControlled(bool value)
    {
        externallyControlled = value;
        if (value && itemRenderer != null)
            itemRenderer.enabled = true;
    }

    public void ResetAfterCarry()
    {
        externallyControlled = false;
        if (carryParent != null)
            transform.SetParent(carryParent, true);

        if (itemRenderer != null)
            itemRenderer.enabled = false;
    }

    public void Setup(ZoneType type, GameModel gameModel, GameConfigData config)
    {
        zoneType = type;
        model = gameModel;
        isSetup = true;
        carryParent = transform.parent;

        float size = config.foodSpriteSize * 1.15f;
        EnsureRenderer(config, size);
        if (food == null)
            food = GetComponentInChildren<Food>();
        itemTransform = itemRenderer.transform;
        itemRenderer.enabled = false;
    }

    void EnsureRenderer(GameConfigData config, float size)
    {
        if (itemRenderer != null)
            return;

        if (TryCreateFoodFromPrefab())
            return;

        itemRenderer = GetComponentInChildren<SpriteRenderer>();
        if (itemRenderer != null)
            return;

        itemRenderer = ColorSpriteFactory.CreateSprite(
            "SharedItem",
            transform,
            ResourceSpriteLoader.GetFood(),
            Color.white,
            new Vector2(size, size));
    }

    bool TryCreateFoodFromPrefab()
    {
        var prefab = Resources.Load<GameObject>(FoodPrefabPath);
        if (prefab == null)
            return false;

        var foodGo = Instantiate(prefab, transform, false);
        foodGo.name = "SharedItem";
        food = foodGo.GetComponentInChildren<Food>();
        if (food == null)
            food = foodGo.AddComponent<Food>();

        itemRenderer = food.GetRenderer();
        if (itemRenderer == null)
            itemRenderer = foodGo.GetComponentInChildren<SpriteRenderer>();
        return itemRenderer != null;
    }

    void Update()
    {
        if (!isSetup || model == null || itemRenderer == null || externallyControlled)
            return;

        var zone = model.GetZone(zoneType);
        bool show = zone.HasSharedItem && zone.SharedItemStage != FoodStage.None;
        itemRenderer.enabled = show;
        if (!show)
            return;

        transform.position = new Vector3(zone.SharedItemPosition.x, zone.SharedItemPosition.y, -0.08f);
        if (food != null)
            food.SetVisual(zone.SharedFoodVisual, zone.SharedItemStage);
        else
        {
            itemRenderer.sprite = ResourceSpriteLoader.GetFoodVisual(zone.SharedFoodVisual);
            itemRenderer.color = FoodVisualColors.GetTint(zone.SharedFoodVisual, zone.SharedItemStage);
        }
        itemTransform.rotation = Quaternion.Euler(0f, 0f, zone.WorkRotation);
    }
}
