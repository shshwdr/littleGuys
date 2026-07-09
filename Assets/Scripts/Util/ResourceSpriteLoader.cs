using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ResourceSpriteLoader
{
    static Sprite customerSprite;
    static Sprite customerSilFallbackSprite;
    static readonly Dictionary<string, Sprite> customerSpritesById = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> customerSilSpritesById = new Dictionary<string, Sprite>();
    static Sprite foodSprite;
    static Sprite vegSprite;
    static Sprite meatSprite;
    static Sprite minionSprite;
    static Sprite squareSprite;
    static readonly Dictionary<string, Sprite> foodSpritesById = new Dictionary<string, Sprite>();

    public static Sprite GetCustomer() => LoadFirst(ref customerSprite, "customer");

    public static Sprite GetCustomer(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return GetCustomer();

        if (customerSpritesById.TryGetValue(identifier, out var cached) && cached != null)
            return cached;

        var sprite = LoadSpriteAtPath("customer/" + identifier) ?? GetCustomer();
        customerSpritesById[identifier] = sprite;
        return sprite;
    }

    public static Sprite GetCustomerSil(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return LoadFirstCustomerSil();

        if (customerSilSpritesById.TryGetValue(identifier, out var cached) && cached != null)
            return cached;

        var sprite = LoadSpriteAtPath("customerSil/" + identifier) ?? LoadFirstCustomerSil();
        customerSilSpritesById[identifier] = sprite;
        return sprite;
    }

    public static Sprite GetBossHead(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        return LoadSpriteAtPath("bossHead/" + identifier);
    }

    public static Sprite GetBossHeadAlt(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        return LoadSpriteAtPath("bossHead/" + identifier + "2")
            ?? LoadSpriteAtPath("bossHead/" + identifier);
    }

    static Sprite LoadFirstCustomerSil() => LoadFirst(ref customerSilFallbackSprite, "customerSil");

    public static Sprite GetFood() => LoadFirst(ref foodSprite, "food");

    // 按 identifier 从 Resources/food/{identifier} 加载食物图片，找不到则回退到默认食物图。
    public static Sprite GetFoodById(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        if (foodSpritesById.TryGetValue(identifier, out var cached))
            return cached;

        var sprite = LoadSpriteAtPath("food/" + identifier);
        foodSpritesById[identifier] = sprite;
        return sprite;
    }
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

    public static List<Sprite> GetSpriteList(string folder)
    {
        var result = new List<Sprite>();

        var sprites = Resources.LoadAll<Sprite>(folder);
        if (sprites != null && sprites.Length > 0)
        {
            result.AddRange(sprites.OrderBy(s => s.name));
            return result;
        }

        var textures = Resources.LoadAll<Texture2D>(folder);
        if (textures != null && textures.Length > 0)
        {
            foreach (var texture in textures.OrderBy(t => t.name))
            {
                result.Add(Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    Mathf.Max(texture.width, texture.height)));
            }
        }

        return result;
    }

    static Sprite LoadSprite(ref Sprite cache, string resourcePath)
    {
        if (cache != null)
            return cache;

        cache = LoadSpriteAtPath(resourcePath);
        return cache;
    }

    static Sprite LoadSpriteAtPath(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(texture.width, texture.height));
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
