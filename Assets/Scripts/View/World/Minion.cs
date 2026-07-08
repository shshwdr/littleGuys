using UnityEngine;

public class Minion : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;

    void Awake()
    {
        EnsureRenderer();
        ApplyDefaultSprite();
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

    void ApplyDefaultSprite()
    {
        if (spriteRenderer == null)
            return;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = ResourceSpriteLoader.GetMinion();
    }
}
