using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a list of sprites in sequence on a SpriteRenderer.
/// frameTime controls how long each frame stays on screen.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimPlayer : MonoBehaviour
{
    [SerializeField] SpriteRenderer targetRenderer;
    [SerializeField] List<Sprite> spriteList = new List<Sprite>();
    [SerializeField] float frameTime = 0.1f;
    [SerializeField] bool loop = true;
    [SerializeField] bool playOnEnable = true;

    float timer;
    int frameIndex;
    bool playing;
    Action onComplete;

    public bool IsPlaying => playing;

    public float FrameTime
    {
        get => frameTime;
        set => frameTime = Mathf.Max(0.0001f, value);
    }

    void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
    }

    void Awake()
    {
        EnsureRenderer();
    }

    void OnEnable()
    {
        if (playOnEnable && HasFrames())
            Play(loop);
    }

    public void SetSprites(IList<Sprite> sprites)
    {
        spriteList.Clear();
        if (sprites != null)
            spriteList.AddRange(sprites);
    }

    public void PlayLoop(IList<Sprite> sprites)
    {
        SetSprites(sprites);
        Play(true);
    }

    public void PlayOnce(IList<Sprite> sprites, Action onDone = null)
    {
        SetSprites(sprites);
        Play(false, onDone);
    }

    public void Play(bool looping) => Play(looping, null);

    public void Play(bool looping, Action onDone)
    {
        loop = looping;
        onComplete = onDone;
        frameIndex = 0;
        timer = 0f;
        playing = HasFrames();
        ApplyFrame();
    }

    public void Stop()
    {
        playing = false;
        onComplete = null;
    }

    void Update()
    {
        if (!playing || !HasFrames())
            return;

        timer += Time.deltaTime;
        if (timer < frameTime)
            return;

        timer -= frameTime;
        frameIndex++;

        if (frameIndex >= spriteList.Count)
        {
            if (loop)
            {
                frameIndex = 0;
            }
            else
            {
                frameIndex = spriteList.Count - 1;
                ApplyFrame();
                playing = false;
                var callback = onComplete;
                onComplete = null;
                callback?.Invoke();
                return;
            }
        }

        ApplyFrame();
    }

    void ApplyFrame()
    {
        EnsureRenderer();
        if (targetRenderer == null || !HasFrames())
            return;

        int clamped = Mathf.Clamp(frameIndex, 0, spriteList.Count - 1);
        targetRenderer.sprite = spriteList[clamped];
    }

    void EnsureRenderer()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    bool HasFrames()
    {
        return spriteList != null && spriteList.Count > 0;
    }
}
