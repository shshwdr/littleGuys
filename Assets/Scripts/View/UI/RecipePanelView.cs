using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class RecipePanelView : MonoBehaviour
{
    public void Setup(
        GameModel model,
        ProductionService productionService,
        RecipeData soupRecipe,
        RecipeData stirFryRecipe,
        CompositeDisposable disposables)
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
        panelRect.sizeDelta = new Vector2(520f, 40f);

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        WorldUiFactory.CreateText(panelGo.transform, "Title", "Add Order", new Vector2(-190f, 0f), 22f, TextAlignmentOptions.MidlineLeft);

        CreateRecipeButton(panelGo.transform, "SoupButton", "Soup", new Vector2(-40f, 0f), soupRecipe, productionService, disposables);
        CreateRecipeButton(panelGo.transform, "StirFryButton", "Stir Fry", new Vector2(150f, 0f), stirFryRecipe, productionService, disposables);

        var queueText = WorldUiFactory.CreateText(canvasGo.transform, "Queue", "Queue: 0", Vector2.zero, 18f, TextAlignmentOptions.Center);
        queueText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        queueText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        queueText.rectTransform.anchoredPosition = new Vector2(0f, 72f);

        Observable.EveryUpdate()
            .Subscribe(_ =>
            {
                int orderCount = model.ProductionOrders.Count;
                int chopQ = model.GetZone(ZoneType.Chop).TaskQueue.Count;
                int cookQ = model.GetZone(ZoneType.Cook).TaskQueue.Count;
                int wokQ = model.GetZone(ZoneType.Wok).TaskQueue.Count;
                int plateQ = model.GetZone(ZoneType.Plate).TaskQueue.Count;
                queueText.text = $"Orders: {orderCount} | Chop:{chopQ} Cook:{cookQ} Wok:{wokQ} Plate:{plateQ}";
            })
            .AddTo(disposables);
    }

    static void CreateRecipeButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        RecipeData recipe,
        ProductionService productionService,
        CompositeDisposable disposables)
    {
        var buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(parent, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(140f, 28f);
        buttonGo.AddComponent<Image>().color = new Color(0.25f, 0.45f, 0.8f, 1f);
        var button = buttonGo.AddComponent<Button>();

        var text = WorldUiFactory.CreateText(buttonGo.transform, "Label", label, Vector2.zero, 22f, TextAlignmentOptions.Center);
        text.rectTransform.sizeDelta = new Vector2(140f, 28f);

        button.OnClickAsObservable()
            .Subscribe(_ => productionService.EnqueueRecipe(recipe.Id))
            .AddTo(disposables);
    }
}
