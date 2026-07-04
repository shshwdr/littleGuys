using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class RecipePanelView : MonoBehaviour
{
    readonly Dictionary<string, Button> recipeButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, Image> recipeButtonImages = new Dictionary<string, Image>();
    TMP_Text queueText;
    GameModel model;
    ProductionService productionService;
    CompositeDisposable disposables;

    public void Setup(GameModel gameModel, ProductionService production, CompositeDisposable bindDisposables)
    {
        model = gameModel;
        productionService = production;
        disposables = bindDisposables;

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
        panelRect.sizeDelta = new Vector2(620f, 40f);

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        WorldUiFactory.CreateText(panelGo.transform, "Title", "Activate", new Vector2(-250f, 0f), 22f, TextAlignmentOptions.MidlineLeft);

        queueText = WorldUiFactory.CreateText(canvasGo.transform, "Queue", "Queue: 0", Vector2.zero, 18f, TextAlignmentOptions.Center);
        queueText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        queueText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        queueText.rectTransform.anchoredPosition = new Vector2(0f, 72f);

        model.RecipeUnlocked
            .Subscribe(recipeId => EnsureRecipeButton(panelGo.transform, recipeId))
            .AddTo(disposables);

        foreach (var recipeId in model.UnlockedRecipes)
            EnsureRecipeButton(panelGo.transform, recipeId);

        model.ActiveRecipeId
            .Subscribe(_ => RefreshActiveHighlights())
            .AddTo(disposables);

        Observable.EveryUpdate()
            .Subscribe(_ => RefreshQueueText())
            .AddTo(disposables);
    }

    void EnsureRecipeButton(Transform parent, string recipeId)
    {
        if (recipeButtons.ContainsKey(recipeId))
            return;

        var recipe = model.GetRecipe(recipeId);
        if (recipe == null)
            return;

        float x = recipeId == "vegsalad" ? -120f : recipeId == "vegsoup" ? 40f : 200f;
        var buttonGo = new GameObject(recipeId + "Button");
        buttonGo.transform.SetParent(parent, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(x, 0f);
        buttonRect.sizeDelta = new Vector2(150f, 28f);
        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        var button = buttonGo.AddComponent<Button>();

        var text = WorldUiFactory.CreateText(
            buttonGo.transform,
            "Label",
            recipe.DisplayName,
            Vector2.zero,
            20f,
            TextAlignmentOptions.Center);
        text.rectTransform.sizeDelta = new Vector2(150f, 28f);

        button.OnClickAsObservable()
            .Subscribe(_ => productionService.ActivateRecipe(recipeId))
            .AddTo(disposables);

        recipeButtons[recipeId] = button;
        recipeButtonImages[recipeId] = image;
        RefreshActiveHighlights();
    }

    void RefreshActiveHighlights()
    {
        string activeId = model.ActiveRecipeId.Value;
        foreach (var pair in recipeButtonImages)
        {
            bool isActive = pair.Key == activeId;
            pair.Value.color = isActive
                ? new Color(0.95f, 0.75f, 0.2f, 1f)
                : new Color(0.25f, 0.45f, 0.8f, 1f);
        }
    }

    void RefreshQueueText()
    {
        if (queueText == null || model == null)
            return;

        string activeName = "None";
        string activeId = model.ActiveRecipeId.Value;
        if (!string.IsNullOrEmpty(activeId))
        {
            var recipe = model.GetRecipe(activeId);
            if (recipe != null)
                activeName = recipe.DisplayName;
        }

        int orderCount = model.ProductionOrders.Count;
        queueText.text = $"Active: {activeName} | In progress: {orderCount}";
    }
}
