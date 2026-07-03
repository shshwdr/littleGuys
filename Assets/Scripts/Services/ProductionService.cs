using System.Linq;

public class ProductionService
{
    readonly GameModel model;

    public ProductionService(GameModel model)
    {
        this.model = model;
    }

    public void ActivateRecipe(string recipeId)
    {
        if (!model.UnlockedRecipes.Contains(recipeId))
            return;

        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return;

        model.ActiveRecipeId.Value = recipeId;
        PushRecipeToChain(recipeId);
    }

    public void PushRecipeToChain(string recipeId)
    {
        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return;

        int orderId = model.NextOrderId++;
        model.ProductionOrders.Add(new ProductionOrder { OrderId = orderId, RecipeId = recipeId });

        foreach (var step in recipe.Steps)
        {
            var zone = model.GetZone(step.Zone);
            if (!zone.IsUnlocked)
                continue;

            zone.TaskQueue.Add(new ZoneQueueItem { OrderId = orderId, RecipeId = recipeId });
        }
    }

    public bool HasQueuedOrActiveWork()
    {
        if (model.ProductionOrders.Count > 0)
            return true;

        foreach (var zone in model.Zones.Values)
        {
            if (zone.TaskQueue.Count > 0)
                return true;
        }

        return false;
    }

    public bool TryActivateNextReadyTask(ZoneData zone, ZoneType zoneType)
    {
        if (!zone.IsUnlocked)
        {
            ClearZoneTask(zone);
            return false;
        }

        for (int i = 0; i < zone.TaskQueue.Count; i++)
        {
            var item = zone.TaskQueue[i];
            var step = GetStepForZone(item.RecipeId, zoneType);
            if (step == null)
                continue;

            if (!IsZoneTaskReady(zoneType, item, step))
                continue;

            if (!TryClaimUpstream(zoneType, item, step))
                continue;

            zone.ActiveQueueIndex = i;
            ApplyStepToZone(zone, step, item.RecipeId);
            zone.CurrentOrderId = item.OrderId;
            return true;
        }

        ClearZoneTask(zone);
        return false;
    }

    public bool IsZoneTaskReady(ZoneType zoneType, ZoneQueueItem item, RecipeStep step)
    {
        if (step.SpawnInputInZone)
            return true;

        if (zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput)
            return true;

        var upstream = GetUpstreamZone(zoneType, item.RecipeId);
        var upstreamZone = model.GetZone(upstream);
        return ZoneOutputStore.Has(upstreamZone, step.Input, item.RecipeId, item.OrderId);
    }

    bool TryClaimUpstream(ZoneType zoneType, ZoneQueueItem item, RecipeStep step)
    {
        if (step.SpawnInputInZone)
            return true;

        if (zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput)
            return true;

        var upstream = GetUpstreamZone(zoneType, item.RecipeId);
        var upstreamZone = model.GetZone(upstream);
        return ZoneOutputStore.TryClaim(upstreamZone, item.RecipeId, step.Input, out _, item.OrderId);
    }

    public bool CanFetchForActiveTask(ZoneData zone, ZoneType zoneType)
    {
        if (!zone.HasActiveStep)
            return false;

        var step = GetStepForZone(zone.CurrentRecipeId, zoneType);
        if (step == null)
            return false;

        if (step.SpawnInputInZone)
            return true;

        if (zoneType == ZoneType.Chop && !zone.ConsumeWorkerAsInput)
            return true;

        var upstream = GetUpstreamZone(zoneType, zone.CurrentRecipeId);
        var upstreamZone = model.GetZone(upstream);
        return upstreamZone.OutputItems.Any(output =>
            output.Occupied &&
            output.OrderId == zone.CurrentOrderId &&
            output.RecipeId == zone.CurrentRecipeId &&
            output.Stage == step.Input);
    }

    public void CompleteZoneStep(ZoneData zone, ZoneType zoneType)
    {
        if (zone.ActiveQueueIndex >= 0 && zone.ActiveQueueIndex < zone.TaskQueue.Count)
            zone.TaskQueue.RemoveAt(zone.ActiveQueueIndex);
        else if (zone.TaskQueue.Count > 0)
            zone.TaskQueue.RemoveAt(0);

        ClearZoneTask(zone);

        var activeRecipe = model.GetRecipe(model.ActiveRecipeId.Value);
        if (activeRecipe != null && activeRecipe.FirstZone == zoneType)
            PushRecipeToChain(model.ActiveRecipeId.Value);
    }

    public void OnOrderDelivered(int orderId)
    {
        model.ProductionOrders.RemoveAll(order => order.OrderId == orderId);
    }

    public void CancelActiveTask(ZoneData zone, ZoneType zoneType)
    {
        if (!zone.HasActiveStep)
            return;

        var step = GetStepForZone(zone.CurrentRecipeId, zoneType);
        if (step != null && !step.SpawnInputInZone && !(zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput))
        {
            var upstream = GetUpstreamZone(zoneType, zone.CurrentRecipeId);
            ZoneOutputStore.ReleaseClaimsForOrder(model.GetZone(upstream), zone.CurrentOrderId);
        }

        ClearZoneTask(zone);
    }

    public RecipeStep GetStepForZone(string recipeId, ZoneType zone)
    {
        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return null;

        return recipe.Steps.FirstOrDefault(step => step.Zone == zone);
    }

    public void ApplyStepToZone(ZoneData zone, RecipeStep step, string recipeId)
    {
        zone.HasActiveStep = true;
        zone.CurrentRecipeId = recipeId;
        zone.StepInput = step.Input;
        zone.StepOutput = step.Output;
        zone.BaseDuration = step.BaseDuration;
        zone.SoloWorkerCount = step.SoloWorkerCount;
        zone.SpawnInputInZone = step.SpawnInputInZone;
        zone.ConsumeWorkerAsInput = step.ConsumeWorkerAsInput;
        zone.StepInputVisual = step.InputVisual;
        zone.StepOutputVisual = step.OutputVisual;
        zone.SharedFoodVisual = step.InputVisual;
    }

    public void ClearZoneTask(ZoneData zone)
    {
        zone.HasActiveStep = false;
        zone.CurrentRecipeId = null;
        zone.CurrentOrderId = 0;
        zone.ActiveQueueIndex = -1;
        zone.StepInput = FoodStage.None;
        zone.StepOutput = FoodStage.None;
        zone.BaseDuration = 0f;
        zone.SoloWorkerCount = 0;
        zone.SpawnInputInZone = false;
        zone.ConsumeWorkerAsInput = false;
        zone.StepInputVisual = FoodVisual.None;
        zone.StepOutputVisual = FoodVisual.None;
        zone.SharedFoodVisual = FoodVisual.None;
        zone.WorkRotation = 0f;
    }

    public ZoneType GetUpstreamZone(ZoneType zoneType, string recipeId)
    {
        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return zoneType;

        for (int i = 0; i < recipe.Steps.Length; i++)
        {
            if (recipe.Steps[i].Zone != zoneType)
                continue;

            if (i == 0)
                return ZoneType.Ingredient;

            return recipe.Steps[i - 1].Zone;
        }

        return zoneType;
    }
}
