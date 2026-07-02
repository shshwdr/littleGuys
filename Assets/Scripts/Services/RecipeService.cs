public class RecipeService
{
    readonly GameModel model;

    public RecipeService(GameModel model)
    {
        this.model = model;
    }

    public void SelectRecipe(RecipeData recipe)
    {
        model.ActiveRecipe.Value = recipe;
        ApplyRecipeSteps(recipe);
    }

    void ApplyRecipeSteps(RecipeData recipe)
    {
        ClearWorkZones();

        if (recipe == null)
            return;

        foreach (var step in recipe.Steps)
        {
            var zone = model.GetZone(step.Zone);
            zone.HasActiveStep = true;
            zone.StepInput = step.Input;
            zone.StepOutput = step.Output;
            zone.BaseDuration = step.BaseDuration;
            zone.InputBuffer = 0;
            zone.OutputBuffer = 0;
            zone.TaskProgress.Value = 0f;
            zone.Phase = ZonePhase.Idle;
            zone.DeliveryCustomer = null;
            zone.HasSharedItem = false;
            zone.SharedItemStage = FoodStage.None;
        }
    }

    void ClearWorkZones()
    {
        foreach (var type in new[] { ZoneType.Chop, ZoneType.Cook, ZoneType.Plate })
        {
            var zone = model.GetZone(type);
            zone.HasActiveStep = false;
            zone.StepInput = FoodStage.None;
            zone.StepOutput = FoodStage.None;
            zone.BaseDuration = 0f;
            zone.InputBuffer = 0;
            zone.OutputBuffer = 0;
            zone.TaskProgress.Value = 0f;
            zone.WorkSpeed.Value = 0f;
            zone.StatusText.Value = "0%";
            zone.Phase = ZonePhase.Idle;
            zone.DeliveryCustomer = null;
            zone.HasSharedItem = false;
            zone.SharedItemStage = FoodStage.None;
        }
    }
}
