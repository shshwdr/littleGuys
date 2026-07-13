using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    const string CustomerViewPrefabPath = "prefab/customerView";

    [Header("View Roots")]
    [Tooltip("Gameplay content hidden while upgrade view is open.")]
    [SerializeField] GameObject mainGameContent;
    [Tooltip("Upgrade UI root shown after game over.")]
    [SerializeField] GameObject upgradeViewRoot;

    [Header("Upgrade View")]
    [Tooltip("升级树拖拽与滚轮缩放灵敏度，值越大移动/缩放越多。")]
    public float upgradeTreeScrollSensitivity = 0.35f;

    [Header("World Positions")]
    [SerializeField] Transform foodOutputPos;
    [SerializeField] Transform sacrificePos;
    [SerializeField] Transform handPos;

    [Header("FMOD Music Configurations")]
    [SerializeField] private FMODUnity.EventReference gameplayMusicEvent;
    [SerializeField] private FMODUnity.EventReference upgradeMusicEvent;

    private FMOD.Studio.EventInstance gameplayMusicInstance;
    private FMOD.Studio.EventInstance upgradeMusicInstance;
    Coroutine gameplayMusicRoutine;
    Coroutine upgradeMusicRoutine;

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
    TutorialManager tutorialManager;
    bool runGoldSettled;
    bool finishedChoppingTutorialTriggered;
    bool almostLoseTutorialTriggered;
    bool sacrificeTutorialTriggered;
    bool finishedServeTutorialTriggered;
    bool openUpgradeTutorialTriggered;
    CustomerData patienceTutorialCustomer;
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
        BuildGame();
        ApplyGameplayMode();
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        disposables.Dispose();
        if (Instance == this)
            Instance = null;

        if (gameplayMusicInstance.isValid())
        {
            gameplayMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            gameplayMusicInstance.release();
        }

        if (upgradeMusicInstance.isValid())
        {
            upgradeMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            upgradeMusicInstance.release();
        }

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
        workService.ZoneStepCompleted += OnZoneStepCompleted;

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

        Observable.EveryUpdate()
            .Subscribe(_ => ClickDebugLogger.LogClickIfAny())
            .AddTo(disposables);
    }

    GameModel CreateModel(GameConfigData config, MetaSaveData metaSave)
    {
        var gameModel = new GameModel { Config = config };
        gameModel.Recipes = RecipeFactory.CreateMap(config);

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

        // 初始解锁 dishIdentifier 为 "0" 的菜谱及其所需机器。
        var dish0Id = DishRecipeBuilder.GetRecipeIdForDish(gameModel.Recipes, "0");
        if (!string.IsNullOrEmpty(dish0Id))
        {
            gameModel.UnlockedRecipes.Add(dish0Id);
            DishRecipeBuilder.UnlockZonesForRecipe(gameModel, gameModel.GetRecipe(dish0Id));
        }

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
        {
            zonePrefab.gameObject.SetActive(false);
            return;
        }

        if (!zonePrefab.gameObject.activeSelf)
            zonePrefab.gameObject.SetActive(true);

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
        hudView = FindObjectOfType<GameHudView>(true);
        if (hudView == null)
        {
            Debug.LogWarning("GameHudView not found in scene.");
        }
        else
        {
            bool speedUpUnlocked = MetaSaveService.IsSpeedUpUnlocked(metaSave);
            hudView.Setup(model, disposables, speedUpUnlocked, OnHudPrimaryClicked, metaSave.CurrentScene);
        }

        var gameOverView = FindObjectOfType<GameOverView>(true);
        if (gameOverView == null)
            Debug.LogWarning("GameOverView not found in scene. Place it under GameHud and wire the panels.");
        else
            gameOverView.Setup(model, this, disposables);

        EnsureUpgradePanel(metaSave);

        tutorialManager = FindObjectOfType<TutorialManager>(true);
        if (tutorialManager == null)
            Debug.LogWarning("TutorialManager not found in scene.");
        else
            tutorialManager.TryShowTutorial("start");
    }

    void OnZoneStepCompleted(ZoneType zoneType)
    {
        if (zoneType != ZoneType.Chop || finishedChoppingTutorialTriggered)
            return;

        finishedChoppingTutorialTriggered = true;
        tutorialManager?.TryShowTutorial("finishedChopping");
    }

    void BindAlmostLoseTutorial(CustomerData customer)
    {
        if (almostLoseTutorialTriggered || patienceTutorialCustomer != null || customer == null)
            return;

        patienceTutorialCustomer = customer;
        customer.Patience
            .Subscribe(patience => TryTriggerAlmostLoseTutorial(customer, patience))
            .AddTo(disposables);
    }

    void TryTriggerAlmostLoseTutorial(CustomerData customer, float patience)
    {
        if (almostLoseTutorialTriggered || customer != patienceTutorialCustomer || customer.IsServed)
            return;

        if (customer.MaxPatience <= 0f)
            return;

        // 第一个顾客耐心剩余 ≤ 30% 时触发。
        if (patience / customer.MaxPatience > 0.3f)
            return;

        almostLoseTutorialTriggered = true;
        tutorialManager?.TryShowTutorial("almostLose");
    }

    void OnHudPrimaryClicked()
    {
        FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/UI/sfx_ui_restart_buttom");
        

        if (gameplayMusicInstance.isValid())
        {
            gameplayMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            gameplayMusicInstance.release();
        }

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

        upgradePanel.Setup(metaSave, disposables, () => hudView?.SetUpgradeMode(true), upgradeTreeScrollSensitivity);
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

        StopMusicRoutine(ref upgradeMusicRoutine);
        StopMusicInstance(ref upgradeMusicInstance);
        StopMusicRoutine(ref gameplayMusicRoutine);
        gameplayMusicRoutine = StartCoroutine(StartGameplayMusicWhenReady());
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

        if (!openUpgradeTutorialTriggered)
        {
            openUpgradeTutorialTriggered = true;
            tutorialManager?.TryShowTutorial("upgradeView");
        }

        StopMusicRoutine(ref gameplayMusicRoutine);
        StopMusicInstance(ref gameplayMusicInstance);
        StopMusicRoutine(ref upgradeMusicRoutine);
        upgradeMusicRoutine = StartCoroutine(StartUpgradeMusicWhenReady());
    }

    IEnumerator StartGameplayMusicWhenReady()
    {
        yield return WaitForFmodBanks();
        TryStartMusic(gameplayMusicEvent, ref gameplayMusicInstance);
        gameplayMusicRoutine = null;
    }

    IEnumerator StartUpgradeMusicWhenReady()
    {
        yield return WaitForFmodBanks();
        TryStartMusic(upgradeMusicEvent, ref upgradeMusicInstance);
        upgradeMusicRoutine = null;
    }

    static IEnumerator WaitForFmodBanks()
    {
        // WebGL loads banks asynchronously; music in Awake is often too early.
        // SFX works because it plays later, after banks have finished loading.
        while (!FMODUnity.RuntimeManager.IsInitialized || !FMODUnity.RuntimeManager.HaveAllBanksLoaded)
            yield return null;
    }

    static void TryStartMusic(FMODUnity.EventReference musicEvent, ref FMOD.Studio.EventInstance instance)
    {
        if (musicEvent.IsNull)
            return;

        if (instance.isValid())
        {
            FMOD.Studio.PLAYBACK_STATE state;
            instance.getPlaybackState(out state);
            if (state == FMOD.Studio.PLAYBACK_STATE.PLAYING)
                return;
            StopMusicInstance(ref instance);
        }

        try
        {
            instance = FMODUnity.RuntimeManager.CreateInstance(musicEvent);
            instance.start();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FMOD] Failed to start music: {e.Message}");
        }
    }

    void StopMusicRoutine(ref Coroutine routine)
    {
        if (routine == null)
            return;
        StopCoroutine(routine);
        routine = null;
    }

    static void StopMusicInstance(ref FMOD.Studio.EventInstance instance)
    {
        if (!instance.isValid())
            return;

        instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        instance.release();
        instance.clearHandle();
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
        Vector3 pickupPosition = workerView != null
            ? workerView.WorldPosition
            : new Vector3(worker.Position.x, worker.Position.y, 0f);

        QueueHandDoorAction(identifier, closeDoor =>
        {
            customerHand = EnsureCustomerHandFor(identifier);
            PlayHandPickupAt(
                pickupPosition,
                () => sacrificeService.FinalizeSacrifice(worker),
                closeDoor);
        });
    }

    void OnFoodReadyForHandPickup(FoodHandPickupRequest request)
    {
        var customer = request.Customer;
        // 立刻在取餐位生成一份独立的成品显示，它独立于 Plate 机器区，
        // 因此 plate 工人可以马上开始下一步而不必等这只手来取。
        GameObject foodGo = CreateHandFoodVisual(request);

        QueueHandDoorAction(customer?.CustomerTypeId ?? "normal", closeDoor =>
        {
            customerHand = EnsureCustomerHandFor(customer?.CustomerTypeId ?? "normal");
            if (customerHand == null)
            {
                if (foodGo != null)
                    Destroy(foodGo);
                CompleteFoodDelivery(request);
                closeDoor?.Invoke();
                return;
            }

            RunHandFoodPickup(request, foodGo, closeDoor);
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

    void PlayHandPickupAt(Vector3 pickupPosition, Action onGrabbed, Action closeDoor)
    {
        if (customerHand == null)
        {
            onGrabbed?.Invoke();
            closeDoor?.Invoke();
            return;
        }

        customerHand.PlayHandSequence(
            pickupPosition,
            onBeforeExtend: () => customerHand.SetHandOpen(true),
            onAtTarget: () =>
            {
                customerHand.SetHandOpen(false);
                onGrabbed?.Invoke();
            },
            onComplete: () =>
            {
                customerHand.SetHandOpen(true);
                closeDoor?.Invoke();
            });
    }

    void RunHandFoodPickup(FoodHandPickupRequest request, GameObject foodGo, Action closeDoor)
    {
        Transform foodTransform = foodGo != null ? foodGo.transform : null;

        customerHand.PlayHandSequence(
            layout.GetFoodOutputPosition(),
            onBeforeExtend: () => customerHand.SetHandOpen(true),
            onAtTarget: () =>
            {
                customerHand.SetHandOpen(false);
                if (foodTransform != null)
                    customerHand.AttachToGrab(foodTransform);
                FMODUnity.RuntimeManager.PlayOneShot("event:/SFX/Costumers/sfx_costumer_eats");
            },
            onComplete: () =>
            {
                if (foodGo != null)
                    Destroy(foodGo);

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
        int baseSatiety = recipe != null ? recipe.Satiety : 0;
        int deliverySatiety = customerService.ComputeDeliverySatiety(request.RecipeId);

        // 放到取餐位时已预扣份额，这里落定：释放预扣并真正累加饱食度。
        customerService.ReleasePendingSatiety(request.Customer, deliverySatiety);
        customerService.AddSatiety(request.Customer, deliverySatiety);
        model.Gold.Value += baseSatiety + model.Config.dishPriceBonus;
        productionService.OnOrderDelivered(request.OrderId);
    }

    void OnSacrificeSatietyGranted(CustomerData customer, int satiety)
    {
        customerService.AddSatiety(customer, satiety);

        if (sacrificeTutorialTriggered)
            return;

        sacrificeTutorialTriggered = true;
        tutorialManager?.TryShowTutorial("sacrificed");
    }

    GameObject CreateHandFoodVisual(FoodHandPickupRequest request)
    {
        var food = Food.Spawn(null, "HandFood");
        food.transform.position = layout.GetFoodOutputPosition();
        food.SetVisual(request.Identifier, request.Visual, request.Stage);
        return food.gameObject;
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
        BindAlmostLoseTutorial(customer);
   

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

        if (!finishedServeTutorialTriggered && customer != null && customer.IsServed)
        {
            finishedServeTutorialTriggered = true;
            tutorialManager?.TryShowTutorial("finishedServe");
        }
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
