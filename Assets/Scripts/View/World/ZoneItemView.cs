using UnityEngine;

public class ZoneItemView : MonoBehaviour
{
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

        EnsureRenderer();
        if (food == null)
            food = GetComponentInChildren<Food>();
        itemTransform = itemRenderer.transform;
        itemRenderer.enabled = false;
    }

    void EnsureRenderer()
    {
        if (itemRenderer != null)
            return;

        food = Food.Spawn(transform, "SharedItem");
        itemRenderer = food.GetRenderer();
        if (itemRenderer == null)
            itemRenderer = food.GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (!isSetup || model == null || itemRenderer == null || externallyControlled)
            return;

        var zone = model.GetZone(zoneType);
        // While the machine is working the ingredient is "inside" the machine:
        // hide it until the step completes and the output pile shows the result.
        bool show = zone.HasSharedItem
            && zone.SharedItemStage != FoodStage.None
            && zone.Phase != ZonePhase.Working;
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
