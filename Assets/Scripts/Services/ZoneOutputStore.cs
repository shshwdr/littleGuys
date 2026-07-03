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

    public static bool TryTake(ZoneData zone, string recipeId, FoodStage stage, out ZoneOutputItem item, int orderId = -1)
    {
        if (orderId >= 0)
        {
            item = zone.OutputItems.FirstOrDefault(output =>
                output.OrderId == orderId &&
                output.RecipeId == recipeId &&
                output.Stage == stage);

            if (item != null)
            {
                zone.OutputItems.Remove(item);
                return true;
            }
        }

        item = zone.OutputItems.FirstOrDefault(output =>
            output.RecipeId == recipeId && output.Stage == stage);

        if (item == null)
            item = zone.OutputItems.FirstOrDefault(output => output.Stage == stage);

        if (item == null)
            return false;

        zone.OutputItems.Remove(item);
        return true;
    }

    public static bool Has(ZoneData zone, FoodStage stage, string recipeId = null, int orderId = -1)
    {
        if (orderId >= 0 && !string.IsNullOrEmpty(recipeId))
        {
            return zone.OutputItems.Any(item =>
                item.OrderId == orderId &&
                item.RecipeId == recipeId &&
                item.Stage == stage);
        }

        if (!string.IsNullOrEmpty(recipeId))
            return zone.OutputItems.Any(item => item.RecipeId == recipeId && item.Stage == stage);

        return zone.OutputItems.Any(item => item.Stage == stage);
    }

    public static ZoneOutputItem PeekPlated(string recipeId, IEnumerable<ZoneData> zones)
    {
        foreach (var zone in zones)
        {
            var item = zone.OutputItems.FirstOrDefault(output =>
                output.RecipeId == recipeId && output.Stage == FoodStage.Plated);
            if (item != null)
                return item;
        }

        return null;
    }
}
