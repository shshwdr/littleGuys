using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CustomerView : MonoBehaviour
{
    CustomerData customer;
    Image patienceFill;
    TMP_Text orderText;
    Button addButton;
    Button subButton;
    TMP_Text countText;
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
        CompositeDisposable disposables)
    {
        customer = data;
        sacrificeService = sacrificeSvc;
        transform.position = new Vector3(startPosition.x, startPosition.y, 0f);

        ColorSpriteFactory.CreateSprite(
            "Body",
            transform,
            ResourceSpriteLoader.GetCustomer(),
            Color.white,
            new Vector2(0.8f, 0.8f));

        var topCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 0.85f, 0f), new Vector2(220f, 120f));
        orderText = WorldUiFactory.CreateText(topCanvas.transform, "Order", customer.OrderLabel, new Vector2(0f, 30f), 28f, TextAlignmentOptions.Center);
        patienceFill = WorldUiFactory.CreateFillBar(topCanvas.transform, "Patience", new Vector2(0f, -10f), new Vector2(180f, 20f), new Color(0.2f, 0.8f, 0.3f));
        WorldUiFactory.CreateText(topCanvas.transform, "PatienceLabel", "Patience", new Vector2(0f, 15f), 18f, TextAlignmentOptions.Center);

        var bottomCanvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, -1.05f, 0f), new Vector2(320f, 80f));
        subButton = WorldUiFactory.CreateButton(bottomCanvas.transform, "Sub", "-", new Vector2(-60f, 0f), new Vector2(50f, 40f));
        addButton = WorldUiFactory.CreateButton(bottomCanvas.transform, "Add", "+", new Vector2(60f, 0f), new Vector2(50f, 40f));
        countText = WorldUiFactory.CreateText(bottomCanvas.transform, "Count", "0", new Vector2(0f, 0f), 28f, TextAlignmentOptions.Center);
        countText.rectTransform.sizeDelta = new Vector2(40f, 40f);

        subButton.OnClickAsObservable()
            .Subscribe(_ => sacrificeService.TryRecallWorker(customer))
            .AddTo(viewDisposables);

        addButton.OnClickAsObservable()
            .Subscribe(_ => sacrificeService.TryAssignWorker(customer))
            .AddTo(viewDisposables);

        model.WorkerAssignmentChanged
            .Subscribe(_ => RefreshAssignControls())
            .AddTo(viewDisposables);

        RefreshAssignControls();

        moveTween = transform
            .DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), 0.6f)
            .SetEase(Ease.OutQuad);
        currentTarget = targetPosition;
    }

    public void Bind(CompositeDisposable disposables)
    {
        customer.Patience
            .Subscribe(p => patienceFill.fillAmount = Mathf.Clamp01(p / customer.MaxPatience))
            .AddTo(viewDisposables);
    }

    void RefreshAssignControls()
    {
        if (sacrificeService == null || customer == null || customer.IsServed)
            return;

        if (addButton == null || subButton == null || countText == null)
            return;

        countText.text = sacrificeService.GetAssignedCount(customer).ToString();
        addButton.interactable = sacrificeService.CanAssign(customer);
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
        if (customer == null || patienceFill == null)
            return;

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
