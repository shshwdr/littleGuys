using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class ZoneBufferPileView : MonoBehaviour
{
    [SerializeField] ZoneType sourceZone;
    [SerializeField] FoodStage pileStage;
    [SerializeField] FoodVisual pileVisual = FoodVisual.None;

    readonly List<SpriteRenderer> pileRenderers = new List<SpriteRenderer>();
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

        float size = config.foodSpriteSize * 1.15f;
        SetOutputSlots(positions);
        EnsureRenderers(config, visual, size, 1);
        SetRendererVisibleCount(0);
    }

    public void BindExisting(ZoneType upstreamZone, GameModel gameModel, GameConfigData config, IReadOnlyList<Vector2> positions)
    {
        sourceZone = upstreamZone;
        model = gameModel;
        isSetup = true;
        SetOutputSlots(positions);

        float size = config.foodSpriteSize * 1.15f;
        EnsureRenderers(config, pileVisual, size, 1);
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

    void EnsureRenderers(GameConfigData config, FoodVisual visual, float size, int requiredCount)
    {
        while (pileRenderers.Count < requiredCount)
        {
            var renderer = ColorSpriteFactory.CreateSprite(
                "BufferPile_" + pileRenderers.Count,
                transform,
                ResourceSpriteLoader.GetFoodVisual(visual),
                Color.white,
                new Vector2(size, size));
            pileRenderers.Add(renderer);
        }
    }

    void Update()
    {
        if (!isSetup || model == null)
            return;

        var zone = model.GetZone(sourceZone);
        var items = zone.OutputItems.Where(output =>
            output.Stage == pileStage &&
            (pileVisual == FoodVisual.None || output.Visual == pileVisual)).ToList();

        int visibleCount = Mathf.Min(items.Count, outputSlots.Count);
        if (visibleCount <= 0)
        {
            SetRendererVisibleCount(0);
            return;
        }

        float size = model.Config.foodSpriteSize * 1.15f;
        EnsureRenderers(model.Config, items[0].Visual, size, visibleCount);
        SetRendererVisibleCount(visibleCount);

        for (int i = 0; i < visibleCount; i++)
        {
            var item = items[i];
            var renderer = pileRenderers[i];
            renderer.sprite = ResourceSpriteLoader.GetFoodVisual(item.Visual);
            renderer.color = item.Occupied ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            var pos = outputSlots[i];
            renderer.transform.position = new Vector3(pos.x, pos.y, renderer.transform.position.z);
        }
    }

    void SetRendererVisibleCount(int count)
    {
        for (int i = 0; i < pileRenderers.Count; i++)
        {
            if (pileRenderers[i] != null)
                pileRenderers[i].enabled = i < count;
        }
    }
}
