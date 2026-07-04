using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class ZoneWorldUIView : MonoBehaviour
{
    [Header("Optional Prefab UI")]
    [SerializeField] Button addButton;
    [SerializeField] Button subButton;
    [SerializeField] TMP_Text countText;
    [SerializeField] TMP_Text percentText;
    [SerializeField] Image progressFill;
    [SerializeField] bool createUiIfMissing = true;

    ZoneType zoneType;
    GameModel model;
    WorkerAssignService assignService;

    public void Setup(
        ZoneType type,
        GameModel gameModel,
        WorkerAssignService assignSvc,
        Vector2 position,
        string zoneLabel)
    {
        zoneType = type;
        model = gameModel;
        assignService = assignSvc;
        transform.position = new Vector3(position.x, position.y, 0f);

        if (createUiIfMissing && addButton == null && countText == null)
            CreateDefaultUi(zoneLabel);

        if (zoneType == ZoneType.Idle)
            HideAssignButtons();
    }

    void HideAssignButtons()
    {
        if (addButton != null)
            addButton.gameObject.SetActive(false);
        if (subButton != null)
            subButton.gameObject.SetActive(false);
    }

    void CreateDefaultUi(string zoneLabel)
    {
        var canvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 1.1f, 0f), new Vector2(320f, 220f));
        WorldUiFactory.CreateText(canvas.transform, "Title", zoneLabel, new Vector2(0f, 85f), 26f, TextAlignmentOptions.Center);

        if (zoneType != ZoneType.Idle)
        {
            addButton = WorldUiFactory.CreateButton(canvas.transform, "Add", "+", new Vector2(60f, 45f), new Vector2(50f, 40f));
            subButton = WorldUiFactory.CreateButton(canvas.transform, "Sub", "-", new Vector2(-60f, 45f), new Vector2(50f, 40f));
        }

        countText = WorldUiFactory.CreateText(canvas.transform, "Count", "0", new Vector2(0f, 45f), 28f, TextAlignmentOptions.Center);
        countText.rectTransform.sizeDelta = new Vector2(40f, 40f);

        if (zoneType != ZoneType.Idle)
        {
            progressFill = WorldUiFactory.CreateFillBar(canvas.transform, "Progress", new Vector2(0f, -5f), new Vector2(220f, 22f), new Color(0.3f, 0.7f, 1f));
            percentText = WorldUiFactory.CreateText(canvas.transform, "Percent", "0%", new Vector2(0f, -35f), 22f, TextAlignmentOptions.Center);
        }
    }

    public void Bind(CompositeDisposable disposables)
    {
        if (model == null)
            return;

        var zone = model.GetZone(zoneType);

        if (zoneType == ZoneType.Idle)
        {
            zone.WorkerCount
                .Subscribe(count =>
                {
                    if (countText != null)
                        countText.text = count.ToString();
                })
                .AddTo(disposables);
            return;
        }

        if (addButton == null || subButton == null)
            return;

        addButton.OnClickAsObservable()
            .Subscribe(_ => assignService.TryAddWorker(zoneType))
            .AddTo(disposables);

        subButton.OnClickAsObservable()
            .Subscribe(_ => assignService.TryRemoveWorker(zoneType))
            .AddTo(disposables);

        zone.WorkerCount
            .Subscribe(count =>
            {
                if (countText != null)
                    countText.text = count.ToString();
                addButton.interactable = assignService.CanAddWorker(zoneType);
                subButton.interactable = assignService.CanRemoveWorker(zoneType);
            })
            .AddTo(disposables);

        if (progressFill != null)
        {
            zone.TaskProgress
                .Subscribe(progress =>
                {
                    progressFill.fillAmount = Mathf.Clamp01(progress);
                    if (percentText != null)
                        percentText.text = zone.StatusText.Value;
                })
                .AddTo(disposables);
        }

        if (percentText != null)
        {
            zone.StatusText
                .Subscribe(text => percentText.text = text)
                .AddTo(disposables);
        }

        addButton.interactable = assignService.CanAddWorker(zoneType);
        subButton.interactable = assignService.CanRemoveWorker(zoneType);

        model.WorkerAssignmentChanged
            .Subscribe(_ => RefreshAssignButtons())
            .AddTo(disposables);
    }

    void RefreshAssignButtons()
    {
        if (addButton == null || subButton == null)
            return;

        addButton.interactable = assignService.CanAddWorker(zoneType);
        subButton.interactable = assignService.CanRemoveWorker(zoneType);
    }

    void Update()
    {
        if (model == null)
            return;

        var zone = model.GetZone(zoneType);

        if (zoneType == ZoneType.Idle)
        {
            if (countText != null)
                countText.text = zone.WorkerCount.Value.ToString();
            return;
        }

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(zone.TaskProgress.Value);
        if (percentText != null)
            percentText.text = zone.StatusText.Value;

        RefreshAssignButtons();
    }
}
