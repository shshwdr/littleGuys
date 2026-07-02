using UnityEngine;

public static class ColorSpriteFactory
{
    static Sprite squareSprite;

    public static Sprite GetSquare()
    {
        if (squareSprite != null)
            return squareSprite;

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

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
