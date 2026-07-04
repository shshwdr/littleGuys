using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeSceneView : MonoBehaviour
{
    const float DebugGoldAmount = 10f;

    readonly UpgradeId[] upgradeIds =
    {
        UpgradeId.InitialWorkers,
        UpgradeId.UnlockVegSoup,
        UpgradeId.UnlockSplitter,
        UpgradeId.MoveSpeed,
        UpgradeId.UnlockStirFry
    };

    MetaSaveData metaSave;
    TMP_Text goldText;
    readonly Button[] upgradeButtons = new Button[5];
    readonly TMP_Text[] upgradeLabels = new TMP_Text[5];
    readonly Image[] upgradeImages = new Image[5];
    Sprite buttonSprite;

    public void Setup(MetaSaveData save, CompositeDisposable disposables)
    {
        metaSave = save;
        buttonSprite = ResourceSpriteLoader.GetSquare();

        var canvasGo = CreateCanvasRoot();
        CreateBackground(canvasGo.transform);
        CreateTitle(canvasGo.transform);
        CreateGoldPanel(canvasGo.transform);
        CreateUpgradeButtons(canvasGo.transform, disposables);
        CreateStartButton(canvasGo.transform, disposables);

        Observable.EveryUpdate()
            .Where(_ => Input.GetKeyDown(KeyCode.G))
            .Subscribe(_ => AddDebugGold())
            .AddTo(disposables);

        RefreshAll();
    }

    GameObject CreateCanvasRoot()
    {
        var canvasGo = new GameObject("UpgradeCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvasGo;
    }

    void CreateBackground(Transform parent)
    {
        var go = new GameObject("Background");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        StretchFullScreen(rect);

        var image = go.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.1f, 0.1f, 0.14f, 1f);
    }

    void CreateTitle(Transform parent)
    {
        var title = CreateLabel(parent, "Title", "Upgrades", new Vector2(0f, 420f), new Vector2(800f, 72f), 52f);
        title.fontStyle = FontStyles.Bold;
    }

    void CreateGoldPanel(Transform parent)
    {
        var panelGo = new GameObject("GoldPanel");
        panelGo.transform.SetParent(parent, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        SetupCenterRect(panelRect, new Vector2(0f, 340f), new Vector2(420f, 56f));

        var panelImage = panelGo.AddComponent<Image>();
        panelImage.sprite = buttonSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = new Color(0f, 0f, 0f, 0.65f);

        goldText = CreateLabel(panelGo.transform, "Gold", FormatGoldText(), Vector2.zero, new Vector2(400f, 56f), 30f);
        goldText.fontStyle = FontStyles.Bold;
    }

    void CreateUpgradeButtons(Transform parent, CompositeDisposable disposables)
    {
        for (int i = 0; i < upgradeIds.Length; i++)
        {
            int index = i;
            var id = upgradeIds[i];

            var buttonGo = new GameObject("Upgrade_" + id);
            buttonGo.transform.SetParent(parent, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            SetupCenterRect(buttonRect, new Vector2(0f, 250f - index * 64f), new Vector2(760f, 56f));

            var image = buttonGo.AddComponent<Image>();
            image.sprite = buttonSprite;
            image.type = Image.Type.Simple;
            upgradeImages[i] = image;

            var button = buttonGo.AddComponent<Button>();
            upgradeButtons[i] = button;

            upgradeLabels[i] = CreateLabel(
                buttonGo.transform,
                "Label",
                BuildUpgradeLabel(id),
                Vector2.zero,
                new Vector2(740f, 56f),
                24f);

            button.onClick.AddListener(() => OnUpgradeClicked(upgradeIds[index]));
        }
    }

    void CreateStartButton(Transform parent, CompositeDisposable disposables)
    {
        var buttonGo = new GameObject("StartGameButton");
        buttonGo.transform.SetParent(parent, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        SetupCenterRect(buttonRect, new Vector2(0f, -360f), new Vector2(280f, 60f));

        var image = buttonGo.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.2f, 0.65f, 0.35f, 1f);

        var button = buttonGo.AddComponent<Button>();
        CreateLabel(buttonGo.transform, "Label", "Start Game", Vector2.zero, new Vector2(280f, 60f), 30f);

        button.OnClickAsObservable()
            .Subscribe(_ => SceneFlowService.LoadMainGame())
            .AddTo(disposables);
    }

    TMP_Text CreateLabel(
        Transform parent,
        string name,
        string text,
        Vector2 anchoredPos,
        Vector2 size,
        float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        SetupCenterRect(rect, anchoredPos, size);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetupCenterRect(RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }

    static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void AddDebugGold()
    {
        metaSave.MetaGold += (int)DebugGoldAmount;
        MetaSaveService.Save(metaSave);
        RefreshAll();
    }

    void OnUpgradeClicked(UpgradeId id)
    {
        if (!MetaSaveService.TryPurchase(metaSave, id))
            return;

        RefreshAll();
    }

    void RefreshAll()
    {
        goldText.text = FormatGoldText();

        for (int i = 0; i < upgradeIds.Length; i++)
            RefreshUpgradeButton(i);
    }

    void RefreshUpgradeButton(int index)
    {
        var id = upgradeIds[index];
        bool canBuy = MetaSaveService.CanPurchase(metaSave, id);
        bool maxed = metaSave.GetLevel(id) >= UpgradeDefinition.Get(id).MaxLevel;
        bool locked = IsSequenceLocked(id);

        upgradeLabels[index].text = BuildUpgradeLabel(id);
        upgradeButtons[index].interactable = canBuy;

        if (maxed)
            upgradeImages[index].color = new Color(0.35f, 0.35f, 0.35f, 1f);
        else if (locked)
            upgradeImages[index].color = new Color(0.22f, 0.22f, 0.22f, 1f);
        else if (canBuy)
            upgradeImages[index].color = new Color(0.25f, 0.45f, 0.8f, 1f);
        else
            upgradeImages[index].color = new Color(0.45f, 0.28f, 0.28f, 1f);
    }

    bool IsSequenceLocked(UpgradeId id)
    {
        return (int)id > 0 && metaSave.GetLevel((UpgradeId)((int)id - 1)) < 1;
    }

    string FormatGoldText()
    {
        return $"Gold: {metaSave.MetaGold}";
    }

    string BuildUpgradeLabel(UpgradeId id)
    {
        var def = UpgradeDefinition.Get(id);
        int level = metaSave.GetLevel(id);
        bool maxed = level >= def.MaxLevel;
        bool locked = (int)id > 0 && metaSave.GetLevel((UpgradeId)((int)id - 1)) < 1;

        if (locked)
            return $"{def.DisplayName}  ({level}/{def.MaxLevel})  —  Locked";

        if (maxed)
            return $"{def.DisplayName}  ({level}/{def.MaxLevel})  —  MAX";

        return $"{def.DisplayName}  ({level}/{def.MaxLevel})  —  {def.Price} Gold";
    }
}
