using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanelView : MonoBehaviour
{
    const string UpgradeCellPrefabPath = "prefab/upgradeCell";
    const float DebugGoldAmount = 10f;
    const float DefaultButtonSize = 160f;

    MetaSaveData metaSave;
    TMP_Text summaryText;
    Sprite buttonSprite;
    GameObject upgradeCellPrefab;
    RectTransform treeRoot;
    RectTransform lineRoot;
    RectTransform treeViewport;
    UpgradeTreePanZoom panZoom;
    CompositeDisposable disposables;
    bool built;
    System.Action onMetaGoldChanged;
    float scrollSensitivity = 0.35f;
    float buttonSize = DefaultButtonSize;

    readonly Dictionary<string, UpgradeNodeView> nodeViews = new Dictionary<string, UpgradeNodeView>();

    class UpgradeNodeView
    {
        public string Id;
        public Button Button;
        public Image Image;
        public TMP_Text Label;
    }

    public void Setup(
        MetaSaveData save,
        CompositeDisposable viewDisposables,
        System.Action metaGoldChanged = null,
        float treeScrollSensitivity = 0.35f)
    {
        metaSave = save;
        disposables = viewDisposables;
        onMetaGoldChanged = metaGoldChanged;
        scrollSensitivity = Mathf.Max(0.01f, treeScrollSensitivity);
    }

    public void EnsureBuilt()
    {
        if (built)
            return;

        built = true;
        CSVLoader.Init();
        buttonSprite = ResourceSpriteLoader.GetSquare();
        upgradeCellPrefab = Resources.Load<GameObject>(UpgradeCellPrefabPath);
        if (upgradeCellPrefab == null)
            Debug.LogError($"UpgradePanelView: prefab not found at Resources/{UpgradeCellPrefabPath}.");
        else
        {
            var prefabRect = upgradeCellPrefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                float size = Mathf.Max(prefabRect.sizeDelta.x, prefabRect.sizeDelta.y);
                if (size > 0.01f)
                    buttonSize = size;
            }
        }

        // upgradeRoot 本身就是 Canvas，内容直接挂在下面。
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        var panelRect = (RectTransform)panel.transform;
        StretchFullScreen(panelRect);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.14f, 1f);

        CreateTree(panel.transform);

        var title = CreateLabel(panel.transform, "Title", "Upgrades", new Vector2(0f, 420f), new Vector2(800f, 72f), 52f);
        title.fontStyle = FontStyles.Bold;

        summaryText = CreateLabel(panel.transform, "Summary", string.Empty, new Vector2(0f, 320f), new Vector2(600f, 56f), 22f);

        Observable.EveryUpdate()
            .Where(_ => gameObject.activeInHierarchy && Input.GetKeyDown(KeyCode.G))
            .Subscribe(_ => AddDebugGold())
            .AddTo(disposables);

        RefreshAll();
    }

    public void SetSummary(string text)
    {
        if (summaryText != null)
            summaryText.text = text;
    }

    public void OnShown()
    {
        EnsureBuilt();
        panZoom?.ResetView();
        ReloadSave();
    }

    void CreateTree(Transform parent)
    {
        var scrollGo = new GameObject("UpgradeTreeScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        var scrollRect = (RectTransform)scrollGo.transform;
        StretchFullScreen(scrollRect);

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        treeViewport = viewportGo.AddComponent<RectTransform>();
        StretchFullScreen(treeViewport);
        viewportGo.AddComponent<RectMask2D>();
        viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

        var treeGo = new GameObject("TreeRoot");
        treeGo.transform.SetParent(treeViewport, false);
        treeRoot = treeGo.AddComponent<RectTransform>();
        SetupCenterRect(treeRoot, Vector2.zero, new Vector2(1600f, 500f));

        var lineGo = new GameObject("Lines");
        lineGo.transform.SetParent(treeRoot, false);
        lineRoot = lineGo.AddComponent<RectTransform>();
        SetupCenterRect(lineRoot, Vector2.zero, new Vector2(1600f, 500f));

        var positions = BuildLayoutPositions();
        foreach (var pair in positions)
        {
            try
            {
                CreateUpgradeNode(pair.Key, pair.Value);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UpgradePanelView: failed to draw upgrade '{pair.Key}'. {ex.Message}\n{ex.StackTrace}");
            }
        }

        foreach (var info in CSVLoader.GetAll())
        {
            if (string.IsNullOrEmpty(info.prev))
                continue;
            if (!info.IsVisible())
                continue;
            if (!nodeViews.ContainsKey(info.prev) || !nodeViews.ContainsKey(info.identifier))
            {
                Debug.LogError(
                    $"UpgradePanelView: failed to draw connection '{info.prev}' -> '{info.identifier}' because at least one node was not drawn.");
                continue;
            }

            if (!positions.TryGetValue(info.prev, out var from)
                || !positions.TryGetValue(info.identifier, out var to))
            {
                Debug.LogError(
                    $"UpgradePanelView: failed to draw connection '{info.prev}' -> '{info.identifier}' because layout position is missing.");
                continue;
            }

            CreateConnectionLine(from, to);
        }

        panZoom = viewportGo.AddComponent<UpgradeTreePanZoom>();
        panZoom.Setup(treeViewport, treeRoot, scrollSensitivity);
    }

    Dictionary<string, Vector2> BuildLayoutPositions()
    {
        UpgradeTreeLayout.TryBuild(buttonSize * 2f, out var positions);
        return positions;
    }

    void CreateUpgradeNode(string identifier, Vector2 position)
    {
        if (treeRoot == null)
        {
            Debug.LogError($"UpgradePanelView: failed to draw upgrade '{identifier}' because treeRoot is null.");
            return;
        }

        var info = CSVLoader.Get(identifier);
        if (info == null)
        {
            Debug.LogError($"UpgradePanelView: failed to draw upgrade '{identifier}' because CSV data was not found.");
            return;
        }
        if (!info.IsVisible())
            return;

        if (upgradeCellPrefab == null)
        {
            Debug.LogError($"UpgradePanelView: failed to draw upgrade '{identifier}' because upgradeCell prefab is missing.");
            return;
        }

        var buttonGo = Instantiate(upgradeCellPrefab, treeRoot);
        buttonGo.name = "Upgrade_" + identifier;

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        if (buttonRect == null)
            buttonRect = buttonGo.AddComponent<RectTransform>();

        Vector2 size = buttonRect.sizeDelta;
        if (size.sqrMagnitude < 0.01f)
            size = new Vector2(buttonSize, buttonSize);
        SetupCenterRect(buttonRect, position, size);

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
            button = buttonGo.GetComponentInChildren<Button>(true);

        Image image = null;
        if (button != null && button.targetGraphic is Image targetImage)
            image = targetImage;
        if (image == null)
            image = buttonGo.GetComponentInChildren<Image>(true);

        var label = buttonGo.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = BuildUpgradeLabel(info);

        if (button != null)
        {
            string capturedId = identifier;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnUpgradeClicked(capturedId));
        }

        nodeViews[identifier] = new UpgradeNodeView
        {
            Id = identifier,
            Button = button,
            Image = image,
            Label = label
        };
    }

    void CreateConnectionLine(Vector2 from, Vector2 to)
    {
        if (lineRoot == null)
            return;

        var go = new GameObject("Line");
        go.transform.SetParent(lineRoot, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);

        float distance = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(distance, 2f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        var image = go.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.55f, 0.55f, 0.6f, 0.85f);
        image.raycastTarget = false;
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
        tmp.enableWordWrapping = true;
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
        onMetaGoldChanged?.Invoke();
    }

    void OnUpgradeClicked(string identifier)
    {
        if (!MetaSaveService.TryPurchase(metaSave, identifier))
            return;

        RefreshAll();
        onMetaGoldChanged?.Invoke();
    }

    public void ReloadSave()
    {
        metaSave = MetaSaveService.Load();
        RefreshAll();
    }

    public void RefreshAll()
    {
        foreach (var pair in nodeViews)
            RefreshUpgradeButton(pair.Value);
    }

    void RefreshUpgradeButton(UpgradeNodeView node)
    {
        var info = CSVLoader.Get(node.Id);
        if (info == null)
            return;

        bool canBuy = MetaSaveService.CanPurchase(metaSave, node.Id);
        bool maxed = metaSave.GetLevel(node.Id) >= info.maxLevel;
        bool locked = MetaSaveService.IsLocked(metaSave, info);

        if (node.Label != null)
        {
            node.Label.text = BuildUpgradeLabel(info);
            node.Label.color = maxed
                ? new Color(0.45f, 0.45f, 0.48f, 1f)
                : Color.white;
        }

        if (node.Button != null)
            node.Button.interactable = canBuy;

        if (node.Image == null)
            return;

        if (maxed)
            node.Image.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        else if (locked)
            node.Image.color = new Color(0.22f, 0.22f, 0.22f, 1f);
        else if (canBuy)
            node.Image.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        else
            node.Image.color = new Color(0.45f, 0.28f, 0.28f, 1f);
    }

    string BuildUpgradeLabel(UpgradeInfo info)
    {
        int level = metaSave.GetLevel(info.identifier);
        bool maxed = level >= info.maxLevel;
        bool locked = MetaSaveService.IsLocked(metaSave, info);
        string title = info.GetDisplayText();

        if (locked)
            return $"{title}\n({level}/{info.maxLevel})\nLocked";

        if (maxed)
            return $"{title}\n({level}/{info.maxLevel})\nMAX";

        return $"{title}\n({level}/{info.maxLevel})\n{info.cost} Gold";
    }
}
