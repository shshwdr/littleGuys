using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameHudView : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] TMP_Text goldText;

    [Header("Primary Action")]
    [SerializeField] Button primaryButton;
    [SerializeField] TMP_Text primaryButtonLabel;
    [SerializeField] Image primaryButtonImage;

    [Header("Progress")]
    [SerializeField] GameObject progressPanel;
    [SerializeField] Image progressFill;
    [SerializeField] TMP_Text progressLabel;

    [Header("Timer")]
    [SerializeField] GameObject timerPanel;
    [SerializeField] Image timerFill;
    [SerializeField] TMP_Text timerLabel;

    [Header("Speed")]
    [SerializeField] GameObject speedPanel;
    [SerializeField] Button pauseSpeedButton;
    [SerializeField] Button normalSpeedButton;
    [SerializeField] Button fastSpeedButton;
    [SerializeField] Image pauseSpeedButtonImage;
    [SerializeField] Image normalSpeedButtonImage;
    [SerializeField] Image fastSpeedButtonImage;

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
        speedPanelPermanentlyUnlocked = speedUpUnlocked;

        if (primaryButton != null)
        {
            primaryButton.OnClickAsObservable()
                .Subscribe(_ => onPrimaryClicked?.Invoke())
                .AddTo(disposables);
        }

        BindSpeedButton(pauseSpeedButton, 0f, disposables);
        BindSpeedButton(normalSpeedButton, 1f, disposables);
        BindSpeedButton(fastSpeedButton, 2f, disposables);

        if (speedPanel != null)
            speedPanel.SetActive(speedUpUnlocked);

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

    void BindSpeedButton(Button button, float speed, CompositeDisposable disposables)
    {
        if (button == null)
            return;

        button.OnClickAsObservable()
            .Subscribe(_ => SetGameSpeed(speed))
            .AddTo(disposables);
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

        if (primaryButtonLabel != null)
            primaryButtonLabel.text = isUpgradeMode ? sceneStartLabel : "End Level";

        if (primaryButtonImage != null)
        {
            primaryButtonImage.color = isUpgradeMode
                ? new Color(0.2f, 0.65f, 0.35f, 1f)
                : new Color(0.7f, 0.25f, 0.25f, 1f);
        }

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

    void SetGameSpeed(float speed)
    {
        currentSpeed = speed;
        Time.timeScale = speed;
        RefreshSpeedHighlights();
    }

    void RefreshSpeedHighlights()
    {
        SetSpeedHighlight(pauseSpeedButtonImage, Mathf.Approximately(currentSpeed, 0f));
        SetSpeedHighlight(normalSpeedButtonImage, Mathf.Approximately(currentSpeed, 1f));
        SetSpeedHighlight(fastSpeedButtonImage, Mathf.Approximately(currentSpeed, 2f));
    }

    static void SetSpeedHighlight(Image image, bool isActive)
    {
        if (image == null)
            return;

        image.color = isActive
            ? new Color(0.95f, 0.75f, 0.2f, 1f)
            : new Color(0.25f, 0.45f, 0.8f, 1f);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
