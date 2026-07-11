using System.Collections.Generic;
using System.Linq;

public enum FoodStepStatus
{
    Pending,
    InProgress,
    Done
}

public class FoodWorkStep
{
    public string Label;
    public ZoneType Zone;
    public FoodStepStatus Status = FoodStepStatus.Pending;
    public bool IsFetch;
}

public class CustomerOrderItem
{
    public string RecipeId;
    public string DisplayName;
    public int OrderId;
    public bool IsDelivered;
}

public class FoodWorkOrder
{
    public int OrderId;
    public int CustomerId;
    public string CustomerName;
    public string RecipeId;
    public int CreatedSequence;
    public List<FoodWorkStep> Steps = new List<FoodWorkStep>();
    public bool IsDelivered;

    public int CurrentStepIndex
    {
        get
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                if (Steps[i].Status != FoodStepStatus.Done)
                    return i;
            }
            return -1;
        }
    }

    public FoodWorkStep CurrentStep
    {
        get
        {
            int idx = CurrentStepIndex;
            return idx >= 0 ? Steps[idx] : null;
        }
    }

    public static FoodWorkOrder Create(
        int orderId,
        int customerId,
        string customerName,
        string recipeId,
        int sequence,
        RecipeData recipe)
    {
        var order = new FoodWorkOrder
        {
            OrderId = orderId,
            CustomerId = customerId,
            CustomerName = customerName,
            RecipeId = recipeId,
            CreatedSequence = sequence
        };

        if (recipe?.Steps == null)
            return order;

        for (int i = 0; i < recipe.Steps.Length; i++)
        {
            var step = recipe.Steps[i];
            if (i == 0 && !step.SpawnInputInZone)
            {
                order.Steps.Add(new FoodWorkStep
                {
                    Label = "Fetch",
                    Zone = step.Zone,
                    IsFetch = true
                });
            }

            order.Steps.Add(new FoodWorkStep
            {
                Label = ZoneLabel(step.Zone),
                Zone = step.Zone
            });
        }

        order.Steps.Add(new FoodWorkStep
        {
            Label = "Deliver",
            Zone = ZoneType.Plate
        });

        return order;
    }

    static string ZoneLabel(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Chop: return "Chop";
            case ZoneType.Cook: return "Cook";
            case ZoneType.Wok: return "Wok";
            case ZoneType.Plate: return "Plate";
            default: return zone.ToString();
        }
    }
}

public static class CustomerOrderPlanner
{
    static readonly string[] NamePool =
    {
        "Alice", "Bob", "Carol", "Dave", "Eve", "Frank", "Grace", "Henry"
    };

    public static string GenerateName(int customerId)
    {
        return NamePool[(customerId - 1) % NamePool.Length];
    }

    public static List<string> SelectRecipes(
        int requiredSatiety,
        IEnumerable<string> unlockedRecipeIds,
        Dictionary<string, RecipeData> recipes)
    {
        var sorted = unlockedRecipeIds
            .Where(id => recipes.ContainsKey(id))
            .Select(id => recipes[id])
            .OrderByDescending(r => r.Satiety)
            .ToList();

        var result = new List<string>();
        int remaining = requiredSatiety;

        foreach (var recipe in sorted)
        {
            while (remaining >= recipe.Satiety)
            {
                result.Add(recipe.Id);
                remaining -= recipe.Satiety;
            }
        }

        if (remaining > 0 && sorted.Count > 0)
        {
            var smallest = sorted[sorted.Count - 1];
            while (remaining > 0)
            {
                result.Add(smallest.Id);
                remaining -= smallest.Satiety;
            }
        }

        return result;
    }
}
