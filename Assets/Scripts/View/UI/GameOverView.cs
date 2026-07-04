using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    GameObject panel;
    TMP_Text summaryText;
    bool goldSettled;

    public void Setup(GameModel model, CompositeDisposable disposables)
    {
        var canvasGo = new GameObject("GameOverCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 80f);
        titleRect.sizeDelta = new Vector2(600f, 80f);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            title.font = TMP_Settings.defaultFontAsset;
        title.text = "Game Over";
        title.fontSize = 64f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;

        var summaryGo = new GameObject("Summary");
        summaryGo.transform.SetParent(panel.transform, false);
        var summaryRect = summaryGo.AddComponent<RectTransform>();
        summaryRect.anchorMin = new Vector2(0.5f, 0.5f);
        summaryRect.anchorMax = new Vector2(0.5f, 0.5f);
        summaryRect.anchoredPosition = new Vector2(0f, 10f);
        summaryRect.sizeDelta = new Vector2(600f, 80f);
        summaryText = summaryGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            summaryText.font = TMP_Settings.defaultFontAsset;
        summaryText.text = string.Empty;
        summaryText.fontSize = 28f;
        summaryText.alignment = TextAlignmentOptions.Center;
        summaryText.color = Color.white;

        var buttonGo = new GameObject("GoToUpgradesButton");
        buttonGo.transform.SetParent(panel.transform, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -80f);
        buttonRect.sizeDelta = new Vector2(140f, 26f);
        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        var button = buttonGo.AddComponent<Button>();

        var buttonLabel = WorldUiFactory.CreateText(
            buttonGo.transform,
            "Label",
            "Go to Upgrades",
            Vector2.zero,
            13f,
            TextAlignmentOptions.Center);
        buttonLabel.rectTransform.sizeDelta = new Vector2(140f, 26f);

        button.OnClickAsObservable()
            .Subscribe(_ => SceneFlowService.LoadUpgradeScene())
            .AddTo(disposables);

        panel.SetActive(false);

        model.State
            .Subscribe(state =>
            {
                panel.SetActive(state == GameState.GameOver);
                if (state == GameState.GameOver)
                    SettleGold(model);
            })
            .AddTo(disposables);
    }

    void SettleGold(GameModel model)
    {
        if (goldSettled)
            return;

        goldSettled = true;
        int runGold = model.Gold.Value;
        var meta = MetaSaveService.Load();
        meta.MetaGold += runGold;
        MetaSaveService.Save(meta);
        summaryText.text = $"This run: +{runGold} Gold\nTotal: {meta.MetaGold} Gold";
    }
}
