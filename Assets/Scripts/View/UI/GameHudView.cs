using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameHudView : MonoBehaviour
{
    readonly (string label, float scale)[] UnlockedSpeedOptions =
    {
        ("Pause", 0f),
        ("1x", 1f),
        ("2x", 2f)
    };

    readonly Dictionary<float, Image> speedButtonImages = new Dictionary<float, Image>();

    TMP_Text goldText;
    TMP_Text primaryButtonLabel;
    Image primaryButtonImage;
    Image progressFill;
    TMP_Text progressLabel;
    Image timerFill;
    TMP_Text timerLabel;
    GameObject progressPanel;
    GameObject timerPanel;
    GameObject speedPanel;
    bool speedPanelPermanentlyUnlocked;
    GameModel model;
    System.Action onPrimaryClicked;
    float currentSpeed = 1f;
    bool upgradeMode;
    int currentSceneId;
    string sceneStartLabel = "Start Game";

    public void Setup(
        GameModel gameModel,
        CompositeDisposable disposables,
        bool speedUpUnlocked,
        System.Action primaryButtonClicked,
        int sceneId = 0)
    {
        model = gameModel;
        onPrimaryClicked = primaryButtonClicked;
        currentSceneId = sceneId;

        var canvasGo = new GameObject("HudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
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

        CreateSpeedPanel(canvasGo.transform, disposables, UnlockedSpeedOptions);
        speedPanelPermanentlyUnlocked = speedUpUnlocked;
        if (speedPanel != null)
            speedPanel.SetActive(speedUpUnlocked);

        CreateProgressPanel(canvasGo.transform);
        CreateTimerPanel(canvasGo.transform);

        var primaryButtonGo = new GameObject("PrimaryActionButton");
        primaryButtonGo.transform.SetParent(canvasGo.transform, false);
        var primaryButtonRect = primaryButtonGo.AddComponent<RectTransform>();
        primaryButtonRect.anchorMin = new Vector2(1f, 1f);
        primaryButtonRect.anchorMax = new Vector2(1f, 1f);
        primaryButtonRect.pivot = new Vector2(1f, 1f);
        primaryButtonRect.anchoredPosition = new Vector2(-20f, -20f);
        primaryButtonRect.sizeDelta = new Vector2(80f, 20f);
        primaryButtonImage = primaryButtonGo.AddComponent<Image>();
        var primaryButton = primaryButtonGo.AddComponent<Button>();

        primaryButtonLabel = WorldUiFactory.CreateText(
            primaryButtonGo.transform,
            "Label",
            "End Level",
            Vector2.zero,
            11f,
            TextAlignmentOptions.Center);
        primaryButtonLabel.rectTransform.sizeDelta = new Vector2(80f, 20f);

        primaryButton.OnClickAsObservable()
            .Subscribe(_ => onPrimaryClicked?.Invoke())
            .AddTo(disposables);

        model.Gold
            .Subscribe(gold => RefreshGoldText(gold))
            .AddTo(disposables);

        model.SceneProgressChanged
            .Subscribe(_ => RefreshProgressBar())
            .AddTo(disposables);

        model.BossFightChanged
            .Subscribe(_ => RefreshProgressBar())
            .AddTo(disposables);

        model.LevelTimeChanged
            .Subscribe(time => RefreshTimerBar(time))
            .AddTo(disposables);

        UpdateSceneDisplay(currentSceneId);
        SetUpgradeMode(false);
        SetGameSpeed(1f);
    }

    public void UpdateSceneDisplay(int sceneId)
    {
        currentSceneId = sceneId;
        var scene = CSVLoader.GetScene(sceneId);
        sceneStartLabel = scene != null && !string.IsNullOrEmpty(scene.name)
            ? $"Start {scene.name}"
            : "Start Game";

        if (upgradeMode && primaryButtonLabel != null)
            primaryButtonLabel.text = sceneStartLabel;

        RefreshProgressBar();
        RefreshTimerBar(model != null ? model.LevelTimeRemaining : 0f);
    }

    void CreateProgressPanel(Transform parent)
    {
        progressPanel = new GameObject("ProgressPanel");
        progressPanel.transform.SetParent(parent, false);
        var panelRect = progressPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -20f);
        panelRect.sizeDelta = new Vector2(320f, 12f);

        var panelBg = progressPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);

        progressFill = WorldUiFactory.CreateFillBar(
            progressPanel.transform,
            "Progress",
            Vector2.zero,
            new Vector2(320f, 12f),
            new Color(0.3f, 0.7f, 0.95f, 1f));

        progressLabel = WorldUiFactory.CreateText(
            progressPanel.transform,
            "ProgressLabel",
            "0/6",
            Vector2.zero,
            11f,
            TextAlignmentOptions.Center);
        progressLabel.rectTransform.anchorMin = Vector2.zero;
        progressLabel.rectTransform.anchorMax = Vector2.one;
        progressLabel.rectTransform.offsetMin = Vector2.zero;
        progressLabel.rectTransform.offsetMax = Vector2.zero;
    }

    void CreateTimerPanel(Transform parent)
    {
        timerPanel = new GameObject("TimerPanel");
        timerPanel.transform.SetParent(parent, false);
        var panelRect = timerPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -34f);
        panelRect.sizeDelta = new Vector2(320f, 10f);

        var panelBg = timerPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);

        timerFill = WorldUiFactory.CreateFillBar(
            timerPanel.transform,
            "Timer",
            Vector2.zero,
            new Vector2(320f, 10f),
            new Color(0.95f, 0.55f, 0.2f, 1f));

        timerLabel = WorldUiFactory.CreateText(
            timerPanel.transform,
            "TimerLabel",
            "120s",
            Vector2.zero,
            10f,
            TextAlignmentOptions.Center);
        timerLabel.rectTransform.anchorMin = Vector2.zero;
        timerLabel.rectTransform.anchorMax = Vector2.one;
        timerLabel.rectTransform.offsetMin = Vector2.zero;
        timerLabel.rectTransform.offsetMax = Vector2.zero;
    }

    void RefreshProgressBar()
    {
        if (model == null || progressFill == null || progressLabel == null)
            return;

        if (upgradeMode)
        {
            progressFill.fillAmount = 0f;
            progressLabel.text = string.Empty;
            return;
        }

        var scene = CSVLoader.GetScene(model.CurrentSceneId);
        int sceneFull = scene != null ? scene.full : 6;
        bool bossFight = model.BossHasSpawned || model.InBossFight;

        if (bossFight)
        {
            progressFill.fillAmount = 1f;
            progressLabel.text = "Boss Fight!";
            return;
        }

        progressFill.fillAmount = sceneFull > 0
            ? Mathf.Clamp01((float)model.SceneProgress / sceneFull)
            : 0f;
        progressLabel.text = $"{model.SceneProgress}/{sceneFull}";
    }

    void RefreshTimerBar(float remainingSeconds)
    {
        if (timerFill == null || timerLabel == null || model == null)
            return;

        if (upgradeMode)
        {
            timerFill.fillAmount = 0f;
            timerLabel.text = string.Empty;
            return;
        }

        float total = model.Config != null ? model.Config.levelTimeSeconds : 120f;
        timerFill.fillAmount = total > 0f ? Mathf.Clamp01(remainingSeconds / total) : 0f;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
        timerLabel.text = $"{seconds}s";
    }

    public void SetUpgradeMode(bool isUpgradeMode)
    {
        upgradeMode = isUpgradeMode;
        primaryButtonLabel.text = isUpgradeMode ? sceneStartLabel : "End Level";
        primaryButtonImage.color = isUpgradeMode
            ? new Color(0.2f, 0.65f, 0.35f, 1f)
            : new Color(0.7f, 0.25f, 0.25f, 1f);

        if (progressPanel != null)
            progressPanel.SetActive(!isUpgradeMode);
        if (timerPanel != null)
            timerPanel.SetActive(!isUpgradeMode);
        if (speedPanel != null)
        {
            if (isUpgradeMode)
                speedPanel.SetActive(false);
            else
                speedPanel.SetActive(speedPanelPermanentlyUnlocked);
        }

        RefreshGoldText(model != null ? model.Gold.Value : 0);
        RefreshProgressBar();
        RefreshTimerBar(model != null ? model.LevelTimeRemaining : 0f);
    }

    void RefreshGoldText(int runGold)
    {
        if (goldText == null)
            return;

        if (upgradeMode)
        {
            var meta = MetaSaveService.Load();
            goldText.text = $"Meta Gold: {meta.MetaGold}";
            return;
        }

        goldText.text = $"Gold: {runGold}";
    }

    public void ToggleSpeedPanelCheat()
    {
        if (speedPanel == null || speedPanelPermanentlyUnlocked || upgradeMode)
            return;

        speedPanel.SetActive(!speedPanel.activeSelf);
    }

    void CreateSpeedPanel(
        Transform parent,
        CompositeDisposable disposables,
        (string label, float scale)[] speedOptions)
    {
        var panelGo = new GameObject("SpeedPanel");
        speedPanel = panelGo;
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(20f, 20f);
        const float buttonWidth = 32f;
        const float buttonGap = 4f;
        const float padding = 6f;
        float panelWidth = padding * 2f + speedOptions.Length * buttonWidth + (speedOptions.Length - 1) * buttonGap;
        panelRect.sizeDelta = new Vector2(panelWidth, 20f);

        var panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < speedOptions.Length; i++)
        {
            var option = speedOptions[i];
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
