using UnityEngine;

public class WorldLayout
{
    readonly GameConfigData config;

    public Vector2 CustomerAreaCenter = new Vector2(0f, 4.5f);
    public float CustomerSpacing = 1.85f;

    public Vector2 IngredientPos = new Vector2(-8f, 0f);
    public Vector2 ChopPos = new Vector2(-4.5f, 0f);
    public Vector2 CookPos = new Vector2(-1f, 0f);
    public Vector2 WokPos = new Vector2(2.5f, 0f);
    public Vector2 PlatePos = new Vector2(6f, 0f);
    public Vector2 SplitterPos = new Vector2(-3.5f, -3.5f);
    public Vector2 IdlePos = new Vector2(3.5f, -3.5f);

    public WorldLayout(GameConfigData config)
    {
        this.config = config;
    }

    public Vector2 GetZonePosition(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Ingredient: return IngredientPos;
            case ZoneType.Chop: return ChopPos;
            case ZoneType.Cook: return CookPos;
            case ZoneType.Wok: return WokPos;
            case ZoneType.Plate: return PlatePos;
            case ZoneType.Splitter: return SplitterPos;
            case ZoneType.Idle: return IdlePos;
            default: return Vector2.zero;
        }
    }

    public Vector2 GetCustomerPosition(int index, int totalCount)
    {
        int count = Mathf.Max(totalCount, 1);
        float totalWidth = (count - 1) * CustomerSpacing;
        float rightX = CustomerAreaCenter.x + totalWidth * 0.5f;
        return new Vector2(rightX - index * CustomerSpacing, CustomerAreaCenter.y);
    }

    public Vector2 GetCustomerSacrificePosition(int index, int totalCount)
    {
        return GetCustomerPosition(index, totalCount) + new Vector2(0f, -1.1f);
    }

    public Vector2 GetWorkItemPosition(ZoneType zone)
    {
        Vector2 center = GetZonePosition(zone);
        return center + new Vector2(0f, config.workItemHeight);
    }

    public Vector2 GetCarriedItemPosition(ZoneType zone)
    {
        Vector2 center = GetZonePosition(zone);
        return center + new Vector2(0f, config.carriedItemHeight);
    }

    public Vector2 ElevateCarriedItem(Vector2 basePosition)
    {
        return basePosition + new Vector2(0f, config.carriedItemHeight * 0.35f);
    }

    public Vector2 GetWorkerSlotPosition(ZoneType zone, int index, int totalInZone)
    {
        Vector2 center = GetZonePosition(zone);
        if (totalInZone <= 1)
            return center;

        float totalWidth = (totalInZone - 1) * config.workerSpacing;
        float startX = center.x - totalWidth * 0.5f;
        return new Vector2(startX + index * config.workerSpacing, center.y);
    }

    public Vector2 GetSourceFetchPosition(ZoneType workZone, int index, int total)
    {
        Vector2 pileCenter = GetSourcePilePosition(workZone);
        pileCenter.y -= config.sourceFetchOffsetY;

        if (total <= 1)
            return pileCenter;

        float totalWidth = (total - 1) * config.workerSpacing;
        float startX = pileCenter.x - totalWidth * 0.5f;
        return new Vector2(startX + index * config.workerSpacing, pileCenter.y);
    }

    public Vector2 GetCustomerEntryPosition(int index, int totalCount)
    {
        return GetCustomerPosition(index, totalCount) + new Vector2(-4f, 0f);
    }

    public Vector2 GetLiftWorkerPosition(Vector2 itemCenter, int index, int total)
    {
        float workerY = itemCenter.y - config.carryYOffset * 1.15f;
        if (total <= 1)
            return new Vector2(itemCenter.x, workerY);

        float totalWidth = (total - 1) * config.workerSpacing;
        float startX = itemCenter.x - totalWidth * 0.5f;
        return new Vector2(startX + index * config.workerSpacing, workerY);
    }

    public Vector2 GetItemCenterAboveZone(ZoneType zone)
    {
        return GetWorkItemPosition(zone);
    }

    public Vector2 GetSourceItemPosition(ZoneType workZone)
    {
        return GetSourcePilePosition(workZone) + new Vector2(0f, config.carryYOffset * 0.5f);
    }

    public Vector2 GetSourcePilePosition(ZoneType workZone)
    {
        switch (workZone)
        {
            case ZoneType.Chop:
                return IngredientPos + new Vector2(0f, 0.5f);
            case ZoneType.Cook:
                return ChopPos + new Vector2(0f, 0.5f);
            case ZoneType.Wok:
                return ChopPos + new Vector2(0f, 0.5f);
            case ZoneType.Plate:
                return PlatePos + new Vector2(0f, -0.2f);
            default:
                return GetZonePosition(workZone);
        }
    }

    public ZoneType GetUpstreamZone(ZoneType workZone, string recipeId, ProductionService production)
    {
        return production.GetUpstreamZone(workZone, recipeId);
    }
}
