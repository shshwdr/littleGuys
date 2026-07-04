using System.Collections.Generic;
using System.Linq;

public static class ZoneOutputStore
{
    public static void Add(ZoneData zone, int orderId, string recipeId, FoodStage stage, FoodVisual visual)
    {
        zone.OutputItems.Add(new ZoneOutputItem
        {
            OrderId = orderId,
            RecipeId = recipeId,
            Stage = stage,
            Visual = visual
        });
    }

    static ZoneOutputItem FindAvailable(
        ZoneData zone,
        string recipeId,
        FoodStage stage,
        int orderId = -1)
    {
        if (orderId >= 0)
        {
            var exact = zone.OutputItems.FirstOrDefault(output =>
                !output.Occupied &&
                output.OrderId == orderId &&
                output.RecipeId == recipeId &&
                output.Stage == stage);

            if (exact != null)
                return exact;
        }

        if (!string.IsNullOrEmpty(recipeId))
        {
            var byRecipe = zone.OutputItems.FirstOrDefault(output =>
                !output.Occupied &&
                output.RecipeId == recipeId &&
                output.Stage == stage);

            if (byRecipe != null)
                return byRecipe;
        }

        return zone.OutputItems.FirstOrDefault(output =>
            !output.Occupied && output.Stage == stage);
    }

    public static bool TryClaim(
        ZoneData zone,
        string recipeId,
        FoodStage stage,
        out ZoneOutputItem item,
        int orderId = -1)
    {
        item = FindAvailable(zone, recipeId, stage, orderId);
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

    public static void ReleaseClaimsForOrder(ZoneData zone, int orderId)
    {
        foreach (var item in zone.OutputItems)
        {
            if (item.OrderId == orderId)
                item.Occupied = false;
        }
    }

    public static bool TryTake(ZoneData zone, string recipeId, FoodStage stage, out ZoneOutputItem item, int orderId = -1)
    {
        item = zone.OutputItems.FirstOrDefault(output =>
            output.OrderId == orderId &&
            output.RecipeId == recipeId &&
            output.Stage == stage);

        if (item == null && orderId >= 0)
        {
            item = zone.OutputItems.FirstOrDefault(output =>
                output.Occupied &&
                output.OrderId == orderId &&
                output.RecipeId == recipeId &&
                output.Stage == stage);
        }

        if (item == null)
            item = FindAvailable(zone, recipeId, stage, orderId);

        if (item == null)
            return false;

        zone.OutputItems.Remove(item);
        return true;
    }

    public static ZoneOutputItem PeekAvailable(ZoneData zone, string recipeId, FoodStage stage, int orderId = -1)
    {
        return FindAvailable(zone, recipeId, stage, orderId);
    }

    public static bool Has(ZoneData zone, FoodStage stage, string recipeId = null, int orderId = -1)
    {
        return FindAvailable(zone, recipeId, stage, orderId) != null;
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
