using UnityEngine;

public class WorldLayout
{
    readonly GameConfigData config;

    public Vector2 CustomerAreaCenter = new Vector2(0f, 4f);
    public float CustomerSpacing = 1.5f;

    public Vector2 IngredientPos = new Vector2(-6f, 0f);
    public Vector2 ChopPos = new Vector2(-2f, 0f);
    public Vector2 CookPos = new Vector2(2f, 0f);
    public Vector2 PlatePos = new Vector2(6f, 0f);
    public Vector2 IdlePos = new Vector2(0f, -3f);

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
            case ZoneType.Plate: return PlatePos;
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
}
