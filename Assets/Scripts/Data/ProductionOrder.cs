public class ProductionOrder
{
    public int OrderId;
    public string RecipeId;
}

public class ZoneQueueItem
{
    public int OrderId;
    public string RecipeId;
}

// 已经取回并放入本区、等待一起加工的原料。
public class CollectedInput
{
    public string Id;
    public FoodStage Stage;
    public FoodVisual Visual;
    public UnityEngine.Vector2 Position;   // 放下后在区内的显示位置
}
