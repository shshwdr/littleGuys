using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameHudView : MonoBehaviour
{
    static readonly (string label, float scale)[] SpeedOptions =
    {
        ("Pause", 0f),
        ("1x", 1f),
        ("2x", 2f),
        ("3x", 3f),
        ("5x", 5f)
    };

    readonly Dictionary<float, Image> speedButtonImages = new Dictionary<float, Image>();
    TMP_Text goldText;
    GameModel model;
    float currentSpeed = 1f;

    public void Setup(GameModel gameModel, CompositeDisposable disposables)
    {
        model = gameModel;

        var canvasGo = new GameObject("HudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var goldPanel = new GameObject("GoldPanel");
        goldPanel.transform.SetParent(canvasGo.transform, false);
        var goldRect = goldPanel.AddComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(0f, 1f);
        goldRect.anchorMax = new Vector2(0f, 1f);
        goldRect.pivot = new Vector2(0f, 1f);
        goldRect.anchoredPosition = new Vector2(20f, -20f);
        goldRect.sizeDelta = new Vector2(200f, 40f);
        var goldBg = goldPanel.AddComponent<Image>();
        goldBg.color = new Color(0f, 0f, 0f, 0.55f);

        goldText = WorldUiFactory.CreateText(
            goldPanel.transform,
            "Gold",
            "Gold: 0",
            Vector2.zero,
            24f,
            TextAlignmentOptions.MidlineLeft);
        goldText.rectTransform.anchorMin = Vector2.zero;
        goldText.rectTransform.anchorMax = Vector2.one;
        goldText.rectTransform.offsetMin = new Vector2(12f, 0f);
        goldText.rectTransform.offsetMax = new Vector2(-12f, 0f);

        CreateSpeedPanel(canvasGo.transform, disposables);

        var endButtonGo = new GameObject("EndLevelButton");
        endButtonGo.transform.SetParent(canvasGo.transform, false);
        var endButtonRect = endButtonGo.AddComponent<RectTransform>();
        endButtonRect.anchorMin = new Vector2(1f, 1f);
        endButtonRect.anchorMax = new Vector2(1f, 1f);
        endButtonRect.pivot = new Vector2(1f, 1f);
        endButtonRect.anchoredPosition = new Vector2(-20f, -20f);
        endButtonRect.sizeDelta = new Vector2(70f, 20f);
        var endButtonImage = endButtonGo.AddComponent<Image>();
        endButtonImage.color = new Color(0.7f, 0.25f, 0.25f, 1f);
        var endButton = endButtonGo.AddComponent<Button>();

        var endButtonLabel = WorldUiFactory.CreateText(
            endButtonGo.transform,
            "Label",
            "End Level",
            Vector2.zero,
            11f,
            TextAlignmentOptions.Center);
        endButtonLabel.rectTransform.sizeDelta = new Vector2(70f, 20f);

        endButton.OnClickAsObservable()
            .Subscribe(_ =>
            {
                if (model.State.Value == GameState.Playing)
                    model.State.Value = GameState.GameOver;
            })
            .AddTo(disposables);

        model.Gold
            .Subscribe(gold => goldText.text = $"Gold: {gold}")
            .AddTo(disposables);

        SetGameSpeed(1f);
    }

    void CreateSpeedPanel(Transform parent, CompositeDisposable disposables)
    {
        var panelGo = new GameObject("SpeedPanel");
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 20f);
        const float buttonWidth = 32f;
        const float buttonGap = 4f;
        const float padding = 6f;
        float panelWidth = padding * 2f + SpeedOptions.Length * buttonWidth + (SpeedOptions.Length - 1) * buttonGap;
        panelRect.sizeDelta = new Vector2(panelWidth, 20f);

        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            var option = SpeedOptions[i];
            float x = padding + i * (buttonWidth + buttonGap);
            var buttonGo = new GameObject(option.label + "Button");
            buttonGo.transform.SetParent(panelGo.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0.5f);
            buttonRect.anchorMax = new Vector2(0f, 0.5f);
            buttonRect.pivot = new Vector2(0f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(x, 0f);
            buttonRect.sizeDelta = new Vector2(buttonWidth, 15f);

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.8f, 1f);
            var button = buttonGo.AddComponent<Button>();

            var label = WorldUiFactory.CreateText(
                buttonGo.transform,
                "Label",
                option.label,
                Vector2.zero,
                9f,
                TextAlignmentOptions.Center);
            label.rectTransform.sizeDelta = new Vector2(buttonWidth, 15f);

            float speed = option.scale;
            speedButtonImages[speed] = image;
            button.OnClickAsObservable()
                .Subscribe(_ => SetGameSpeed(speed))
                .AddTo(disposables);
        }

        RefreshSpeedHighlights();
    }

    void SetGameSpeed(float speed)
    {
        currentSpeed = speed;
        Time.timeScale = speed;
        RefreshSpeedHighlights();
    }

    void RefreshSpeedHighlights()
    {
        foreach (var pair in speedButtonImages)
        {
            bool isActive = Mathf.Approximately(pair.Key, currentSpeed);
            pair.Value.color = isActive
                ? new Color(0.95f, 0.75f, 0.2f, 1f)
                : new Color(0.25f, 0.45f, 0.8f, 1f);
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
