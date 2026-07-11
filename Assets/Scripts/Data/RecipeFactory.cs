using System.Collections.Generic;

// 配方现在完全由 dish.csv 驱动（见 DishRecipeBuilder）。
public static class RecipeFactory
{
    public static Dictionary<string, RecipeData> CreateMap(GameConfigData config)
    {
        return DishRecipeBuilder.BuildMap(config);
    }
}
