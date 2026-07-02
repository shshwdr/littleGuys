using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class RecipePanelView : MonoBehaviour
{
    public void Setup(GameModel model, RecipeService recipeService, RecipeData soupRecipe, CompositeDisposable disposables)
    {
        var canvasGo = new GameObject("RecipeCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 20f);
        panelRect.sizeDelta = new Vector2(420f, 40f);

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        WorldUiFactory.CreateText(panelGo.transform, "Title", "Select Recipe", new Vector2(-120f, 0f), 24f, TextAlignmentOptions.MidlineLeft);

        var buttonGo = new GameObject("SoupButton");
        buttonGo.transform.SetParent(panelGo.transform, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(120f, 0f);
        buttonRect.sizeDelta = new Vector2(160f, 28f);
        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        var button = buttonGo.AddComponent<Button>();

        var label = WorldUiFactory.CreateText(buttonGo.transform, "Label", "Soup", Vector2.zero, 26f, TextAlignmentOptions.Center);
        label.rectTransform.sizeDelta = new Vector2(160f, 28f);

        var statusText = WorldUiFactory.CreateText(panelGo.transform, "Status", "No recipe selected", new Vector2(0f, 28f), 18f, TextAlignmentOptions.Center);
        statusText.rectTransform.SetParent(canvasGo.transform, false);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 72f);

        button.OnClickAsObservable()
            .Subscribe(_ => recipeService.SelectRecipe(soupRecipe))
            .AddTo(disposables);

        model.ActiveRecipe
            .Subscribe(recipe =>
            {
                statusText.text = recipe == null ? "No recipe selected" : $"Active: {recipe.DisplayName}";
            })
            .AddTo(disposables);
    }
}
