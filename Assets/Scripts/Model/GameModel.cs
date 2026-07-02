using System.Collections.Generic;
using UniRx;

public class GameModel
{
    public GameConfigData Config;

    public ReactiveProperty<GameState> State = new ReactiveProperty<GameState>(GameState.Playing);
    public ReactiveProperty<RecipeData> ActiveRecipe = new ReactiveProperty<RecipeData>(null);
    public ReactiveCollection<CustomerData> Customers = new ReactiveCollection<CustomerData>();
    public ReactiveCollection<WorkerData> Workers = new ReactiveCollection<WorkerData>();
    public Dictionary<ZoneType, ZoneData> Zones = new Dictionary<ZoneType, ZoneData>();
    public Subject<Unit> WorkerAssignmentChanged = new Subject<Unit>();

    public int NextCustomerId = 1;
    public float CustomerSpawnTimer;
    public float PatienceTimer;
    public bool HasSpawnedFirstCustomer;

    public ZoneData GetZone(ZoneType type) => Zones[type];
}
