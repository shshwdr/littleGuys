using System.Collections.Generic;
using UniRx;

public class GameModel
{
    public GameConfigData Config;
    public Dictionary<string, RecipeData> Recipes = new Dictionary<string, RecipeData>();

    public ReactiveProperty<GameState> State = new ReactiveProperty<GameState>(GameState.Playing);
    public ReactiveCollection<CustomerData> Customers = new ReactiveCollection<CustomerData>();
    public ReactiveCollection<WorkerData> Workers = new ReactiveCollection<WorkerData>();
    public Dictionary<ZoneType, ZoneData> Zones = new Dictionary<ZoneType, ZoneData>();
    public Subject<Unit> WorkerAssignmentChanged = new Subject<Unit>();

    public List<ProductionOrder> ProductionOrders = new List<ProductionOrder>();
    public int NextOrderId = 1;
    public int NextCustomerRecipeToggle;

    public int NextWorkerId;
    public int NextCustomerId = 1;
    public float CustomerSpawnTimer;
    public float PatienceTimer;
    public bool HasSpawnedFirstCustomer;

    public ZoneData GetZone(ZoneType type) => Zones[type];

    public RecipeData GetRecipe(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
            return null;

        Recipes.TryGetValue(recipeId, out var recipe);
        return recipe;
    }
}
