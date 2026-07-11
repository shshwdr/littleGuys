using System;
using System.Collections.Generic;

// 一个加工步骤需要的单个原料。
[Serializable]
public class StepInput
{
    public string Id;                 // 原料 identifier；若 IsMinion 则为 "Minion"
    public FoodStage Stage;           // 该原料在其来源区的阶段（用于匹配产出堆）
    public ZoneType Source;           // 从哪个区取料
    public bool IsMinion;             // 直接消耗本区的小人作为原材料（在区内生成，不外出取料）
    public bool FromIngredientSource; // 从无限原料区（Ingredient）直接获取
}

[Serializable]
public class RecipeStep
{
    public ZoneType Zone;
    public string OutputId;           // 本步骤产出的 identifier
    public List<StepInput> Inputs = new List<StepInput>();

    public FoodStage Input;           // 兼容字段：首个原料的阶段
    public FoodStage Output;          // 产出阶段（由 Zone 推导）
    public float BaseDuration;
    public int SoloWorkerCount;
    public bool SpawnInputInZone;     // 在区内生成原料（消耗小人）
    public bool ConsumeWorkerAsInput; // 消耗一个小人作为原材料
    public FoodVisual InputVisual;    // 旧视觉字段（identifier 优先，保留作为回退）
    public FoodVisual OutputVisual;

    // 需要外出取料的原料（排除在区内生成的小人原料）。
    public IEnumerable<StepInput> FetchInputs
    {
        get
        {
            if (Inputs == null)
                yield break;

            foreach (var input in Inputs)
            {
                if (input != null && !input.IsMinion)
                    yield return input;
            }
        }
    }
}

[Serializable]
public class RecipeData
{
    public string Id;
    public string DisplayName;
    public string DishIdentifier;     // "0"/"1"/"2"，仅最终菜谱有值
    public int Satiety = 1;
    public FoodStage FinalStage;
    public FoodVisual Visual;
    public RecipeStep[] Steps;

    public ZoneType FirstZone => Steps != null && Steps.Length > 0 ? Steps[0].Zone : ZoneType.Chop;
}

public class ZoneOutputItem
{
    public int OrderId;
    public string RecipeId;
    public string Identifier;         // 物品身份主键
    public FoodStage Stage;
    public FoodVisual Visual;
    public bool Occupied;
}
