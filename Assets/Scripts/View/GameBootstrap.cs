using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() != null)
            return;

        var go = new GameObject("GameBootstrap");
        go.AddComponent<GameBootstrap>();
    }

    readonly CompositeDisposable disposables = new CompositeDisposable();
    readonly Dictionary<CustomerData, CustomerView> customerViews = new Dictionary<CustomerData, CustomerView>();

    GameModel model;
    WorldLayout layout;
    CustomerSpawnService customerService;
    WorkerAssignService assignService;
    ZoneWorkService workService;
    TransportService transportService;
    RecipeService recipeService;

    Transform worldRoot;
    Transform workerRoot;
    Transform customerRoot;

    void Awake()
    {
        MainThreadDispatcher.Initialize();
        EnsureEventSystem();
        SetupCamera();
        BuildGame();
    }

    void OnDestroy()
    {
        disposables.Dispose();
    }

    void BuildGame()
    {
        var config = GameConfigData.Load();
        layout = new WorldLayout(config);
        model = CreateModel(config);
        customerService = new CustomerSpawnService(model);
        assignService = new WorkerAssignService(model);
        workService = new ZoneWorkService(model);
        transportService = new TransportService(model, layout, customerService);
        recipeService = new RecipeService(model);

        worldRoot = new GameObject("World").transform;
        workerRoot = new GameObject("Workers").transform;
        workerRoot.SetParent(worldRoot, false);
        customerRoot = new GameObject("Customers").transform;
        customerRoot.SetParent(worldRoot, false);

        CreateZones();
        CreateWorkers();
        CreateUi();

        model.Customers.ObserveAdd()
            .Subscribe(e => OnCustomerAdded(e.Value))
            .AddTo(disposables);

        model.Customers.ObserveRemove()
            .Subscribe(e => OnCustomerRemoved(e.Value))
            .AddTo(disposables);

        Observable.EveryUpdate()
            .Subscribe(_ => RefreshCustomerPositions())
            .AddTo(disposables);

        Observable.EveryUpdate()
            .Where(_ => model.State.Value == GameState.Playing)
            .Subscribe(_ =>
            {
                float dt = Time.deltaTime;
                customerService.Tick(dt);
                transportService.Tick(dt);
                workService.Tick(dt);
            })
            .AddTo(disposables);
    }

    GameModel CreateModel(GameConfigData config)
    {
        var gameModel = new GameModel { Config = config };

        gameModel.Zones[ZoneType.Ingredient] = new ZoneData { Type = ZoneType.Ingredient };
        gameModel.Zones[ZoneType.Chop] = new ZoneData { Type = ZoneType.Chop };
        gameModel.Zones[ZoneType.Cook] = new ZoneData { Type = ZoneType.Cook };
        gameModel.Zones[ZoneType.Plate] = new ZoneData { Type = ZoneType.Plate };
        gameModel.Zones[ZoneType.Idle] = new ZoneData { Type = ZoneType.Idle };

        for (int i = 0; i < config.totalWorkers; i++)
        {
            var worker = new WorkerData
            {
                Id = i,
                AssignedZone = ZoneType.Idle,
                State = WorkerState.Standing,
                HasArrivedAtZone = true,
                Position = layout.GetWorkerSlotPosition(ZoneType.Idle, i, config.totalWorkers)
            };
            gameModel.Workers.Add(worker);
        }

        gameModel.GetZone(ZoneType.Idle).WorkerCount.Value = config.totalWorkers;
        return gameModel;
    }

    void CreateZones()
    {
        CreateZoneView(ZoneType.Ingredient, "Ingredient", false);
        CreateZoneView(ZoneType.Chop, "Chop", true);
        CreateZoneView(ZoneType.Cook, "Cook", true);
        CreateZoneView(ZoneType.Plate, "Plate", true);
        CreateZoneView(ZoneType.Idle, "Idle", false);
    }

    void CreateZoneView(ZoneType type, string label, bool withControls)
    {
        var go = new GameObject(label + "Zone");
        go.transform.SetParent(worldRoot, false);

        if (withControls)
        {
            var view = go.AddComponent<ZoneWorldUIView>();
            view.Setup(type, model, assignService, layout.GetZonePosition(type), label);
            view.Bind(disposables);
        }
        else
        {
            go.transform.position = layout.GetZonePosition(type);
            var color = type == ZoneType.Ingredient
                ? new Color(0.3f, 0.75f, 0.3f)
                : new Color(0.55f, 0.55f, 0.55f);
            ColorSpriteFactory.CreateSquare("Zone", go.transform, color, new Vector2(1.6f, 1.2f));

            if (type == ZoneType.Ingredient)
            {
                var canvas = WorldUiFactory.CreateWorldCanvas(go.transform, new Vector3(0f, 1.1f, 0f), new Vector2(220f, 60f));
                WorldUiFactory.CreateText(canvas.transform, "Title", label, Vector2.zero, 26f, TMPro.TextAlignmentOptions.Center);
            }
            else
            {
                var canvas = WorldUiFactory.CreateWorldCanvas(go.transform, new Vector3(0f, 1.1f, 0f), new Vector2(220f, 60f));
                WorldUiFactory.CreateText(canvas.transform, "Title", label, Vector2.zero, 26f, TMPro.TextAlignmentOptions.Center);
            }
        }
    }

    void CreateWorkers()
    {
        foreach (var worker in model.Workers)
        {
            var go = new GameObject("Worker_" + worker.Id);
            go.transform.SetParent(workerRoot, false);
            var view = go.AddComponent<WorkerView>();
            view.Setup(worker, model.Config);
        }
    }

    void CreateUi()
    {
        var recipeGo = new GameObject("RecipePanel");
        recipeGo.transform.SetParent(transform, false);
        recipeGo.AddComponent<RecipePanelView>()
            .Setup(model, recipeService, RecipeFactory.CreateSoup(model.Config), disposables);

        var gameOverGo = new GameObject("GameOver");
        gameOverGo.transform.SetParent(transform, false);
        gameOverGo.AddComponent<GameOverView>().Setup(model, disposables);
    }

    void OnCustomerAdded(CustomerData customer)
    {
        int index = model.Customers.IndexOf(customer);
        var go = new GameObject("Customer_" + customer.Id);
        go.transform.SetParent(customerRoot, false);
        var view = go.AddComponent<CustomerView>();
        view.Setup(customer, layout.GetCustomerPosition(index));
        view.Bind(disposables);
        customerViews[customer] = view;
        RefreshCustomerPositions();
    }

    void OnCustomerRemoved(CustomerData customer)
    {
        if (!customerViews.TryGetValue(customer, out var view))
            return;

        customerViews.Remove(customer);
        if (view != null)
            Destroy(view.gameObject);
    }

    void RefreshCustomerPositions()
    {
        for (int i = 0; i < model.Customers.Count; i++)
        {
            var customer = model.Customers[i];
            if (!customerViews.TryGetValue(customer, out var view) || view == null)
                continue;

            Vector2 pos = layout.GetCustomerPosition(i);
            view.transform.position = new Vector3(pos.x, pos.y, 0f);
        }
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
    }
}
