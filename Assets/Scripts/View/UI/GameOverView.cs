using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    [Header("Panels (scene-built under HUD)")]
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] GameObject gameCompletePanel;

    [Header("Continue")]
    [SerializeField] Button gameOverContinueButton;
    [SerializeField] Button gameCompleteContinueButton;

    GameModel model;
    GameBootstrap bootstrap;
    Action pendingContinue;

    public void Setup(GameModel gameModel, GameBootstrap gameBootstrap, CompositeDisposable disposables)
    {
        model = gameModel;
        bootstrap = gameBootstrap;

        BindContinueButton(gameOverContinueButton);
        BindContinueButton(gameCompleteContinueButton);
        Hide();

        model.State
            .Subscribe(state =>
            {
                switch (state)
                {
                    case GameState.TimeOut:
                        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/UI/sfx_ui_time_out");
                        ShowPanel(gameOverPanel, () => bootstrap.EnterUpgradeMode(string.Empty));
                        break;
                    case GameState.GameOver:
                        ShowPanel(gameOverPanel, () => bootstrap.EnterUpgradeMode(string.Empty));
                        break;
                    case GameState.LevelComplete:
                        ShowGameComplete();
                        break;
                    default:
                        Hide();
                        break;
                }
            })
            .AddTo(disposables);
    }

    void BindContinueButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(OnContinueClicked);
        button.onClick.AddListener(OnContinueClicked);
    }

    void ShowGameComplete()
    {
        var meta = MetaSaveService.Load();
        var nextScene = CSVLoader.GetScene(meta.CurrentScene);

        if (nextScene == null)
        {
            ShowPanel(gameCompletePanel, () => bootstrap.EnterUpgradeMode("All levels complete!"));
            return;
        }

        string sceneLabel = string.IsNullOrEmpty(nextScene.name)
            ? $"Level {meta.CurrentScene}"
            : nextScene.name;

        ShowPanel(gameCompletePanel, () => bootstrap.EnterUpgradeMode($"Entered next level: {sceneLabel}"));
    }

    void ShowPanel(GameObject panel, Action onContinue)
    {
        Hide();
        pendingContinue = onContinue;
        if (panel != null)
            panel.SetActive(true);
    }

    void Hide()
    {
        pendingContinue = null;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (gameCompletePanel != null)
            gameCompletePanel.SetActive(false);
    }

    void OnContinueClicked()
    {
        var action = pendingContinue;
        pendingContinue = null;
        action?.Invoke();
        Hide();
    }
}
