using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] GameObject customerPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() != null)
            return;

        if (!SceneFlowService.IsMainGameScene())
            return;

        var go = new GameObject("GameBootstrap");
        go.AddComponent<GameBootstrap>();
    }

    readonly CompositeDisposable disposables = new CompositeDisposable();
    readonly Dictionary<CustomerData, CustomerView> customerViews = new Dictionary<CustomerData, CustomerView>();
    readonly Dictionary<int, WorkerView> workerViews = new Dictionary<int, WorkerView>();
    readonly HashSet<ZoneType> boundZonePrefabs = new HashSet<ZoneType>();

    GameModel model;
    WorldLayout layout;
    CustomerSpawnService customerService;
    WorkerAssignService assignService;
    ZoneWorkService workService;
    TransportService transportService;
    ProductionService productionService;
    SplitterService splitterService;
    WorkerGrowthService growthService;
    CustomerSacrificeService sacrificeService;

    Transform workerRoot;

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
        var metaSave = MetaSaveService.Load();
        var baseConfig = GameConfigData.Load();
        var config = MetaSaveService.ApplyUpgrades(baseConfig, metaSave);
        layout = new WorldLayout(config);
        layout.RegisterFromScene();
        model = CreateModel(config, metaSave);
        productionService = new ProductionService(model);
        customerService = new CustomerSpawnService(model, layout);
        splitterService = new SplitterService(model, layout);
        assignService = new WorkerAssignService(model, splitterService);
        workService = new ZoneWorkService(model, layout, productionService);
        transportService = new TransportService(model, layout, customerService, productionService);
        growthService = new WorkerGrowthService(model);
        sacrificeService = new CustomerSacrificeService(model, layout, assignService);

        splitterService.WorkerAdded += OnWorkerAdded;
        splitterService.WorkerRemoved += OnWorkerRemoved;
        transportService.WorkerRemoved += OnWorkerRemoved;
        sacrificeService.WorkerRemoved += OnWorkerRemoved;

        workerRoot = new GameObject("Workers").transform;

        BindSceneZones();
        CreateWorkers();
        CreateUi();

        model.ZoneUnlocked
            .Subscribe(zoneType => BindZonePrefabIfNeeded(zoneType))
            .AddTo(disposables);

        model.Customers.ObserveAdd()
            .Subscribe(e => OnCustomerAdded(e.Value))
            .AddTo(disposables);

        model.Customers.ObserveRemove()
            .Subscribe(e =>
            {
                OnCustomerRemoved(e.Value);
                RefreshCustomerPositions();
            })
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
                sacrificeService.Tick(dt);
            })
            .AddTo(disposables);

        Observable.EveryUpdate()
            .Where(_ => Input.GetKeyDown(KeyCode.S))
            .Subscribe(_ =>
            {
                MetaSaveService.Reset();
                SceneFlowService.LoadMainGame();
            })
            .AddTo(disposables);
    }

    GameModel CreateModel(GameConfigData config, MetaSaveData metaSave)
    {
        var gameModel = new GameModel { Config = config };
        gameModel.Recipes = RecipeFactory.CreateMap(config);
        gameModel.UnlockedRecipes.Add("vegsalad");

        foreach (var zonePrefab in layout.GetSceneZones())
        {
            bool unlocked = zonePrefab.StartsUnlocked;
            if (zonePrefab.ZoneType == ZoneType.Cook
                || zonePrefab.ZoneType == ZoneType.Wok
                || zonePrefab.ZoneType == ZoneType.Splitter)
                unlocked = false;

            gameModel.Zones[zonePrefab.ZoneType] = new ZoneData
            {
                Type = zonePrefab.ZoneType,
                IsUnlocked = unlocked
            };
        }

        EnsureDefaultZones(gameModel);
        MetaSaveService.ApplyUnlocks(gameModel, metaSave);

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

    static void EnsureDefaultZones(GameModel gameModel)
    {
        void Ensure(ZoneType type, bool unlocked)
        {
            if (!gameModel.Zones.ContainsKey(type))
                gameModel.Zones[type] = new ZoneData { Type = type, IsUnlocked = unlocked };
        }

        Ensure(ZoneType.Ingredient, true);
        Ensure(ZoneType.Chop, true);
        Ensure(ZoneType.Cook, false);
        Ensure(ZoneType.Wok, false);
        Ensure(ZoneType.Plate, true);
        Ensure(ZoneType.Splitter, false);
        Ensure(ZoneType.Idle, true);
    }

    void BindSceneZones()
    {
        foreach (var zonePrefab in layout.GetSceneZones())
            BindZonePrefabIfNeeded(zonePrefab.ZoneType);
    }

    void BindZonePrefabIfNeeded(ZoneType type)
    {
        if (boundZonePrefabs.Contains(type))
            return;

        if (!layout.TryGetZonePrefab(type, out var zonePrefab))
            return;

        if (!model.GetZone(type).IsUnlocked)
            return;

        boundZonePrefabs.Add(type);
        zonePrefab.Setup(model, assignService, disposables);
    }

    void CreateWorkers()
    {
        foreach (var worker in model.Workers)
            OnWorkerAdded(worker);
    }

    void CreateUi()
    {
        var hudGo = new GameObject("GameHud");
        hudGo.transform.SetParent(transform, false);
        hudGo.AddComponent<GameHudView>().Setup(model, disposables);

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
        view.Setup(worker, model, layout, model.Config);
        view.SacrificeAnimationComplete += sacrificeService.FinalizeSacrifice;
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
        int index = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : model.Customers.IndexOf(customer);
        int total = model.Customers.Count;
        Vector2 target = layout.GetCustomerPosition(index, total);
        Vector2 entry = layout.GetCustomerEntryPosition(index, total);

        CustomerView view;
        if (customerPrefab != null)
        {
            var go = Instantiate(customerPrefab, target, Quaternion.identity);
            go.name = "Customer_" + customer.Id;
            view = go.GetComponent<CustomerView>();
            if (view == null)
                view = go.AddComponent<CustomerView>();
        }
        else
        {
            var go = new GameObject("Customer_" + customer.Id);
            view = go.AddComponent<CustomerView>();
        }

        view.Setup(customer, entry, target, model, sacrificeService, disposables);
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
        int total = model.Customers.Count;
        for (int i = 0; i < total; i++)
        {
            var customer = model.Customers[i];
            if (!customerViews.TryGetValue(customer, out var view) || view == null)
                continue;

            int slotIndex = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : i;
            Vector2 pos = layout.GetCustomerPosition(slotIndex, total);
            view.MoveTo(pos);
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
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            cam.transform.position = new Vector3(0f, 0.5f, -10f);
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            return;
        }

        cam.orthographic = true;
    }
}
