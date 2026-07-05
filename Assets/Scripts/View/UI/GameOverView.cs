using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    GameObject overlayRoot;
    TMP_Text titleText;
    TMP_Text subtitleText;
    Button continueButton;
    TMP_Text continueButtonLabel;
    GameModel model;
    GameBootstrap bootstrap;
    Action pendingContinue;

    public void Setup(GameModel gameModel, GameBootstrap gameBootstrap, CompositeDisposable disposables)
    {
        model = gameModel;
        bootstrap = gameBootstrap;
        BuildUi();

        model.State
            .Subscribe(state =>
            {
                switch (state)
                {
                    case GameState.TimeOut:
                        Show("Time Out", string.Empty, "Continue", () => bootstrap.EnterUpgradeMode(string.Empty));
                        break;
                    case GameState.GameOver:
                        Show("Game Over", string.Empty, "Continue", () => bootstrap.EnterUpgradeMode(string.Empty));
                        break;
                    case GameState.LevelComplete:
                        ShowLevelComplete();
                        break;
                    default:
                        Hide();
                        break;
                }
            })
            .AddTo(disposables);
    }

    void ShowLevelComplete()
    {
        var meta = MetaSaveService.Load();
        var nextScene = CSVLoader.GetScene(meta.CurrentScene);

        if (nextScene == null)
        {
            Show(
                "All Levels Complete!",
                "Congratulations! You have cleared all levels.",
                "Continue",
                () => bootstrap.EnterUpgradeMode("All levels complete!"));
            return;
        }

        string sceneLabel = string.IsNullOrEmpty(nextScene.name)
            ? $"Level {meta.CurrentScene}"
            : nextScene.name;

        Show(
            "Level Complete!",
            $"You have entered the next level: {sceneLabel}",
            "Continue",
            () => bootstrap.EnterUpgradeMode($"Entered next level: {sceneLabel}"));
    }

    void BuildUi()
    {
        overlayRoot = new GameObject("ResultOverlay");
        overlayRoot.transform.SetParent(transform, false);

        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        overlayRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        overlayRoot.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(overlayRoot.transform, false);
        var dimRect = dimGo.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImage = dimGo.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.65f);
        dimImage.raycastTarget = true;

        titleText = WorldUiFactory.CreateText(
            overlayRoot.transform,
            "Title",
            string.Empty,
            new Vector2(0f, 60f),
            48f,
            TextAlignmentOptions.Center);
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.sizeDelta = new Vector2(640f, 80f);

        subtitleText = WorldUiFactory.CreateText(
            overlayRoot.transform,
            "Subtitle",
            string.Empty,
            new Vector2(0f, 10f),
            24f,
            TextAlignmentOptions.Center);
        subtitleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        subtitleText.rectTransform.sizeDelta = new Vector2(640f, 60f);

        continueButton = WorldUiFactory.CreateButton(
            overlayRoot.transform,
            "Continue",
            "Continue",
            new Vector2(0f, -50f),
            new Vector2(180f, 40f));
        var continueRect = continueButton.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.5f, 0.5f);
        continueRect.anchorMax = new Vector2(0.5f, 0.5f);
        continueRect.pivot = new Vector2(0.5f, 0.5f);
        continueButtonLabel = continueButton.GetComponentInChildren<TMP_Text>();
        continueButton.onClick.AddListener(OnContinueClicked);

        Hide();
    }

    void Show(string title, string subtitle, string buttonLabel, Action onContinue)
    {
        pendingContinue = onContinue;
        titleText.text = title;
        subtitleText.text = subtitle ?? string.Empty;
        subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));

        if (continueButtonLabel != null)
            continueButtonLabel.text = buttonLabel;

        overlayRoot.SetActive(true);
    }

    void Hide()
    {
        pendingContinue = null;
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    void OnContinueClicked()
    {
        var action = pendingContinue;
        pendingContinue = null;
        action?.Invoke();
        Hide();
    }
}
