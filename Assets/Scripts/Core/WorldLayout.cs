using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldLayout
{
    struct CustomerSlot
    {
        public CustomerSpawnPoint Point;
        public int LocalIndex;
    }

    readonly GameConfigData config;
    readonly Dictionary<ZoneType, ZonePrefab> zonePrefabs = new Dictionary<ZoneType, ZonePrefab>();
    readonly List<CustomerSlot> customerSlots = new List<CustomerSlot>();

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
        customerSlots.Clear();

        foreach (var zone in Object.FindObjectsOfType<ZonePrefab>())
        {
            if (zonePrefabs.ContainsKey(zone.ZoneType))
            {
                Debug.LogWarning($"Duplicate ZonePrefab for {zone.ZoneType}; keeping first found.");
                continue;
            }

            zonePrefabs[zone.ZoneType] = zone;
        }

        foreach (var point in Object.FindObjectsOfType<CustomerSpawnPoint>().OrderBy(p => p.SlotIndex))
        {
            for (int i = 0; i < point.Capacity; i++)
                customerSlots.Add(new CustomerSlot { Point = point, LocalIndex = i });
        }
    }

    public IReadOnlyList<ZonePrefab> GetSceneZones() => zonePrefabs.Values.ToList();

    public bool TryGetZonePrefab(ZoneType type, out ZonePrefab prefab) => zonePrefabs.TryGetValue(type, out prefab);

    public int CustomerSlotCount => customerSlots.Count > 0 ? customerSlots.Count : 0;

    bool TryGetCustomerSlot(int slotIndex, out CustomerSlot slot)
    {
        if (slotIndex >= 0 && slotIndex < customerSlots.Count)
        {
            slot = customerSlots[slotIndex];
            return true;
        }

        slot = default;
        return false;
    }

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

    public CustomerSpawnPoint GetCustomerSpawnPoint(int slotIndex)
    {
        return TryGetCustomerSlot(slotIndex, out var slot) ? slot.Point : null;
    }

    public Vector2 GetCustomerPosition(int index, int totalCount)
    {
        if (TryGetCustomerSlot(index, out var slot))
            return slot.Point.GetStandPosition(slot.LocalIndex);

        int count = Mathf.Max(totalCount, 1);
        float totalWidth = (count - 1) * CustomerSpacing;
        float rightX = CustomerAreaCenter.x + totalWidth * 0.5f;
        return new Vector2(rightX - index * CustomerSpacing, CustomerAreaCenter.y);
    }

    public Vector2 GetCustomerEntryPosition(int index, int totalCount)
    {
        if (TryGetCustomerSlot(index, out var slot))
            return slot.Point.GetEntryPosition(slot.LocalIndex);

        return GetCustomerPosition(index, totalCount) + new Vector2(-4f, 0f);
    }

    public Vector2 GetCustomerSacrificePosition(int index, int totalCount)
    {
        if (TryGetCustomerSlot(index, out var slot))
            return slot.Point.GetSacrificePosition(slot.LocalIndex);

        return GetCustomerPosition(index, totalCount) + new Vector2(0f, -1.1f);
    }

    public Vector2 GetCustomerDeliveryPosition(int index, int totalCount)
    {
        if (TryGetCustomerSlot(index, out var slot))
            return slot.Point.GetDeliveryPosition(slot.LocalIndex);

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
