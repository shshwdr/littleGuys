using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CustomerView : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] SpriteRenderer bodyRenderer;
    [SerializeField] Vector2 bodySize = new Vector2(0.8f, 0.8f);

    [Header("UI")]
    [SerializeField] Image patienceFill;
    [SerializeField] TMP_Text orderText;
    [SerializeField] Button addButton;
    [SerializeField] Button subButton;
    [SerializeField] TMP_Text countText;
    [SerializeField] bool createUiIfMissing = true;

    [Header("Points")]
    [SerializeField] Transform deliveryPoint;
    [SerializeField] Transform sacrificePoint;

    CustomerData customer;
    CustomerSacrificeService sacrificeService;
    readonly CompositeDisposable viewDisposables = new CompositeDisposable();
    Vector2 currentTarget;
    Tween moveTween;

    public Transform DeliveryPoint => deliveryPoint != null ? deliveryPoint : transform;
    public Transform SacrificePoint => sacrificePoint != null ? sacrificePoint : transform;

    public void Setup(
        CustomerData data,
        Vector2 startPosition,
        Vector2 targetPosition,
        GameModel model,
        CustomerSacrificeService sacrificeSvc,
        CompositeDisposable disposables)
    {
        customer = data;
        sacrificeService = sacrificeSvc;
        transform.position = new Vector3(startPosition.x, startPosition.y, 0f);

        EnsureBodyRenderer();
        if (createUiIfMissing && patienceFill == null)
            CreateDefaultUi();

        moveTween = transform
            .DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), 0.6f)
            .SetEase(Ease.OutQuad);
        currentTarget = targetPosition;

        if (orderText != null)
            orderText.text = customer.OrderLabel;

        if (addButton != null)
        {
            addButton.OnClickAsObservable()
                .Subscribe(_ => sacrificeService.TryAssignWorker(customer))
                .AddTo(viewDisposables);
        }

        if (subButton != null)
        {
            subButton.OnClickAsObservable()
                .Subscribe(_ => sacrificeService.TryRecallWorker(customer))
                .AddTo(viewDisposables);
        }

        model.WorkerAssignmentChanged
            .Subscribe(_ => RefreshAssignControls())
            .AddTo(viewDisposables);

        RefreshAssignControls();
    }

    void EnsureBodyRenderer()
    {
        if (bodyRenderer != null)
            return;

        bodyRenderer = ColorSpriteFactory.CreateSprite(
            "Body",
            transform,
            ResourceSpriteLoader.GetCustomer(),
            Color.white,
            bodySize);
    }

    void CreateDefaultUi()
    {
        var topCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 0.85f, 0f), new Vector2(220f, 120f));
        orderText = WorldUiFactory.CreateText(topCanvas.transform, "Order", customer.OrderLabel, new Vector2(0f, 30f), 28f, TextAlignmentOptions.Center);
        patienceFill = WorldUiFactory.CreateFillBar(topCanvas.transform, "Patience", new Vector2(0f, -10f), new Vector2(180f, 20f), new Color(0.2f, 0.8f, 0.3f));

        var bottomCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, -1.05f, 0f), new Vector2(320f, 80f));
        subButton = WorldUiFactory.CreateButton(bottomCanvas.transform, "Sub", "-", new Vector2(-60f, 0f), new Vector2(50f, 40f));
        addButton = WorldUiFactory.CreateButton(bottomCanvas.transform, "Add", "+", new Vector2(60f, 0f), new Vector2(50f, 40f));
        countText = WorldUiFactory.CreateText(bottomCanvas.transform, "Count", "0", new Vector2(0f, 0f), 28f, TextAlignmentOptions.Center);
        countText.rectTransform.sizeDelta = new Vector2(40f, 40f);
    }

    public void Bind(CompositeDisposable disposables)
    {
        if (customer == null || patienceFill == null)
            return;

        customer.Patience
            .Subscribe(p => patienceFill.fillAmount = Mathf.Clamp01(p / customer.MaxPatience))
            .AddTo(viewDisposables);
    }

    void RefreshAssignControls()
    {
        if (sacrificeService == null || customer == null || customer.IsServed)
            return;

        if (countText != null)
            countText.text = sacrificeService.GetAssignedCount(customer).ToString();

        if (addButton != null)
            addButton.interactable = sacrificeService.CanAssign(customer);

        if (subButton != null)
            subButton.interactable = sacrificeService.CanRecall(customer);
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

    void Update()
    {
        if (customer == null)
            return;

        if (patienceFill != null)
            patienceFill.fillAmount = Mathf.Clamp01(customer.Patience.Value / customer.MaxPatience);

        if (orderText != null)
            orderText.text = customer.OrderLabel;
    }

    void OnDestroy()
    {
        viewDisposables.Dispose();
        moveTween?.Kill();
        customer = null;
        addButton = null;
        subButton = null;
        countText = null;
    }
}
