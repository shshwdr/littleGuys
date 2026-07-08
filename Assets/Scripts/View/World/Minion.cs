using System.Collections.Generic;
using UnityEngine;

public enum MinionAnimState
{
    Idle,
    Walk,
    Work
}

public class Minion : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] SpriteAnimPlayer animPlayer;
    [SerializeField] List<Sprite> idleSprites = new List<Sprite>();
    [SerializeField] List<Sprite> walkSprites = new List<Sprite>();
    [SerializeField] List<Sprite> workSprites = new List<Sprite>();

    MinionAnimState currentState = MinionAnimState.Idle;
    bool stateInitialized;

    void Awake()
    {
        EnsureRenderer();
        EnsureAnimPlayer();
        EnsureDefaultSprites();
        ApplyDefaultSprite();
        SetAnimState(MinionAnimState.Idle, force: true);
    }

    public SpriteRenderer GetRenderer()
    {
        EnsureRenderer();
        return spriteRenderer;
    }

    public void SetAnimState(MinionAnimState state, bool force = false)
    {
        if (stateInitialized && !force && state == currentState)
            return;

        currentState = state;
        stateInitialized = true;

        var frames = GetFrames(state);
        if (frames == null || frames.Count == 0)
            return;

        EnsureAnimPlayer();
        animPlayer?.PlayLoop(frames);
    }

    List<Sprite> GetFrames(MinionAnimState state)
    {
        switch (state)
        {
            case MinionAnimState.Walk:
                return walkSprites;
            case MinionAnimState.Work:
                return workSprites;
            default:
                return idleSprites;
        }
    }

    void EnsureRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void EnsureAnimPlayer()
    {
        if (animPlayer != null)
            return;

        EnsureRenderer();
        if (spriteRenderer == null)
            return;

        animPlayer = spriteRenderer.GetComponent<SpriteAnimPlayer>();
        if (animPlayer == null)
            animPlayer = spriteRenderer.gameObject.AddComponent<SpriteAnimPlayer>();
    }

    void EnsureDefaultSprites()
    {
        if (idleSprites == null || idleSprites.Count == 0)
            idleSprites = ResourceSpriteLoader.GetSpriteList("minion/minion_Idle");

        if (walkSprites == null || walkSprites.Count == 0)
            walkSprites = ResourceSpriteLoader.GetSpriteList("minion/minion_walk");

        if (workSprites == null || workSprites.Count == 0)
            workSprites = ResourceSpriteLoader.GetSpriteList("minion/minion_work");
    }

    void ApplyDefaultSprite()
    {
        if (spriteRenderer == null)
            return;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = ResourceSpriteLoader.GetMinion();
    }
}
