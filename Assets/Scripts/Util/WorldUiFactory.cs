using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class WorldUiFactory
{
    public static Canvas CreateWorldCanvas(Transform parent, Vector3 localPosition, Vector2 size)
    {
        var canvasGo = new GameObject("WorldCanvas");
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = localPosition;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.localScale = Vector3.one * 0.01f;

        return canvas;
    }

    static Sprite squareSprite;

    static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
            squareSprite = ResourceSpriteLoader.GetSquare();
        return squareSprite;
    }

    public static Image CreateFillBar(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color fillColor)
    {
        var sprite = GetSquareSprite();

        var bgGo = new GameObject(name + "_Bg");
        bgGo.transform.SetParent(parent, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.sizeDelta = size;
        bgRect.anchoredPosition = anchoredPos;
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.sprite = sprite;
        bgImage.type = Image.Type.Simple;
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        bgImage.raycastTarget = false;

        var fillGo = new GameObject(name + "_Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = sprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        return fillImage;
    }

    public static TMP_Text CreateText(Transform parent, string name, string text, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 40f);
        rect.anchoredPosition = anchoredPos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 0.95f);

        var button = go.AddComponent<Button>();

        var text = CreateText(go.transform, "Label", label, Vector2.zero, 24f, TextAlignmentOptions.Center);
        text.rectTransform.sizeDelta = size;
        text.raycastTarget = true;

        return button;
    }
}
