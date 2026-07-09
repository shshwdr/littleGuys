using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class ZoneBufferPileView : MonoBehaviour
{
    [SerializeField] ZoneType sourceZone;
    [SerializeField] FoodStage pileStage;
    [SerializeField] FoodVisual pileVisual = FoodVisual.None;

    readonly List<Food> pileFoods = new List<Food>();
    readonly List<Vector2> outputSlots = new List<Vector2>();
    GameModel model;
    bool isSetup;

    public void Setup(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, IReadOnlyList<Vector2> positions, FoodStage stage, FoodVisual visual)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        pileStage = stage;
        pileVisual = visual;
        isSetup = true;

        SetOutputSlots(positions);
        EnsureFoods(1);
        SetVisibleCount(0);
    }

    public void BindExisting(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, IReadOnlyList<Vector2> positions)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        isSetup = true;
        SetOutputSlots(positions);

        EnsureFoods(1);
    }

    void SetOutputSlots(IReadOnlyList<Vector2> positions)
    {
        outputSlots.Clear();
        if (positions != null)
        {
            for (int i = 0; i < positions.Count; i++)
                outputSlots.Add(positions[i]);
        }

        if (outputSlots.Count == 0)
            outputSlots.Add(transform.position);
    }

    void EnsureFoods(int requiredCount)
    {
        while (pileFoods.Count < requiredCount)
        {
            var food = Food.Spawn(transform, "BufferPile_" + pileFoods.Count);
            pileFoods.Add(food);
        }
    }

    void Update()
    {
        if (!isSetup || model == null)
            return;

        var zone = model.GetZone(sourceZone);
        // pileStage == None 表示展示该区所有产出（identifier 模式）；否则按阶段过滤。
        var items = zone.OutputItems.Where(output =>
            (pileStage == FoodStage.None || output.Stage == pileStage) &&
            (pileVisual == FoodVisual.None || output.Visual == pileVisual)).ToList();

        int visibleCount = Mathf.Min(items.Count, outputSlots.Count);
        if (visibleCount <= 0)
        {
            SetVisibleCount(0);
            return;
        }

        EnsureFoods(visibleCount);
        SetVisibleCount(visibleCount);

        for (int i = 0; i < visibleCount; i++)
        {
            var item = items[i];
            var food = pileFoods[i];
            food.SetVisual(item.Identifier, item.Visual, item.Stage);

            var renderer = food.GetRenderer();
            if (renderer != null)
            {
                var color = renderer.color;
                color.a = item.Occupied ? 0.55f : 1f;
                renderer.color = color;
            }

            var pos = outputSlots[i];
            food.transform.position = new Vector3(pos.x, pos.y, food.transform.position.z);
        }
    }

    void SetVisibleCount(int count)
    {
        for (int i = 0; i < pileFoods.Count; i++)
        {
            if (pileFoods[i] != null)
                pileFoods[i].gameObject.SetActive(i < count);
        }
    }
}
