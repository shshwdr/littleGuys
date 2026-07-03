using System.Linq;

public class ProductionService
{
    readonly GameModel model;

    public ProductionService(GameModel model)
    {
        this.model = model;
    }

    public void EnqueueRecipe(string recipeId)
    {
        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return;

        int orderId = model.NextOrderId++;
        model.ProductionOrders.Add(new ProductionOrder { OrderId = orderId, RecipeId = recipeId });

        foreach (var step in recipe.Steps)
            model.GetZone(step.Zone).TaskQueue.Add(new ZoneQueueItem { OrderId = orderId, RecipeId = recipeId });
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

    public bool TryActivateQueueHead(ZoneData zone, ZoneType zoneType)
    {
        if (zone.TaskQueue.Count == 0)
        {
            ClearZoneTask(zone);
            return false;
        }

        var item = zone.TaskQueue[0];
        var step = GetStepForZone(item.RecipeId, zoneType);
        if (step == null)
        {
            zone.TaskQueue.RemoveAt(0);
            return TryActivateQueueHead(zone, zoneType);
        }

        if (!IsZoneTaskReady(zoneType, item, step))
            return false;

        ApplyStepToZone(zone, step, item.RecipeId);
        zone.CurrentOrderId = item.OrderId;
        return true;
    }

    public bool IsZoneTaskReady(ZoneType zoneType, ZoneQueueItem item, RecipeStep step)
    {
        if (step.SpawnInputInZone || zoneType == ZoneType.Chop)
            return true;

        var upstream = GetUpstreamZone(zoneType, item.RecipeId);
        var upstreamZone = model.GetZone(upstream);
        return ZoneOutputStore.Has(upstreamZone, step.Input, item.RecipeId, item.OrderId);
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

        if (zoneType == ZoneType.Chop)
            return true;

        var upstream = GetUpstreamZone(zoneType, zone.CurrentRecipeId);
        return ZoneOutputStore.Has(
            model.GetZone(upstream),
            step.Input,
            zone.CurrentRecipeId,
            zone.CurrentOrderId);
    }

    public void CompleteZoneStep(ZoneData zone)
    {
        if (zone.TaskQueue.Count > 0)
            zone.TaskQueue.RemoveAt(0);

        ClearZoneTask(zone);
    }

    public void OnOrderDelivered(int orderId)
    {
        model.ProductionOrders.RemoveAll(order => order.OrderId == orderId);
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

    public static ZoneType GetUpstreamZone(ZoneType zoneType, string recipeId)
    {
        switch (zoneType)
        {
            case ZoneType.Cook:
                return ZoneType.Chop;
            case ZoneType.Wok:
                return ZoneType.Chop;
            case ZoneType.Plate:
                return recipeId == "stirfry" ? ZoneType.Wok : ZoneType.Cook;
            default:
                return zoneType;
        }
    }
}
