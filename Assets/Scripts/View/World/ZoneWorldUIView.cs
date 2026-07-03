using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class ZoneWorldUIView : MonoBehaviour
{
    ZoneType zoneType;
    GameModel model;
    WorkerAssignService assignService;

    Button addButton;
    Button subButton;
    TMP_Text countText;
    TMP_Text percentText;
    TMP_Text speedText;
    Image progressFill;

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

        Color zoneColor = GetZoneColor(type);
        ColorSpriteFactory.CreateSquare("Zone", transform, zoneColor, new Vector2(1.6f, 1.2f));

        var canvas = WorldUiFactory.CreateWorldCanvas(transform, new Vector3(0f, 1.1f, 0f), new Vector2(320f, 220f));
        WorldUiFactory.CreateText(canvas.transform, "Title", zoneLabel, new Vector2(0f, 85f), 26f, TextAlignmentOptions.Center);

        addButton = WorldUiFactory.CreateButton(canvas.transform, "Add", "+", new Vector2(60f, 45f), new Vector2(50f, 40f));
        subButton = WorldUiFactory.CreateButton(canvas.transform, "Sub", "-", new Vector2(-60f, 45f), new Vector2(50f, 40f));
        countText = WorldUiFactory.CreateText(canvas.transform, "Count", "0", new Vector2(0f, 45f), 28f, TextAlignmentOptions.Center);
        countText.rectTransform.sizeDelta = new Vector2(40f, 40f);

        progressFill = WorldUiFactory.CreateFillBar(canvas.transform, "Progress", new Vector2(0f, -5f), new Vector2(220f, 22f), new Color(0.3f, 0.7f, 1f));
        percentText = WorldUiFactory.CreateText(canvas.transform, "Percent", "0%", new Vector2(0f, -35f), 22f, TextAlignmentOptions.Center);
        speedText = WorldUiFactory.CreateText(canvas.transform, "Speed", "Speed: 0.0", new Vector2(0f, -65f), 20f, TextAlignmentOptions.Center);
    }

    public void Bind(CompositeDisposable disposables)
    {
        var zone = model.GetZone(zoneType);

        addButton.OnClickAsObservable()
            .Subscribe(_ => assignService.TryAddWorker(zoneType))
            .AddTo(disposables);

        subButton.OnClickAsObservable()
            .Subscribe(_ => assignService.TryRemoveWorker(zoneType))
            .AddTo(disposables);

        zone.WorkerCount
            .Subscribe(count =>
            {
                countText.text = count.ToString();
                addButton.interactable = assignService.CanAddWorker(zoneType);
                subButton.interactable = assignService.CanRemoveWorker(zoneType);
            })
            .AddTo(disposables);

        zone.TaskProgress
            .Subscribe(progress =>
            {
                progressFill.fillAmount = Mathf.Clamp01(progress);
                percentText.text = zone.StatusText.Value;
            })
            .AddTo(disposables);

        zone.StatusText
            .Subscribe(text => percentText.text = text)
            .AddTo(disposables);

        zone.WorkSpeed
            .Subscribe(speed => speedText.text = $"Speed: {speed:F1}")
            .AddTo(disposables);

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
        if (model == null || progressFill == null)
            return;

        var zone = model.GetZone(zoneType);
        progressFill.fillAmount = Mathf.Clamp01(zone.TaskProgress.Value);
        if (percentText != null)
            percentText.text = zone.StatusText.Value;

        RefreshAssignButtons();
    }

    static Color GetZoneColor(ZoneType type)
    {
        switch (type)
        {
            case ZoneType.Ingredient: return new Color(0.3f, 0.75f, 0.3f);
            case ZoneType.Chop: return new Color(0.9f, 0.85f, 0.2f);
            case ZoneType.Cook: return new Color(0.95f, 0.55f, 0.15f);
            case ZoneType.Wok: return new Color(0.8f, 0.25f, 0.2f);
            case ZoneType.Plate: return new Color(0.65f, 0.35f, 0.85f);
            case ZoneType.Splitter: return new Color(0.45f, 0.7f, 0.85f);
            case ZoneType.Idle: return new Color(0.55f, 0.55f, 0.55f);
            default: return Color.white;
        }
    }
}
