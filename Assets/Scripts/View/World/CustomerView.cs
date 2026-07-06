using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CustomerView : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] Image bodyImage;
    [SerializeField, HideInInspector] SpriteRenderer bodyRenderer;

    [Header("UI")]
    [SerializeField] Image patienceFill;
    [SerializeField] Image effectFill;
    [SerializeField] TMP_Text orderText;
    [SerializeField] TMP_Text descText;
    [SerializeField] Button sacrificeButton;
    [SerializeField] bool createUiIfMissing = true;

    CustomerData customer;
    CustomerSacrificeService sacrificeService;
    readonly CompositeDisposable viewDisposables = new CompositeDisposable();
    Vector2 currentTarget;
    Tween moveTween;

    public void Setup(
        CustomerData data,
        Vector2 startPosition,
        Vector2 targetPosition,
        GameModel model,
        CustomerSacrificeService sacrificeSvc,
        CompositeDisposable disposables,
        bool animateFromEntry = true)
    {
        customer = data;
        sacrificeService = sacrificeSvc;
        currentTarget = targetPosition;
        transform.position = animateFromEntry
            ? new Vector3(startPosition.x, startPosition.y, 0f)
            : new Vector3(targetPosition.x, targetPosition.y, 0f);

        EnsureBodyRenderer();
        EnsureUi();

        moveTween?.Kill();
        if (animateFromEntry)
        {
            moveTween = transform
                .DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), 0.6f)
                .SetEase(Ease.OutQuad);
        }

        if (orderText != null)
            orderText.text = customer.OrderLabel;

        if (descText != null)
            descText.text = customer.Name;

        if (sacrificeButton != null)
        {
            sacrificeButton.OnClickAsObservable()
                .Subscribe(_ => sacrificeService.TryAssignWorker(customer))
                .AddTo(viewDisposables);
        }

        model.WorkerAssignmentChanged
            .Subscribe(_ => RefreshSacrificeControls())
            .AddTo(viewDisposables);

        RefreshSacrificeControls();
    }

    void EnsureBodyRenderer()
    {
        var sprite = ResourceSpriteLoader.GetCustomer(customer?.CustomerTypeId);

        EnsureBodyImage();

        if (bodyImage != null)
        {
            bodyImage.sprite = sprite;
            bodyImage.enabled = sprite != null;
        }

        if (bodyRenderer != null)
            bodyRenderer.enabled = false;
    }

    void EnsureBodyImage()
    {
        if (bodyImage != null)
            return;

        var canvas = WorldUiFactory.CreateWorldCanvas(transform, Vector3.zero, new Vector2(100f, 100f));
        bodyImage = CreateBodyImage(canvas.transform);
    }

    Image CreateBodyImage(Transform parent)
    {
        var go = new GameObject("BodyImage");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(96f, 96f);
        rect.anchoredPosition = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    void CreateDefaultUi()
    {
        var topCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 0.95f, 0f), new Vector2(260f, 150f));
        descText = WorldUiFactory.CreateText(topCanvas.transform, "Desc", customer.Name, new Vector2(0f, 52f), 18f, TextAlignmentOptions.Center);
        descText.rectTransform.sizeDelta = new Vector2(240f, 48f);
        orderText = WorldUiFactory.CreateText(topCanvas.transform, "Order", customer.OrderLabel, new Vector2(0f, 18f), 28f, TextAlignmentOptions.Center);
        patienceFill = WorldUiFactory.CreateFillBar(topCanvas.transform, "Patience", new Vector2(0f, -18f), new Vector2(180f, 20f), new Color(0.2f, 0.8f, 0.3f));
        effectFill = WorldUiFactory.CreateFillBar(topCanvas.transform, "Effect", new Vector2(0f, -46f), new Vector2(180f, 14f), new Color(0.95f, 0.35f, 0.25f, 1f));
        effectFill.gameObject.transform.parent.gameObject.SetActive(customer.Effect == "eatMinion");

        var bottomCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, -1.05f, 0f), new Vector2(320f, 80f));
        sacrificeButton = WorldUiFactory.CreateButton(bottomCanvas.transform, "Sacrifice", "Sacrifice", Vector2.zero, new Vector2(150f, 44f));
        EnsureSacrificeButtonVisible();
    }

    void EnsureUi()
    {
        if (createUiIfMissing && patienceFill == null)
        {
            CreateDefaultUi();
            return;
        }

        HideLegacyAssignUi();
        EnsureDescText();
        EnsureSacrificeButton();
        EnsureEffectBar();
    }

    void EnsureDescText()
    {
        if (descText != null || orderText == null)
            return;

        var parent = orderText.transform.parent;
        descText = WorldUiFactory.CreateText(parent, "Desc", customer.Name, new Vector2(0f, 52f), 18f, TextAlignmentOptions.Center);
        descText.rectTransform.sizeDelta = new Vector2(240f, 48f);
    }

    void EnsureSacrificeButton()
    {
        if (sacrificeButton != null)
            return;

        Canvas buttonCanvas = null;
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas == null || canvas.transform == orderText?.transform.parent)
                continue;

            buttonCanvas = canvas;
            break;
        }

        if (buttonCanvas == null)
            buttonCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, -1.05f, 0f), new Vector2(320f, 80f));

        sacrificeButton = WorldUiFactory.CreateButton(buttonCanvas.transform, "Sacrifice", "Sacrifice", Vector2.zero, new Vector2(150f, 44f));
        EnsureSacrificeButtonVisible();
    }

    void EnsureSacrificeButtonVisible()
    {
        if (sacrificeButton == null)
            return;

        sacrificeButton.gameObject.SetActive(true);

        var canvas = sacrificeButton.transform.parent;
        if (canvas != null)
            canvas.gameObject.SetActive(true);
    }

    void HideLegacyAssignUi()
    {
        HideChildByName("Add");
        HideChildByName("Sub");
        HideChildByName("Count");
    }

    void HideChildByName(string childName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child == null || child.name != childName)
                continue;

            child.gameObject.SetActive(false);
        }
    }

    void EnsureEffectBar()
    {
        if (customer == null || customer.Effect != "eatMinion")
        {
            if (effectFill != null)
                effectFill.gameObject.transform.parent.gameObject.SetActive(false);
            return;
        }

        if (effectFill != null)
        {
            effectFill.gameObject.transform.parent.gameObject.SetActive(true);
            return;
        }

        Transform parent = patienceFill != null
            ? patienceFill.transform.parent.parent
            : transform;

        effectFill = WorldUiFactory.CreateFillBar(
            parent,
            "Effect",
            new Vector2(0f, -35f),
            new Vector2(180f, 14f),
            new Color(0.95f, 0.35f, 0.25f, 1f));
    }

    public void Bind(CompositeDisposable disposables)
    {
        if (customer == null || patienceFill == null)
            return;

        customer.Patience
            .Subscribe(p => patienceFill.fillAmount = Mathf.Clamp01(p / customer.MaxPatience))
            .AddTo(viewDisposables);

        if (effectFill != null && customer.Effect == "eatMinion")
        {
            customer.EffectProgress
                .Subscribe(p => effectFill.fillAmount = p)
                .AddTo(viewDisposables);
        }
    }

    void RefreshSacrificeControls()
    {
        if (sacrificeButton == null)
            return;

        EnsureSacrificeButtonVisible();
        sacrificeButton.interactable = sacrificeService != null
            && customer != null
            && !customer.IsServed
            && sacrificeService.CanSacrificeButton(customer);
    }

    public void MoveTo(Vector2 targetPosition)
    {
        if (Vector2.Distance(currentTarget, targetPosition) <= 0.01f)
            return;

        currentTarget = targetPosition;
        moveTween?.Kill();
        moveTween = transform
            .DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), 0.35f)
            .SetEase(Ease.OutQuad);
    }

    public void SetVisible(bool visible)
    {
        if (bodyImage != null)
            bodyImage.transform.parent.gameObject.SetActive(visible);

        if (bodyRenderer != null)
            bodyRenderer.enabled = visible;

        SetUiVisible(visible);
    }

    void SetUiVisible(bool visible)
    {
        if (patienceFill != null)
            patienceFill.transform.parent.parent.gameObject.SetActive(visible);

        if (orderText != null)
            orderText.transform.parent.gameObject.SetActive(visible);

        if (sacrificeButton != null)
            sacrificeButton.transform.parent.gameObject.SetActive(visible);
    }

    void Update()
    {
        if (customer == null)
            return;

        if (patienceFill != null)
            patienceFill.fillAmount = Mathf.Clamp01(customer.Patience.Value / customer.MaxPatience);

        if (effectFill != null && customer.Effect == "eatMinion")
            effectFill.fillAmount = customer.EffectProgress.Value;

        if (orderText != null)
            orderText.text = customer.OrderLabel;

        if (descText != null)
            descText.text = customer.Name;

        RefreshSacrificeControls();
    }

    void OnDestroy()
    {
        viewDisposables.Dispose();
        moveTween?.Kill();
        customer = null;
        sacrificeButton = null;
        descText = null;
    }
}
