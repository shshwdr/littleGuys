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

        if (!IsStepReady(step))
        {
            ClearZoneTask(zone);
            return false;
        }

        ApplyStepToZone(zone, step, activeId);
        zone.CurrentOrderId = model.NextOrderId++;
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

        return IsStepReady(step);
    }

    // 所有需要外出取的原料都必须同时就绪，才允许开始（多原料：需集齐才制作）。
    bool IsStepReady(RecipeStep step)
    {
        if (step.SpawnInputInZone)
            return true;

        foreach (var input in step.FetchInputs)
        {
            if (input.FromIngredientSource)
                continue;

            var srcZone = model.GetZone(input.Source);
            if (!ZoneOutputStore.Has(srcZone, input.Id, input.Stage))
                return false;
        }

        return true;
    }

    public bool CanFetchForActiveTask(ZoneData zone, ZoneType zoneType)
    {
        if (!zone.HasActiveStep)
            return false;

        var step = GetStepForZone(zone.CurrentRecipeId, zoneType);
        if (step == null)
            return false;

        return IsStepReady(step);
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
        if (recipe == null || recipe.Steps == null)
            return null;

        return recipe.Steps.FirstOrDefault(step => step.Zone == zone);
    }

    public void ApplyStepToZone(ZoneData zone, RecipeStep step, string recipeId)
    {
        zone.HasActiveStep = true;
        zone.CurrentRecipeId = recipeId;
        zone.StepInput = step.Input;
        zone.StepOutput = step.Output;
        zone.StepOutputId = step.OutputId;
        zone.BaseDuration = step.BaseDuration;
        zone.SoloWorkerCount = step.SoloWorkerCount;
        zone.SpawnInputInZone = step.SpawnInputInZone;
        zone.ConsumeWorkerAsInput = step.ConsumeWorkerAsInput;
        zone.StepInputVisual = step.InputVisual;
        zone.StepOutputVisual = step.OutputVisual;
        zone.SharedFoodVisual = step.InputVisual;

        zone.StepInputs = step.FetchInputs.ToList();
        zone.FetchInputIndex = 0;
        zone.CollectedInputs.Clear();
        zone.SharedItemId = "";
    }

    public void ClearZoneTask(ZoneData zone)
    {
        zone.HasActiveStep = false;
        zone.CurrentRecipeId = null;
        zone.CurrentOrderId = 0;
        zone.StepInput = FoodStage.None;
        zone.StepOutput = FoodStage.None;
        zone.StepOutputId = "";
        zone.BaseDuration = 0f;
        zone.SoloWorkerCount = 0;
        zone.SpawnInputInZone = false;
        zone.ConsumeWorkerAsInput = false;
        zone.StepInputVisual = FoodVisual.None;
        zone.StepOutputVisual = FoodVisual.None;
        zone.SharedFoodVisual = FoodVisual.None;
        zone.SharedItemId = "";
        zone.WorkRotation = 0f;
        zone.StepInputs.Clear();
        zone.FetchInputIndex = 0;
        zone.CollectedInputs.Clear();
    }

    // 当前正在外出取的原料（多原料时按顺序逐个取）。
    public StepInput CurrentFetchInput(ZoneData zone)
    {
        if (zone.FetchInputIndex < 0 || zone.FetchInputIndex >= zone.StepInputs.Count)
            return null;

        return zone.StepInputs[zone.FetchInputIndex];
    }

    public bool AllInputsCollected(ZoneData zone)
    {
        return zone.FetchInputIndex >= zone.StepInputs.Count;
    }
}
