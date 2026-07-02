using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameOverView : MonoBehaviour
{
    GameObject panel;

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

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panel.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(600f, 120f);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = "Game Over";
        tmp.fontSize = 64f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        panel.SetActive(false);

        model.State
            .Subscribe(state => panel.SetActive(state == GameState.GameOver))
            .AddTo(disposables);
    }
}
