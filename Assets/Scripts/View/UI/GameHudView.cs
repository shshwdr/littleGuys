using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameHudView : MonoBehaviour
{
    TMP_Text goldText;
    GameModel model;

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

        var endButtonGo = new GameObject("EndLevelButton");
        endButtonGo.transform.SetParent(canvasGo.transform, false);
        var endButtonRect = endButtonGo.AddComponent<RectTransform>();
        endButtonRect.anchorMin = new Vector2(1f, 1f);
        endButtonRect.anchorMax = new Vector2(1f, 1f);
        endButtonRect.pivot = new Vector2(1f, 1f);
        endButtonRect.anchoredPosition = new Vector2(-20f, -20f);
        endButtonRect.sizeDelta = new Vector2(140f, 40f);
        var endButtonImage = endButtonGo.AddComponent<Image>();
        endButtonImage.color = new Color(0.7f, 0.25f, 0.25f, 1f);
        var endButton = endButtonGo.AddComponent<Button>();

        var endButtonLabel = WorldUiFactory.CreateText(
            endButtonGo.transform,
            "Label",
            "End Level",
            Vector2.zero,
            22f,
            TextAlignmentOptions.Center);
        endButtonLabel.rectTransform.sizeDelta = new Vector2(140f, 40f);

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
    }
}
