using UnityEngine;

public class Food : MonoBehaviour
{
    const string FoodPrefabPath = "prefab/food";

    [SerializeField] SpriteRenderer spriteRenderer;

    void Awake()
    {
        EnsureRenderer();
    }

    /// <summary>
    /// Spawns a food item from the shared food prefab (prefab/food) and returns
    /// its Food component. Falls back to a bare SpriteRenderer object only if the
    /// prefab is missing. Use this for every intermediate food visual.
    /// </summary>
    public static Food Spawn(Transform parent, string name = "Food")
    {
        var prefab = Resources.Load<GameObject>(FoodPrefabPath);

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, parent, false);
        }
        else
        {
            go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.AddComponent<SpriteRenderer>();
        }

        go.name = name;

        var food = go.GetComponentInChildren<Food>();
        if (food == null)
            food = go.AddComponent<Food>();

        return food;
    }

    public void SetVisual(FoodVisual visual, FoodStage stage)
    {
        EnsureRenderer();
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = LoadFoodSprite(visual);
        spriteRenderer.color = FoodVisualColors.GetTint(visual, stage);
    }

    public SpriteRenderer GetRenderer()
    {
        EnsureRenderer();
        return spriteRenderer;
    }

    void EnsureRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    static Sprite LoadFoodSprite(FoodVisual visual)
    {
        string path = GetResourcePath(visual);
        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        var fallback = Resources.Load<Sprite>("food");
        if (fallback != null)
            return fallback;

        return ResourceSpriteLoader.GetFoodVisual(visual);
    }

    static string GetResourcePath(FoodVisual visual)
    {
        switch (visual)
        {
            case FoodVisual.Veg:
                return "food/veg";
            case FoodVisual.Meat:
                return "food/meat";
            case FoodVisual.Minion:
                return "food/minion";
            default:
                return "food";
        }
    }
}
