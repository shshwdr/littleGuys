using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    const string CustomerViewPrefabPath = "prefab/customerView";
    const string FoodPrefabPath = "prefab/food";

    [Header("View Roots")]
    [Tooltip("Gameplay content hidden while upgrade view is open.")]
    [SerializeField] GameObject mainGameContent;
    [Tooltip("Upgrade UI root shown after game over.")]
    [SerializeField] GameObject upgradeViewRoot;

    [Header("World Positions")]
    [SerializeField] Transform foodOutputPos;
    [SerializeField] Transform sacrificePos;
    [SerializeField] Transform handPos;

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
    CustomerEffectService customerEffectService;
    WorkerAssignService assignService;
    ZoneWorkService workService;
    TransportService transportService;
    ProductionService productionService;
    SplitterService splitterService;
    WorkerGrowthService growthService;
    CustomerSacrificeService sacrificeService;
    CustomerSil customerSil;
    CustomerHand customerHand;

    Transform workerRoot;
    UpgradePanelView upgradePanel;
    GameHudView hudView;
    bool runGoldSettled;
    GameObject customerViewPrefab;

    public Vector3 FoodOutputPosition => foodOutputPos != null ? foodOutputPos.position : Vector3.zero;
    public Vector3 SacrificePosition => sacrificePos != null ? sacrificePos.position : Vector3.zero;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate GameBootstrap in scene; destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        MainThreadDispatcher.Initialize();
        EnsureEventSystem();
        EnsureCamera();
        EnsureViewRoots();
        ApplyGameplayMode();
        BuildGame();
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        disposables.Dispose();
        if (Instance == this)
            Instance = null;
    }

    void BuildGame()
    {
        CSVLoader.Init();
        customerViewPrefab = Resources.Load<GameObject>(CustomerViewPrefabPath);
        if (customerViewPrefab == null)
            Debug.LogWarning($"Customer view prefab not found at Resources/{CustomerViewPrefabPath}.");
        var metaSave = MetaSaveService.Load();
        if (CSVLoader.GetScene(metaSave.CurrentScene) == null)
        {
            metaSave.CurrentScene = 0;
            MetaSaveService.Save(metaSave);
        }
        var baseConfig = GameConfigData.Load();
        var config = MetaSaveService.ApplyUpgrades(baseConfig, metaSave);
        layout = new WorldLayout(config);
        layout.RegisterFromScene();
        layout.RegisterWorldPositions(foodOutputPos, sacrificePos);
        model = CreateModel(config, metaSave);
        productionService = new ProductionService(model);
        customerService = new CustomerSpawnService(model, layout, metaSave.CurrentScene);
        customerEffectService = new CustomerEffectService(model, layout);
        splitterService = new SplitterService(model, layout);
        assignService = new WorkerAssignService(model, splitterService);
        workService = new ZoneWorkService(model, layout, productionService);
        transportService = new TransportService(model, layout, customerService, productionService);
        growthService = new WorkerGrowthService(model);
        sacrificeService = new CustomerSacrificeService(model, layout, assignService);
        customerSil = FindObjectOfType<CustomerSil>();
        customerHand = FindObjectOfType<CustomerHand>();
        EnsureCustomerHand();

        customerService.SceneCompleted += OnSceneCompleted;
        customerService.CustomerReadyToDepart += OnCustomerReadyToDepart;
        customerEffectService.EatMinionPerformanceRequested += OnEatMinionPerformanceRequested;
        sacrificeService.SacrificeReadyForPickup += OnSacrificeReadyForPickup;
        sacrificeService.SacrificeSatietyGranted += OnSacrificeSatietyGranted;
        transportService.FoodReadyForHandPickup += OnFoodReadyForHandPickup;

        splitterService.WorkerAdded += OnWorkerAdded;
        splitterService.WorkerRemoved += OnWorkerRemoved;
        transportService.WorkerRemoved += OnWorkerRemoved;
        sacrificeService.WorkerRemoved += OnWorkerRemoved;

        workerRoot = new GameObject("Workers").transform;
        workerRoot.SetParent(GetMainGameParent(), false);

        BindSceneZones();
        CreateWorkers();
        CreateUi(metaSave);

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
                customerEffectService.Tick(dt);
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

        Observable.EveryUpdate()
            .Where(_ => Input.GetKeyDown(KeyCode.B) && model.State.Value == GameState.Playing)
            .Subscribe(_ => customerService.CheatTriggerBossFight())
            .AddTo(disposables);

        Observable.EveryUpdate()
            .Where(_ => Input.GetKeyDown(KeyCode.V))
            .Subscribe(_ => hudView?.ToggleSpeedPanelCheat())
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

    void CreateUi(MetaSaveData metaSave)
    {
        var hudGo = new GameObject("GameHud");
        hudGo.transform.SetParent(transform, false);
        bool speedUpUnlocked = MetaSaveService.IsSpeedUpUnlocked(metaSave);
        hudView = hudGo.AddComponent<GameHudView>();
        hudView.Setup(model, disposables, speedUpUnlocked, OnHudPrimaryClicked, metaSave.CurrentScene);

        var gameOverGo = new GameObject("GameOver");
        gameOverGo.transform.SetParent(transform, false);
        gameOverGo.AddComponent<GameOverView>().Setup(model, this, disposables);

        EnsureUpgradePanel(metaSave);
    }

    void OnHudPrimaryClicked()
    {
        if (upgradeViewRoot != null && upgradeViewRoot.activeSelf)
        {
            SceneFlowService.LoadMainGame();
            return;
        }

        if (model.State.Value == GameState.Playing)
            model.State.Value = GameState.GameOver;
    }

    void EnsureViewRoots()
    {
        if (mainGameContent == null)
        {
            mainGameContent = new GameObject("MainGameContent");
            mainGameContent.transform.SetParent(transform, false);
        }

        if (upgradeViewRoot == null)
        {
            upgradeViewRoot = new GameObject("UpgradeView");
            upgradeViewRoot.transform.SetParent(transform, false);
        }
    }

    void EnsureUpgradePanel(MetaSaveData metaSave)
    {
        if (upgradePanel == null)
        {
            upgradePanel = upgradeViewRoot.GetComponent<UpgradePanelView>();
            if (upgradePanel == null)
                upgradePanel = upgradeViewRoot.AddComponent<UpgradePanelView>();
        }

        upgradePanel.Setup(metaSave, disposables, () => hudView?.SetUpgradeMode(true));
    }

    Transform GetMainGameParent()
    {
        return mainGameContent != null ? mainGameContent.transform : transform;
    }

    public void ApplyGameplayMode()
    {
        if (mainGameContent != null)
            mainGameContent.SetActive(true);

        if (upgradeViewRoot != null)
            upgradeViewRoot.SetActive(false);

        hudView?.SetUpgradeMode(false);
    }

    public void EnterUpgradeMode(string summaryText)
    {
        summaryText = SettleRunGold(summaryText);

        if (mainGameContent != null)
            mainGameContent.SetActive(false);

        if (upgradeViewRoot != null)
            upgradeViewRoot.SetActive(true);

        hudView?.SetUpgradeMode(true);
        var meta = MetaSaveService.Load();
        hudView?.UpdateSceneDisplay(meta.CurrentScene);
        upgradePanel?.SetSummary(summaryText);
        upgradePanel?.OnShown();
    }

    string SettleRunGold(string summaryText)
    {
        if (runGoldSettled || model == null)
            return summaryText ?? string.Empty;

        runGoldSettled = true;
        int runGold = model.Gold.Value;
        var meta = MetaSaveService.Load();
        meta.MetaGold += runGold;
        MetaSaveService.Save(meta);

        if (!string.IsNullOrEmpty(summaryText))
            return summaryText;

        return $"This run: +{runGold} Gold\nTotal: {meta.MetaGold} Gold";
    }

    void EnsureCustomerHand()
    {
        if (customerHand != null)
            return;

        customerHand = FindObjectOfType<CustomerHand>();
    }

    CustomerHand EnsureCustomerHandFor(string identifier)
    {
        customerHand = CustomerHand.SetIdentifier(
            identifier,
            GetCustomerHandParent(),
            GetCustomerHandGrabAnchor());

        if (customerHand != null && handPos != null)
            customerHand.AlignGrabTo(handPos.position);

        return customerHand;
    }

    Vector3? GetCustomerHandGrabAnchor()
    {
        return handPos != null ? handPos.position : (Vector3?)null;
    }

    Transform GetCustomerHandParent()
    {
        if (handPos != null)
            return handPos.parent;

        if (customerHand != null)
            return customerHand.transform.parent;

        if (customerSil != null)
            return customerSil.transform.parent;

        return GetMainGameParent();
    }

    void OnSceneCompleted()
    {
        var meta = MetaSaveService.Load();
        meta.CurrentScene++;
        MetaSaveService.Save(meta);
    }

    void OnEatMinionPerformanceRequested(CustomerData customer, WorkerData worker)
    {
        workerViews.TryGetValue(worker.Id, out var workerView);
        Vector3 pickupPosition = workerView != null
            ? workerView.WorldPosition
            : customerHand != null ? customerHand.OriginPosition : Vector3.zero;

        QueueHandDoorAction(customer?.CustomerTypeId ?? "normal", closeDoor =>
        {
            customerHand = EnsureCustomerHandFor(customer?.CustomerTypeId ?? "normal");
            PlayHandPickupAt(
                workerView != null ? workerView.transform : null,
                pickupPosition,
                () =>
                {
                    if (workerViews.TryGetValue(worker.Id, out var view))
                    {
                        workerViews.Remove(worker.Id);
                        if (view != null)
                            Destroy(view.gameObject);
                    }

                    customerEffectService.FinalizeEatenWorker(worker);
                },
                closeDoor);
        });
    }

    void OnSacrificeReadyForPickup(WorkerData worker)
    {
        if (worker == null)
            return;

        workerViews.TryGetValue(worker.Id, out var workerView);
        string identifier = worker.SacrificeTarget?.CustomerTypeId ?? "normal";
        Vector3 pickupPosition = new Vector3(worker.Position.x, worker.Position.y, 0f);

        QueueHandDoorAction(identifier, closeDoor =>
        {
            customerHand = EnsureCustomerHandFor(identifier);
            PlayHandPickupAt(
                workerView != null ? workerView.transform : null,
                pickupPosition,
                () =>
                {
                    sacrificeService.FinalizeSacrifice(worker);
                    if (workerViews.TryGetValue(worker.Id, out var view))
                    {
                        workerViews.Remove(worker.Id);
                        if (view != null)
                            Destroy(view.gameObject);
                    }
                },
                closeDoor);
        });
    }

    void OnFoodReadyForHandPickup(FoodHandPickupRequest request)
    {
        var customer = request.Customer;
        QueueHandDoorAction(customer?.CustomerTypeId ?? "normal", closeDoor =>
        {
            customerHand = EnsureCustomerHandFor(customer?.CustomerTypeId ?? "normal");
            if (customerHand == null)
            {
                CompleteFoodDelivery(request);
                closeDoor?.Invoke();
                return;
            }

            RunHandFoodPickup(request, closeDoor);
        });
    }

    void QueueHandDoorAction(string identifier, Action<Action> onDoorOpened)
    {
        if (customerSil != null)
            customerSil.QueueHandAction(identifier, onDoorOpened);
        else
        {
            EnsureCustomerHandFor(identifier);
            onDoorOpened?.Invoke(null);
        }
    }

    void QueueBossDoorAction(CustomerData customer, Action<Action> onDoorOpened)
    {
        string identifier = customer?.CustomerTypeId ?? "normal";

        if (customerSil != null)
            customerSil.QueueBossEntrance(identifier, onDoorOpened);
        else
            onDoorOpened?.Invoke(null);
    }

    void PlayHandPickupAt(Transform item, Vector3 pickupPosition, Action onDelivered, Action closeDoor)
    {
        if (customerHand == null)
        {
            onDelivered?.Invoke();
            closeDoor?.Invoke();
            return;
        }

        customerHand.PlayHandSequence(
            pickupPosition,
            onBeforeExtend: () => customerHand.SetHandOpen(true),
            onAtTarget: () =>
            {
                if (item != null)
                {
                    customerHand.SetHandOpen(false);
                    customerHand.AttachToGrab(item);
                }
            },
            onComplete: () =>
            {
                customerHand.SetHandOpen(true);
                onDelivered?.Invoke();
                closeDoor?.Invoke();
            });
    }

    void RunHandFoodPickup(FoodHandPickupRequest request, Action closeDoor)
    {
        ZoneItemView plateItemView = null;
        if (layout.TryGetZonePrefab(ZoneType.Plate, out var platePrefab))
            plateItemView = platePrefab.ItemView;

        Transform foodTransform = plateItemView != null ? plateItemView.transform : null;
        GameObject fallbackFoodGo = null;

        customerHand.PlayHandSequence(
            layout.GetFoodOutputPosition(),
            onBeforeExtend: () => customerHand.SetHandOpen(true),
            onAtTarget: () =>
            {
                if (foodTransform == null)
                {
                    fallbackFoodGo = CreateHandFoodVisual(request);
                    foodTransform = fallbackFoodGo.transform;
                }
                else
                {
                    plateItemView.SetExternallyControlled(true);
                }

                customerHand.SetHandOpen(false);
                customerHand.AttachToGrab(foodTransform);
            },
            onComplete: () =>
            {
                if (plateItemView != null)
                    plateItemView.ResetAfterCarry();
                else if (fallbackFoodGo != null)
                    Destroy(fallbackFoodGo);

                customerHand.SetHandOpen(true);
                CompleteFoodDelivery(request);
                closeDoor?.Invoke();
            });
    }

    void CompleteFoodDelivery(FoodHandPickupRequest request)
    {
        if (request.Customer == null)
            return;

        var recipe = model.GetRecipe(request.RecipeId);
        int satiety = recipe != null ? recipe.Satiety : 0;
        int bonusSatiety = 0;
        if (model.Config.patienceFoodPercent > 0 && satiety > 0)
            bonusSatiety = Mathf.CeilToInt(satiety * model.Config.patienceFoodPercent / 100f);

        customerService.AddSatiety(request.Customer, satiety + bonusSatiety);
        model.Gold.Value += satiety + model.Config.dishPriceBonus;
        productionService.OnOrderDelivered(request.OrderId);
    }

    void OnSacrificeSatietyGranted(CustomerData customer, int satiety)
    {
        customerService.AddSatiety(customer, satiety);
    }

    GameObject CreateHandFoodVisual(FoodHandPickupRequest request)
    {
        var foodPrefab = Resources.Load<GameObject>(FoodPrefabPath);
        if (foodPrefab != null)
        {
            var prefabGo = Instantiate(foodPrefab);
            prefabGo.name = "HandFood";
            prefabGo.transform.position = layout.GetFoodOutputPosition();
            var food = prefabGo.GetComponentInChildren<Food>();
            if (food == null)
                food = prefabGo.AddComponent<Food>();
            food.SetVisual(request.Visual, request.Stage);
            return prefabGo;
        }

        var go = new GameObject("HandFood");
        float size = model.Config.foodSpriteSize * 1.15f;
        var renderer = ColorSpriteFactory.CreateSprite(
            "Food",
            go.transform,
            ResourceSpriteLoader.GetFoodVisual(request.Visual),
            FoodVisualColors.GetTint(request.Visual, request.Stage),
            new Vector2(size, size));
        renderer.transform.position = layout.GetFoodOutputPosition();
        return go;
    }

    void OnCustomerReadyToDepart(CustomerData customer)
    {
        if (customer == null || customer.IsServed)
            return;

        customerService.ServeCustomer(customer);
    }

    void OnWorkerAdded(WorkerData worker)
    {
        if (workerViews.ContainsKey(worker.Id))
            return;

        var go = new GameObject("Worker_" + worker.Id);
        go.transform.SetParent(workerRoot, false);
        var view = go.AddComponent<WorkerView>();
        view.Setup(worker, model, layout, model.Config);
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
        if (customerSil == null && customerHand == null)
        {
            customer.IsAwaitingEntrance = false;
            CreateCustomerView(customer);
            RefreshCustomerPositions();
            return;
        }

        if (customer.IsBoss)
            QueueBossDoorAction(customer, closeDoor => RunCustomerEntrance(customer, closeDoor));
        else
            QueueHandDoorAction(customer.CustomerTypeId, closeDoor => RunCustomerEntrance(customer, closeDoor));
    }

    void RunCustomerEntrance(CustomerData customer, Action closeDoor)
    {
        if (customer == null || customer.IsServed)
        {
            closeDoor?.Invoke();
            return;
        }

        customerHand = EnsureCustomerHandFor(customer.CustomerTypeId) ?? CustomerHand.Instance;
        if (customerHand == null)
        {
            customer.IsAwaitingEntrance = false;
            CreateCustomerView(customer);
            RefreshCustomerPositions();
            closeDoor?.Invoke();
            return;
        }

        int index = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : model.Customers.IndexOf(customer);
        Vector3 targetPos = layout.GetCustomerPosition(index, model.Customers.Count);
        CustomerView carriedView = null;

        customerHand.PlayHandSequence(
            targetPos,
            onBeforeExtend: () =>
            {
                // 入场流程：先切到 close，再把顾客挂到抓取点后送到站位
                customerHand.SetHandOpen(false);
                carriedView = CreateCustomerViewForCarry(customer, targetPos);
                customerHand.AttachToGrab(carriedView.transform);
            },
            onAtTarget: () =>
            {
                customerHand.SetHandOpen(true);
                customerHand.DetachAtWorldPosition(carriedView.transform, targetPos);
                customerViews[customer] = carriedView;
                carriedView.Bind(disposables);
                customer.IsAwaitingEntrance = false;
                RefreshCustomerPositions();
            },
            onComplete: () =>
            {
                customerHand.SetHandOpen(true);
                closeDoor?.Invoke();
            });
    }

    CustomerView CreateCustomerViewForCarry(CustomerData customer, Vector2 target)
    {
        var view = InstantiateCustomerView(customer, target);
        view.Setup(customer, target, target, model, sacrificeService, disposables, animateFromEntry: false);
        return view;
    }

    void CreateCustomerView(CustomerData customer)
    {
        int index = customer.SpawnSlotIndex >= 0 ? customer.SpawnSlotIndex : model.Customers.IndexOf(customer);
        Vector2 target = layout.GetCustomerPosition(index, model.Customers.Count);

        var view = InstantiateCustomerView(customer, target);
        view.Setup(customer, target, target, model, sacrificeService, disposables, animateFromEntry: false);
        view.Bind(disposables);
        customerViews[customer] = view;
    }

    CustomerView InstantiateCustomerView(CustomerData customer, Vector2 target)
    {
        GameObject go;
        if (customerViewPrefab != null)
        {
            go = Instantiate(customerViewPrefab, target, Quaternion.identity);
            go.name = "Customer_" + customer.Id;
        }
        else
        {
            go = new GameObject("Customer_" + customer.Id);
        }

        var view = go.GetComponent<CustomerView>();
        if (view == null)
            view = go.AddComponent<CustomerView>();

        return view;
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

    void EnsureCamera()
    {
        if (Camera.main != null)
            return;

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();
    }
}
