using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 从 dish.csv 构建配方依赖图。
// 每个 identifier 通过 ingredients 在 machine 上生产：
//  - machine == Ingredient：叶子原料，直接从原料区获取，不产生加工步骤。
//  - ingredient == Minion：以本区小人作为原材料，在区内生成。
//  - 其它：递归查找该原料的制作方法（原料 + 机器），直到抵达 Minion 或 Ingredient。
// 若找不到某个中间原料的制作方法，或存在循环引用，则抛出异常并跳过该菜谱。
public static class DishRecipeBuilder
{
    public class BuildException : Exception
    {
        public BuildException(string message) : base(message) { }
    }

    // 构建所有拥有 dishIdentifier 的最终菜谱；无法解析的菜谱会记录错误并跳过。
    public static Dictionary<string, RecipeData> BuildMap(GameConfigData config)
    {
        var map = new Dictionary<string, RecipeData>();

        foreach (var dish in CSVLoader.GetFinalDishes())
        {
            try
            {
                var recipe = BuildRecipe(dish, config);
                map[recipe.Id] = recipe;
            }
            catch (BuildException ex)
            {
                Debug.LogError($"[DishRecipeBuilder] 菜谱 '{dish.identifier}'(dishIdentifier={dish.dishIdentifier}) 构建失败并停止制作：{ex.Message}");
            }
        }

        return map;
    }

    public static RecipeData BuildRecipe(DishInfo finalDish, GameConfigData config)
    {
        if (finalDish == null)
            throw new BuildException("菜谱为空");

        var zone = MachineToZone(finalDish.machine);
        if (zone == ZoneType.Ingredient)
            throw new BuildException($"最终菜谱 '{finalDish.identifier}' 不能直接是原料区产物");

        var steps = new List<RecipeStep>();
        var visiting = new HashSet<string>();
        var built = new HashSet<string>();
        Resolve(finalDish.identifier, config, visiting, built, steps);

        return new RecipeData
        {
            Id = finalDish.identifier,
            DisplayName = finalDish.DisplayName,
            DishIdentifier = finalDish.dishIdentifier,
            Satiety = SatietyForDish(finalDish.dishIdentifier),
            FinalStage = ZoneStage(zone),
            Visual = FoodVisual.None,
            Steps = steps.ToArray()
        };
    }

    // 后序遍历：先解析所有原料（其步骤先入列），再加入本节点的步骤，
    // 从而保证 steps 顺序即为流水线顺序。
    static void Resolve(
        string identifier,
        GameConfigData config,
        HashSet<string> visiting,
        HashSet<string> built,
        List<RecipeStep> steps)
    {
        if (built.Contains(identifier))
            return;

        var info = CSVLoader.GetDish(identifier);
        if (info == null)
            throw new BuildException($"找不到中间原料 '{identifier}' 的制作方法");

        var zone = MachineToZone(info.machine);

        // 原料区叶子：无需加工步骤，由消费它的步骤直接从原料区取。
        if (zone == ZoneType.Ingredient)
            return;

        if (visiting.Contains(identifier))
            throw new BuildException($"检测到循环引用：'{identifier}'");

        visiting.Add(identifier);

        var inputs = new List<StepInput>();
        foreach (var ingredient in info.CleanIngredients())
        {
            if (string.Equals(ingredient, DishInfo.MinionIngredient, StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(new StepInput
                {
                    Id = DishInfo.MinionIngredient,
                    Source = zone,
                    Stage = FoodStage.None,
                    IsMinion = true
                });
                continue;
            }

            var ingInfo = CSVLoader.GetDish(ingredient);
            if (ingInfo == null)
                throw new BuildException($"找不到中间原料 '{ingredient}' 的制作方法（被 '{identifier}' 需要）");

            var ingZone = MachineToZone(ingInfo.machine);
            if (ingZone == ZoneType.Ingredient)
            {
                inputs.Add(new StepInput
                {
                    Id = ingredient,
                    Source = ZoneType.Ingredient,
                    Stage = ZoneStage(ZoneType.Ingredient),
                    FromIngredientSource = true
                });
            }
            else
            {
                Resolve(ingredient, config, visiting, built, steps);
                inputs.Add(new StepInput
                {
                    Id = ingredient,
                    Source = ingZone,
                    Stage = ZoneStage(ingZone)
                });
            }
        }

        visiting.Remove(identifier);
        built.Add(identifier);

        bool isMinionStep = inputs.Any(i => i.IsMinion);
        var firstInput = inputs.FirstOrDefault();

        steps.Add(new RecipeStep
        {
            Zone = zone,
            OutputId = identifier,
            Inputs = inputs,
            Input = firstInput != null ? firstInput.Stage : FoodStage.None,
            Output = ZoneStage(zone),
            BaseDuration = ZoneDuration(zone, config),
            SoloWorkerCount = 0,
            SpawnInputInZone = isMinionStep,
            ConsumeWorkerAsInput = isMinionStep,
            InputVisual = FoodVisual.None,
            OutputVisual = FoodVisual.None
        });
    }

    public static ZoneType MachineToZone(string machine)
    {
        if (string.IsNullOrEmpty(machine))
            return ZoneType.Idle;

        switch (machine.Trim().ToLowerInvariant())
        {
            case "ingredient": return ZoneType.Ingredient;
            case "chop": return ZoneType.Chop;
            case "cook":
            case "cooker": return ZoneType.Cook;
            case "wok": return ZoneType.Wok;
            case "plate": return ZoneType.Plate;
            case "splitter": return ZoneType.Splitter;
            default: return ZoneType.Idle;
        }
    }

    public static FoodStage ZoneStage(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Ingredient: return FoodStage.Raw;
            case ZoneType.Chop: return FoodStage.Chopped;
            case ZoneType.Cook: return FoodStage.Cooked;
            case ZoneType.Wok: return FoodStage.Fried;
            case ZoneType.Plate: return FoodStage.Plated;
            default: return FoodStage.None;
        }
    }

    static float ZoneDuration(ZoneType zone, GameConfigData config)
    {
        switch (zone)
        {
            case ZoneType.Chop: return config.chopDuration;
            case ZoneType.Cook: return config.cookDuration;
            case ZoneType.Wok: return config.wokDuration;
            case ZoneType.Plate: return config.plateDuration;
            case ZoneType.Splitter: return config.splitterDuration;
            default: return config.plateDuration;
        }
    }

    static int SatietyForDish(string dishIdentifier)
    {
        if (int.TryParse(dishIdentifier, out int index))
            return Mathf.Max(1, index + 1);

        return 1;
    }

    // 找到某 dishIdentifier 对应的配方 Id（= 最终食物 identifier）。
    public static string GetRecipeIdForDish(Dictionary<string, RecipeData> recipes, string dishIdentifier)
    {
        if (recipes == null || string.IsNullOrEmpty(dishIdentifier))
            return null;

        foreach (var recipe in recipes.Values)
        {
            if (recipe.DishIdentifier == dishIdentifier)
                return recipe.Id;
        }

        return null;
    }

    // 解锁某配方所需的全部机器区。
    public static void UnlockZonesForRecipe(GameModel model, RecipeData recipe)
    {
        if (model == null || recipe == null || recipe.Steps == null)
            return;

        foreach (var step in recipe.Steps)
        {
            if (model.Zones.ContainsKey(step.Zone))
                model.GetZone(step.Zone).IsUnlocked = true;

            foreach (var input in step.Inputs)
            {
                if (input != null && model.Zones.ContainsKey(input.Source))
                    model.GetZone(input.Source).IsUnlocked = true;
            }
        }
    }
}
