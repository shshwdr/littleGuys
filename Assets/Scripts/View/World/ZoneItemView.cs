using System.Collections.Generic;
using UnityEngine;

public class ZoneItemView : MonoBehaviour
{
    [SerializeField] SpriteRenderer itemRenderer;
    Food food;
    readonly List<Food> depositedFoods = new List<Food>();

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
        UpdatePrimaryItem(zone);
        UpdateDepositedItems(zone);
    }

    void UpdatePrimaryItem(ZoneData zone)
    {
        // 加工(Working)时原料在机器"内部"，隐藏；其余阶段（搬运、放下、等待取餐）保持显示。
        bool show = zone.HasSharedItem
            && zone.SharedItemStage != FoodStage.None
            && zone.Phase != ZonePhase.Working;
        itemRenderer.enabled = show;
        if (!show)
            return;

        transform.position = new Vector3(zone.SharedItemPosition.x, zone.SharedItemPosition.y, -0.08f);
        if (food != null)
            food.SetVisual(zone.SharedItemId, zone.SharedFoodVisual, zone.SharedItemStage);
        else
        {
            var byId = ResourceSpriteLoader.GetFoodById(zone.SharedItemId);
            itemRenderer.sprite = byId != null ? byId : ResourceSpriteLoader.GetFoodVisual(zone.SharedFoodVisual);
            itemRenderer.color = byId != null ? Color.white : FoodVisualColors.GetTint(zone.SharedFoodVisual, zone.SharedItemStage);
        }
        itemTransform.rotation = Quaternion.Euler(0f, 0f, zone.WorkRotation);
    }

    // 已放下的原料保持显示，直到开始加工(Working)时才隐藏。
    void UpdateDepositedItems(ZoneData zone)
    {
        var collected = zone.CollectedInputs;
        bool showDeposited = zone.Phase != ZonePhase.Working;
        int visible = showDeposited && collected != null ? collected.Count : 0;
        EnsureDepositedFoods(visible);

        for (int i = 0; i < depositedFoods.Count; i++)
        {
            var f = depositedFoods[i];
            if (f == null)
                continue;

            bool active = i < visible;
            f.gameObject.SetActive(active);
            if (!active)
                continue;

            var ci = collected[i];
            f.transform.position = new Vector3(ci.Position.x, ci.Position.y, -0.08f);
            f.SetVisual(ci.Id, ci.Visual, ci.Stage);
        }
    }

    void EnsureDepositedFoods(int count)
    {
        while (depositedFoods.Count < count)
        {
            var f = Food.Spawn(transform, "Deposited_" + depositedFoods.Count);
            depositedFoods.Add(f);
        }
    }
}
