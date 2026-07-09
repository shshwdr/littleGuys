using System.Collections.Generic;
using System.Linq;

// 各机器区的产出缓冲区。物品以 identifier 为主键匹配；
// 当 identifier 为空时退化为按 stage 匹配（用于交付任意成品）。
public static class ZoneOutputStore
{
    public static void Add(ZoneData zone, int orderId, string recipeId, string identifier, FoodStage stage, FoodVisual visual)
    {
        zone.OutputItems.Add(new ZoneOutputItem
        {
            OrderId = orderId,
            RecipeId = recipeId,
            Identifier = identifier,
            Stage = stage,
            Visual = visual
        });
    }

    static bool Matches(ZoneOutputItem output, string identifier, FoodStage stage)
    {
        if (!string.IsNullOrEmpty(identifier))
            return output.Identifier == identifier;

        return output.Stage == stage;
    }

    static ZoneOutputItem FindAvailable(ZoneData zone, string identifier, FoodStage stage)
    {
        return zone.OutputItems.FirstOrDefault(output => !output.Occupied && Matches(output, identifier, stage));
    }

    public static bool Has(ZoneData zone, string identifier, FoodStage stage)
    {
        return FindAvailable(zone, identifier, stage) != null;
    }

    public static int CountAvailable(ZoneData zone, string identifier, FoodStage stage)
    {
        return zone.OutputItems.Count(output => !output.Occupied && Matches(output, identifier, stage));
    }

    public static ZoneOutputItem PeekAvailable(ZoneData zone, string identifier, FoodStage stage)
    {
        return FindAvailable(zone, identifier, stage);
    }

    public static bool TryTake(ZoneData zone, string identifier, FoodStage stage, out ZoneOutputItem item)
    {
        item = FindAvailable(zone, identifier, stage);
        if (item == null)
            return false;

        zone.OutputItems.Remove(item);
        return true;
    }

    public static bool TryClaim(ZoneData zone, string identifier, FoodStage stage, out ZoneOutputItem item)
    {
        item = FindAvailable(zone, identifier, stage);
        if (item == null)
            return false;

        item.Occupied = true;
        return true;
    }

    public static void ReleaseClaim(ZoneOutputItem item)
    {
        if (item != null)
            item.Occupied = false;
    }

    public static ZoneOutputItem PeekPlated(IEnumerable<ZoneData> zones)
    {
        foreach (var zone in zones)
        {
            var item = zone.OutputItems.FirstOrDefault(output =>
                !output.Occupied && output.Stage == FoodStage.Plated);
            if (item != null)
                return item;
        }

        return null;
    }
}
