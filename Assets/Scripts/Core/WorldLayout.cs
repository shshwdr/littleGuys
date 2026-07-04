using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldLayout
{
    readonly GameConfigData config;
    readonly Dictionary<ZoneType, ZonePrefab> zonePrefabs = new Dictionary<ZoneType, ZonePrefab>();
    readonly List<CustomerSpawnPoint> customerSpawnPoints = new List<CustomerSpawnPoint>();

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

    public void RegisterFromScene()
    {
        zonePrefabs.Clear();
        customerSpawnPoints.Clear();

        foreach (var zone in Object.FindObjectsOfType<ZonePrefab>())
        {
            if (zonePrefabs.ContainsKey(zone.ZoneType))
            {
                Debug.LogWarning($"Duplicate ZonePrefab for {zone.ZoneType}; keeping first found.");
                continue;
            }

            zonePrefabs[zone.ZoneType] = zone;
        }

        customerSpawnPoints.AddRange(
            Object.FindObjectsOfType<CustomerSpawnPoint>()
                .OrderBy(point => point.SlotIndex));
    }

    public IReadOnlyList<ZonePrefab> GetSceneZones() => zonePrefabs.Values.ToList();

    public bool TryGetZonePrefab(ZoneType type, out ZonePrefab prefab) => zonePrefabs.TryGetValue(type, out prefab);

    public int CustomerSlotCount => customerSpawnPoints.Count > 0 ? customerSpawnPoints.Count : 0;

    public Vector2 GetZonePosition(ZoneType type)
    {
        if (zonePrefabs.TryGetValue(type, out var prefab))
            return prefab.RootPosition;

        return GetFallbackZonePosition(type);
    }

    public Vector2 GetInputPosition(ZoneType zone)
    {
        if (zonePrefabs.TryGetValue(zone, out var prefab))
            return prefab.GetInputPosition();

        return GetWorkItemPosition(zone);
    }

    public Vector2 GetWorkItemPosition(ZoneType zone)
    {
        if (zonePrefabs.TryGetValue(zone, out var prefab))
            return prefab.GetWorkPosition();

        Vector2 center = GetFallbackZonePosition(zone);
        return center + new Vector2(0f, config.workItemHeight);
    }

    public Vector2 GetOutputPosition(ZoneType zone)
    {
        if (zonePrefabs.TryGetValue(zone, out var prefab))
            return prefab.GetOutputPosition();

        return GetWorkItemPosition(zone);
    }

    public Vector2 GetCarriedItemPosition(ZoneType zone) => GetInputPosition(zone);

    public Vector2 ElevateCarriedItem(Vector2 basePosition)
    {
        return basePosition + new Vector2(0f, config.carriedItemHeight * 0.35f);
    }

    public Vector2 GetWorkerSlotPosition(ZoneType zone, int index, int totalInZone)
    {
        Vector2 center = zonePrefabs.TryGetValue(zone, out var prefab)
            ? prefab.GetWorkerRootPosition()
            : GetFallbackZonePosition(zone);

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

    public CustomerSpawnPoint GetCustomerSpawnPoint(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= customerSpawnPoints.Count)
            return null;

        return customerSpawnPoints[slotIndex];
    }

    public Vector2 GetCustomerPosition(int index, int totalCount)
    {
        var spawnPoint = GetCustomerSpawnPoint(index);
        if (spawnPoint != null)
            return spawnPoint.StandPosition;

        int count = Mathf.Max(totalCount, 1);
        float totalWidth = (count - 1) * CustomerSpacing;
        float rightX = CustomerAreaCenter.x + totalWidth * 0.5f;
        return new Vector2(rightX - index * CustomerSpacing, CustomerAreaCenter.y);
    }

    public Vector2 GetCustomerEntryPosition(int index, int totalCount)
    {
        var spawnPoint = GetCustomerSpawnPoint(index);
        if (spawnPoint != null)
            return spawnPoint.GetEntryPosition();

        return GetCustomerPosition(index, totalCount) + new Vector2(-4f, 0f);
    }

    public Vector2 GetCustomerSacrificePosition(int index, int totalCount)
    {
        var spawnPoint = GetCustomerSpawnPoint(index);
        if (spawnPoint != null)
            return spawnPoint.GetSacrificePosition();

        return GetCustomerPosition(index, totalCount) + new Vector2(0f, -1.1f);
    }

    public Vector2 GetCustomerDeliveryPosition(int index, int totalCount)
    {
        var spawnPoint = GetCustomerSpawnPoint(index);
        if (spawnPoint != null)
            return spawnPoint.GetDeliveryPosition();

        return GetCustomerPosition(index, totalCount) + new Vector2(0f, 0.3f);
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

    public Vector2 GetItemCenterAboveZone(ZoneType zone) => GetOutputPosition(zone);

    public Vector2 GetSourceItemPosition(ZoneType workZone)
    {
        return GetSourcePilePosition(workZone) + new Vector2(0f, config.carryYOffset * 0.5f);
    }

    public Vector2 GetSourcePilePosition(ZoneType workZone)
    {
        switch (workZone)
        {
            case ZoneType.Chop:
                return GetOutputPosition(ZoneType.Ingredient);
            case ZoneType.Cook:
            case ZoneType.Wok:
                return GetOutputPosition(ZoneType.Chop);
            case ZoneType.Plate:
                return GetOutputPosition(ZoneType.Plate);
            default:
                return GetZonePosition(workZone);
        }
    }

    public ZoneType GetUpstreamZone(ZoneType workZone, string recipeId, ProductionService production)
    {
        return production.GetUpstreamZone(workZone, recipeId);
    }

    Vector2 GetFallbackZonePosition(ZoneType type)
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
}
