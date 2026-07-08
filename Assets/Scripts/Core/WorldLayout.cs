using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldLayout
{
    readonly GameConfigData config;
    readonly Dictionary<ZoneType, ZonePrefab> zonePrefabs = new Dictionary<ZoneType, ZonePrefab>();
    readonly List<Vector2> customerSlotPositions = new List<Vector2>();

    public Vector2 IngredientPos = new Vector2(-8f, 0f);
    public Vector2 ChopPos = new Vector2(-4.5f, 0f);
    public Vector2 CookPos = new Vector2(-1f, 0f);
    public Vector2 WokPos = new Vector2(2.5f, 0f);
    public Vector2 PlatePos = new Vector2(6f, 0f);
    public Vector2 SplitterPos = new Vector2(-3.5f, -3.5f);
    public Vector2 IdlePos = new Vector2(3.5f, -3.5f);

    Vector2? registeredFoodOutputPos;
    Vector2? registeredSacrificePos;

    public WorldLayout(GameConfigData config)
    {
        this.config = config;
    }

    public void RegisterFromScene()
    {
        zonePrefabs.Clear();
        customerSlotPositions.Clear();

        foreach (var zone in Object.FindObjectsOfType<ZonePrefab>())
        {
            if (zonePrefabs.ContainsKey(zone.ZoneType))
            {
                Debug.LogWarning($"Duplicate ZonePrefab for {zone.ZoneType}; keeping first found.");
                continue;
            }

            zonePrefabs[zone.ZoneType] = zone;
        }

        foreach (var point in Object.FindObjectsOfType<CustomerSpawnPoint>())
        {
            for (int i = 0; i < point.SlotCount; i++)
                customerSlotPositions.Add(point.GetPosition(i));
        }
    }

    public void RegisterWorldPositions(Transform foodOutput, Transform sacrifice)
    {
        if (foodOutput != null)
            registeredFoodOutputPos = foodOutput.position;

        if (sacrifice != null)
            registeredSacrificePos = sacrifice.position;
    }

    public Vector2 GetFoodOutputPosition()
    {
        if (registeredFoodOutputPos.HasValue)
            return registeredFoodOutputPos.Value;

        return GetOutputPosition(ZoneType.Plate);
    }

    public Vector2 GetSacrificeQueueBasePosition()
    {
        if (registeredSacrificePos.HasValue)
            return registeredSacrificePos.Value;

        return IdlePos;
    }

    public IReadOnlyList<ZonePrefab> GetSceneZones() => zonePrefabs.Values.ToList();

    public bool TryGetZonePrefab(ZoneType type, out ZonePrefab prefab) => zonePrefabs.TryGetValue(type, out prefab);

    public int CustomerSlotCount => customerSlotPositions.Count;

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

    public Vector2 GetOutputPosition(ZoneType zone, int slotIndex)
    {
        if (zonePrefabs.TryGetValue(zone, out var prefab))
            return prefab.GetOutputPosition(slotIndex);

        return GetOutputPosition(zone);
    }

    public Vector2 GetCarriedItemPosition(ZoneType zone) => GetInputPosition(zone);

    public Vector2 ElevateCarriedItem(Vector2 basePosition)
    {
        return basePosition + new Vector2(0f, config.carriedItemHeight * 0.35f);
    }

    public Vector2 PlaceItemOnGround(Vector2 position, Vector2 groundReference)
    {
        if (position.y <= groundReference.y + 0.02f)
            return position;

        return new Vector2(position.x, groundReference.y);
    }

    public Vector2 GetWorkerSlotPosition(ZoneType zone, int index, int totalInZone)
    {
        return GetSlotPositionAround(GetWorkItemPosition(zone), index, totalInZone);
    }

    public Vector2 GetOutputSlotPosition(ZoneType zone, int index, int totalInZone)
    {
        return GetSlotPositionAround(GetOutputPosition(zone), index, totalInZone);
    }

    Vector2 GetSlotPositionAround(Vector2 center, int index, int totalInZone)
    {
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

    public Vector2 GetCustomerPosition(int index, int totalCount)
    {
        if (index >= 0 && index < customerSlotPositions.Count)
            return customerSlotPositions[index];

        Debug.LogWarning($"Customer slot {index} is missing in scene; using fallback position.");
        return IdlePos;
    }

    public Vector2 GetSacrificeQueuePosition(Vector2 basePosition, int queueIndex)
    {
        if (queueIndex <= 0)
            return basePosition;

        return basePosition + config.sacrificeQueueOffset * queueIndex;
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
