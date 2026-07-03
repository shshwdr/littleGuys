using System.Linq;
using UnityEngine;

public static class ResourceSpriteLoader
{
    static Sprite customerSprite;
    static Sprite foodSprite;
    static Sprite vegSprite;
    static Sprite meatSprite;
    static Sprite minionSprite;
    static Sprite squareSprite;

    public static Sprite GetCustomer() => LoadFirst(ref customerSprite, "customer");
    public static Sprite GetFood() => LoadFirst(ref foodSprite, "food");
    public static Sprite GetVeg() => LoadSprite(ref vegSprite, "food/veg") ?? GetFood();
    public static Sprite GetMeat() => LoadSprite(ref meatSprite, "food/meat") ?? GetFood();
    public static Sprite GetMinion() => LoadFirst(ref minionSprite, "minion");
    public static Sprite GetSquare() => LoadSprite(ref squareSprite, "square") ?? CreateWhiteSquare();

    public static Sprite GetFoodVisual(FoodVisual visual)
    {
        switch (visual)
        {
            case FoodVisual.Veg:
                return GetVeg();
            case FoodVisual.Meat:
                return GetMeat();
            case FoodVisual.Minion:
                return GetMinion();
            default:
                return GetFood();
        }
    }

    static Sprite LoadSprite(ref Sprite cache, string resourcePath)
    {
        if (cache != null)
            return cache;

        cache = Resources.Load<Sprite>(resourcePath);
        if (cache != null)
            return cache;

        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        cache = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(texture.width, texture.height));
        return cache;
    }

    static Sprite LoadFirst(ref Sprite cache, string folder)
    {
        if (cache != null)
            return cache;

        var sprites = Resources.LoadAll<Sprite>(folder);
        if (sprites.Length > 0)
        {
            cache = sprites.OrderBy(s => s.name).First();
            return cache;
        }

        var textures = Resources.LoadAll<Texture2D>(folder);
        if (textures.Length > 0)
        {
            var texture = textures.OrderBy(t => t.name).First();
            cache = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(texture.width, texture.height));
            return cache;
        }

        return null;
    }

    static Sprite CreateWhiteSquare()
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
    }
}
