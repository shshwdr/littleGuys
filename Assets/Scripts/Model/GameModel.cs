using System.Collections.Generic;
using UniRx;

public class GameModel
{
    public GameConfigData Config;
    public Dictionary<string, RecipeData> Recipes = new Dictionary<string, RecipeData>();

    public ReactiveProperty<GameState> State = new ReactiveProperty<GameState>(GameState.Playing);
    public ReactiveProperty<int> Gold = new ReactiveProperty<int>(0);
    public ReactiveProperty<string> ActiveRecipeId = new ReactiveProperty<string>(null);
    public ReactiveCollection<CustomerData> Customers = new ReactiveCollection<CustomerData>();
    public ReactiveCollection<WorkerData> Workers = new ReactiveCollection<WorkerData>();
    public Dictionary<ZoneType, ZoneData> Zones = new Dictionary<ZoneType, ZoneData>();
    public Subject<Unit> WorkerAssignmentChanged = new Subject<Unit>();
    public Subject<ZoneType> ZoneSourcePicked = new Subject<ZoneType>();
    public Subject<ZoneType> ZoneUnlocked = new Subject<ZoneType>();
    public Subject<string> RecipeUnlocked = new Subject<string>();

    public List<ProductionOrder> ProductionOrders = new List<ProductionOrder>();
    public HashSet<string> UnlockedRecipes = new HashSet<string>();
    public int NextOrderId = 1;
    public int ServedCustomerCount;
    public int TotalCustomersSpawned;

    public int NextWorkerId;
    public int NextCustomerId = 1;
    public float CustomerSpawnTimer;
    public float PatienceTimer;
    public bool HasSpawnedFirstCustomer;

    public int CurrentSceneId;
    public int SceneProgress;
    public bool BossHasSpawned;
    public bool InBossFight;
    public int LevelEncounterRowIndex;
    public int LevelEncounterCustomerIndex;
    public float LevelTimeRemaining;
    public float CurrentSpawnInterval;

    public ReactiveProperty<int> SceneProgressChanged = new ReactiveProperty<int>(0);
    public ReactiveProperty<bool> BossFightChanged = new ReactiveProperty<bool>(false);
    public ReactiveProperty<float> LevelTimeChanged = new ReactiveProperty<float>(0f);

    public ZoneData GetZone(ZoneType type) => Zones[type];

    public RecipeData GetRecipe(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
            return null;

        Recipes.TryGetValue(recipeId, out var recipe);
        return recipe;
    }

    public void NotifyWorkerAssignmentChanged()
    {
        WorkerAssignmentChanged.OnNext(Unit.Default);
    }

    public void SetZonePhase(ZoneData zone, ZonePhase phase)
    {
        if (zone.Phase == phase)
            return;

        zone.Phase = phase;
        NotifyWorkerAssignmentChanged();
    }
}
