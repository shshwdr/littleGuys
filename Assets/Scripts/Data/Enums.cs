public enum ZonePhase
{
    Idle,
    GoingToSource,
    Returning,
    AwaitingWorkers,
    Working,
    Delivering,
    AwaitingHandPickup
}

public enum ZoneType
{
    Ingredient,
    Chop,
    Cook,
    Wok,
    Plate,
    Splitter,
    Idle
}

public enum FoodStage
{
    None,
    Raw,
    Chopped,
    Cooked,
    Fried,
    Plated
}

public enum FoodVisual
{
    None,
    Veg,
    Meat,
    Minion
}

public enum WorkerState
{
    WalkingToZone,
    InZoneSync,
    Standing,
    Sacrificing,
    BeingEaten
}

public enum GameState
{
    Playing,
    TimeOut,
    LevelComplete,
    GameOver
}
