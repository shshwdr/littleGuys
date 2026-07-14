using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "LittleGuys/Game Config")]
public class GameConfigData : ScriptableObject
{
    [Header("Workers")]
    public int totalWorkers = 10;
    public int maxWorkersPerZone = 5;
    public float workerMoveSpeed = 1f;
    public float carryMoveSpeedDivisor = 10f;
    public float smallWorkerGrowTime = 10f;
    public float smallWorkerScale = 0.5f;

    [Header("Work Durations")]
    public float chopDuration = 10f;
    public float cookDuration = 15f;
    public float wokDuration = 20f;
    public float plateDuration = 5f;
    public float splitterDuration = 20f;

    [Header("Customers")]
    public int maxCustomers = 5;
    public float customerSpawnInterval = 10f;
    public float customerMaxPatience = 100f;
    public float patienceDecayPerSecond = 1f;

    [Header("Level")]
    public float levelTimeSeconds = 120f;

    [Header("Layout")]
    public float workerSpacing = 0.35f;
    public float carryYOffset = 0.6f;
    public float carriedItemHeight = 0.95f;
    public float workItemHeight = 1.15f;
    public float customerSacrificePatienceRestore = 0.3f;
    public float arriveThreshold = 0.05f;
    public float foodSpriteSize = 0.75f;
    public float sourceFetchOffsetY = 0.9f;
    public float workRotationSpeed = 180f;
    public Vector2 sacrificeQueueOffset = new Vector2(-0.45f, 0f);

    [Header("Economy")]
    public int customerTipsBonus = 0;
    public int dishPriceBonus = 0;
    public int doubleCutChancePercent = 0;
    public int doubleCookChancePercent = 0;
    public int doubleSplitChancePercent = 0;
    public bool yummyMinionEnabled = false;
    public int yummyMinionSatiety = 0;
    public int patienceFoodPercent = 0;

    public float GetMoveSpeed(int workerCount)
    {
        if (workerCount <= 0)
            return 0f;

        return workerCount * workerMoveSpeed / carryMoveSpeedDivisor;
    }

    public static GameConfigData Load()
    {
        var config = Resources.Load<GameConfigData>("GameConfig");
        if (config != null)
            return config;

        Debug.LogWarning("GameConfig not found at Resources/GameConfig. Using runtime defaults.");
        return CreateInstance<GameConfigData>();
    }
}
