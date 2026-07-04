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

        if (model.GetRecipe(recipeId) == null)
            return;

        model.ActiveRecipeId.Value = recipeId;
    }

    public bool TryActivateActiveTask(ZoneData zone, ZoneType zoneType)
    {
        if (!zone.IsUnlocked)
        {
            ClearZoneTask(zone);
            return false;
        }

        if (zone.HasActiveStep)
        {
            if (IsActiveTaskStillValid(zone, zoneType))
                return true;

            ClearZoneTask(zone);
        }

        string activeId = model.ActiveRecipeId.Value;
        if (string.IsNullOrEmpty(activeId))
        {
            ClearZoneTask(zone);
            return false;
        }

        var step = GetStepForZone(activeId, zoneType);
        if (step == null)
        {
            ClearZoneTask(zone);
            return false;
        }

        if (!IsStepReady(zoneType, activeId, step))
        {
            ClearZoneTask(zone);
            return false;
        }

        int orderId = ResolveOrderId(zoneType, activeId, step);
        if (orderId < 0)
        {
            ClearZoneTask(zone);
            return false;
        }

        ApplyStepToZone(zone, step, activeId);
        zone.CurrentOrderId = orderId;
        return true;
    }

    bool IsActiveTaskStillValid(ZoneData zone, ZoneType zoneType)
    {
        string activeId = model.ActiveRecipeId.Value;
        if (string.IsNullOrEmpty(activeId) || zone.CurrentRecipeId != activeId)
            return false;

        var step = GetStepForZone(activeId, zoneType);
        if (step == null)
            return false;

        if (step.SpawnInputInZone || (zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput))
            return true;

        var upstream = GetUpstreamZone(zoneType, activeId);
        var upstreamZone = model.GetZone(upstream);
        return ZoneOutputStore.Has(upstreamZone, step.Input, activeId, zone.CurrentOrderId);
    }

    bool IsStepReady(ZoneType zoneType, string recipeId, RecipeStep step)
    {
        if (step.SpawnInputInZone)
            return true;

        if (zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput)
            return true;

        var upstream = GetUpstreamZone(zoneType, recipeId);
        var upstreamZone = model.GetZone(upstream);
        return ZoneOutputStore.Has(upstreamZone, step.Input, recipeId);
    }

    int ResolveOrderId(ZoneType zoneType, string recipeId, RecipeStep step)
    {
        if (step.SpawnInputInZone || (zoneType == ZoneType.Chop && !step.ConsumeWorkerAsInput))
        {
            int orderId = model.NextOrderId++;
            model.ProductionOrders.Add(new ProductionOrder { OrderId = orderId, RecipeId = recipeId });
            return orderId;
        }

        var upstream = GetUpstreamZone(zoneType, recipeId);
        var upstreamZone = model.GetZone(upstream);
        var item = ZoneOutputStore.PeekAvailable(upstreamZone, recipeId, step.Input);
        return item != null ? item.OrderId : -1;
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
        return ZoneOutputStore.Has(
            upstreamZone,
            step.Input,
            zone.CurrentRecipeId,
            zone.CurrentOrderId);
    }

    public void CompleteZoneStep(ZoneData zone, ZoneType zoneType)
    {
        ClearZoneTask(zone);
    }

    public void OnOrderDelivered(int orderId)
    {
        model.ProductionOrders.RemoveAll(order => order.OrderId == orderId);
    }

    public void CancelActiveTask(ZoneData zone, ZoneType zoneType)
    {
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
