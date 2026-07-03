using UnityEngine;

public static class ColorSpriteFactory
{
    public static Sprite GetSquare() => ResourceSpriteLoader.GetSquare();

    public static SpriteRenderer CreateSquare(string name, Transform parent, Color color, Vector2 size)
    {
        return CreateSprite(name, parent, GetSquare(), color, size);
    }

    public static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : GetSquare();
        renderer.color = color;
        return renderer;
    }
}
