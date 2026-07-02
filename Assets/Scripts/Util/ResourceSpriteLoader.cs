using System.Linq;
using UnityEngine;

public static class ResourceSpriteLoader
{
    static Sprite customerSprite;
    static Sprite foodSprite;
    static Sprite minionSprite;

    public static Sprite GetCustomer() => LoadFirst(ref customerSprite, "customer");
    public static Sprite GetFood() => LoadFirst(ref foodSprite, "food");
    public static Sprite GetMinion() => LoadFirst(ref minionSprite, "minion");

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

        cache = ColorSpriteFactory.GetSquare();
        return cache;
    }
}
