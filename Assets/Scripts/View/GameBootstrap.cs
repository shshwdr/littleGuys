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
    readonly Dictionary<int, WorkerView> workerViews = new Dictionary<int, WorkerView>();

    GameModel model;
    WorldLayout layout;
    CustomerSpawnService customerService;
    WorkerAssignService assignService;
    ZoneWorkService workService;
    TransportService transportService;
    ProductionService productionService;
    SplitterService splitterService;
    WorkerGrowthService growthService;

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
        productionService = new ProductionService(model);
        customerService = new CustomerSpawnService(model);
        splitterService = new SplitterService(model, layout);
        assignService = new WorkerAssignService(model, splitterService);
        workService = new ZoneWorkService(model, layout, productionService);
        transportService = new TransportService(model, layout, customerService, productionService);
        growthService = new WorkerGrowthService(model);

        splitterService.WorkerAdded += OnWorkerAdded;
        splitterService.WorkerRemoved += OnWorkerRemoved;
        transportService.WorkerRemoved += OnWorkerRemoved;

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
                splitterService.Tick(dt);
                growthService.Tick(dt);
            })
            .AddTo(disposables);
    }

    GameModel CreateModel(GameConfigData config)
    {
        var gameModel = new GameModel { Config = config };
        gameModel.Recipes = RecipeFactory.CreateMap(config);

        gameModel.Zones[ZoneType.Ingredient] = new ZoneData { Type = ZoneType.Ingredient };
        gameModel.Zones[ZoneType.Chop] = new ZoneData { Type = ZoneType.Chop };
        gameModel.Zones[ZoneType.Cook] = new ZoneData { Type = ZoneType.Cook };
        gameModel.Zones[ZoneType.Wok] = new ZoneData { Type = ZoneType.Wok };
        gameModel.Zones[ZoneType.Plate] = new ZoneData { Type = ZoneType.Plate };
        gameModel.Zones[ZoneType.Splitter] = new ZoneData { Type = ZoneType.Splitter };
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

        gameModel.NextWorkerId = config.totalWorkers;
        gameModel.GetZone(ZoneType.Idle).WorkerCount.Value = config.totalWorkers;
        return gameModel;
    }

    void CreateZones()
    {
        CreateZoneView(ZoneType.Ingredient, "Ingredient", false);
        CreateZoneView(ZoneType.Chop, "Chop", true);
        CreateZoneView(ZoneType.Cook, "Cook", true);
        CreateZoneView(ZoneType.Wok, "Wok", true);
        CreateZoneView(ZoneType.Plate, "Plate", true);
        CreateZoneView(ZoneType.Splitter, "Splitter", true);
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

            var itemGo = new GameObject(type + "ZoneItem");
            itemGo.transform.SetParent(worldRoot, false);
            itemGo.AddComponent<ZoneItemView>().Setup(type, model, model.Config);

            if (type == ZoneType.Chop)
            {
                var chopPos = layout.GetItemCenterAboveZone(ZoneType.Chop);
                CreateBufferPile("ChopOutputPileVeg", ZoneType.Chop, chopPos, FoodStage.Chopped, FoodVisual.Veg);
                CreateBufferPile("ChopOutputPileMeat", ZoneType.Chop, chopPos, FoodStage.Chopped, FoodVisual.Meat);
            }
            else if (type == ZoneType.Cook)
            {
                CreateBufferPile("CookOutputPile", ZoneType.Cook, layout.GetItemCenterAboveZone(ZoneType.Cook), FoodStage.Cooked, FoodVisual.Veg);
            }
            else if (type == ZoneType.Wok)
            {
                CreateBufferPile("WokOutputPile", ZoneType.Wok, layout.GetItemCenterAboveZone(ZoneType.Wok), FoodStage.Fried, FoodVisual.Meat);
            }
        }
        else
        {
            go.transform.position = layout.GetZonePosition(type);
            var color = type == ZoneType.Ingredient
                ? new Color(0.3f, 0.75f, 0.3f)
                : new Color(0.55f, 0.55f, 0.55f);
            ColorSpriteFactory.CreateSquare("Zone", go.transform, color, new Vector2(1.6f, 1.2f));

            var canvas = WorldUiFactory.CreateWorldCanvas(go.transform, new Vector3(0f, 1.1f, 0f), new Vector2(220f, 60f));
            WorldUiFactory.CreateText(canvas.transform, "Title", label, Vector2.zero, 26f, TMPro.TextAlignmentOptions.Center);

            if (type == ZoneType.Ingredient)
            {
                float size = model.Config.foodSpriteSize * 1.2f;
                var pilePos = layout.GetSourceItemPosition(ZoneType.Chop);
                var pileGo = new GameObject("IngredientPile");
                pileGo.transform.position = new Vector3(pilePos.x, pilePos.y, -0.06f);
                pileGo.transform.SetParent(worldRoot, false);
                ColorSpriteFactory.CreateSprite(
                    "Pile",
                    pileGo.transform,
                    ResourceSpriteLoader.GetVeg(),
                    Color.white,
                    new Vector2(size, size));
            }
        }
    }

    void CreateBufferPile(string name, ZoneType zone, Vector2 position, FoodStage stage, FoodVisual visual)
    {
        var pileGo = new GameObject(name);
        pileGo.transform.SetParent(worldRoot, false);
        pileGo.AddComponent<ZoneBufferPileView>().Setup(zone, model, model.Config, position, stage, visual);
    }

    void CreateWorkers()
    {
        foreach (var worker in model.Workers)
            OnWorkerAdded(worker);
    }

    void CreateUi()
    {
        var recipeGo = new GameObject("RecipePanel");
        recipeGo.transform.SetParent(transform, false);
        recipeGo.AddComponent<RecipePanelView>().Setup(
            model,
            productionService,
            model.GetRecipe("soup"),
            model.GetRecipe("stirfry"),
            disposables);

        var gameOverGo = new GameObject("GameOver");
        gameOverGo.transform.SetParent(transform, false);
        gameOverGo.AddComponent<GameOverView>().Setup(model, disposables);
    }

    void OnWorkerAdded(WorkerData worker)
    {
        if (workerViews.ContainsKey(worker.Id))
            return;

        var go = new GameObject("Worker_" + worker.Id);
        go.transform.SetParent(workerRoot, false);
        var view = go.AddComponent<WorkerView>();
        view.Setup(worker, model.Config);
        workerViews[worker.Id] = view;
    }

    void OnWorkerRemoved(WorkerData worker)
    {
        if (!workerViews.TryGetValue(worker.Id, out var view))
            return;

        workerViews.Remove(worker.Id);
        if (view != null)
            Destroy(view.gameObject);
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
        cam.orthographicSize = 7.5f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
    }
}
