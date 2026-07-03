using UnityEngine;

public class WorldLayout
{
    readonly GameConfigData config;

    public Vector2 CustomerAreaCenter = new Vector2(0f, 4.5f);
    public float CustomerSpacing = 1.5f;

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

    public Vector2 GetCustomerPosition(int index)
    {
        float totalWidth = (config.maxCustomers - 1) * CustomerSpacing;
        float startX = CustomerAreaCenter.x - totalWidth * 0.5f;
        return new Vector2(startX + index * CustomerSpacing, CustomerAreaCenter.y);
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

    public Vector2 GetLiftWorkerPosition(Vector2 objectCenter, int index, int total)
    {
        float workerY = objectCenter.y - config.carryYOffset;
        if (total <= 1)
            return new Vector2(objectCenter.x, workerY);

        float totalWidth = (total - 1) * config.workerSpacing;
        float startX = objectCenter.x - totalWidth * 0.5f;
        return new Vector2(startX + index * config.workerSpacing, workerY);
    }

    public Vector2 GetItemCenterAboveZone(ZoneType zone)
    {
        Vector2 center = GetZonePosition(zone);
        return center + new Vector2(0f, config.carryYOffset * 0.5f);
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
        var step = production.GetStepForZone(recipeId, workZone);
        if (step == null)
            return workZone;

        switch (workZone)
        {
            case ZoneType.Chop:
                return ZoneType.Ingredient;
            case ZoneType.Cook:
                return ZoneType.Chop;
            case ZoneType.Wok:
                return ZoneType.Chop;
            case ZoneType.Plate:
                var recipe = production.GetStepForZone(recipeId, workZone);
                if (recipeId == "stirfry")
                    return ZoneType.Wok;
                return ZoneType.Cook;
            default:
                return workZone;
        }
    }
}
