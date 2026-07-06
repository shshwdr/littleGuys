using System.Collections.Generic;
using System.Linq;
using Sinbad;

public class UpgradeInfo
{
    public string identifier;
    public string name;
    public string desc;
    public string effect;
    public string prev;
    public int cost;
    public int maxLevel;
    public bool unlocked;
    public int value;
    public string comment;

    public string GetDisplayText()
    {
        if (string.IsNullOrEmpty(desc))
            return identifier;

        return desc.Contains("{0}") ? string.Format(desc, value) : desc;
    }
}

public class LevelInfo
{
    public int scene;
    public List<string> encounters;
    public float interval;
}

public class SceneInfo
{
    public int scene;
    public string name;
    public string desc;
    public int full;
    public string boss;
}

public class CustomerInfo
{
    public string identifier;
    public string desc;
    public string effect;
    public int value;
    public int @base;

    public string GetDisplayText()
    {
        if (string.IsNullOrEmpty(desc))
            return identifier ?? string.Empty;

        return desc.Contains("{0}") ? string.Format(desc, value) : desc;
    }
}

public static class CSVLoader
{
    static readonly Dictionary<string, UpgradeInfo> upgradeDict = new Dictionary<string, UpgradeInfo>();
    static readonly Dictionary<string, List<UpgradeInfo>> childrenMap = new Dictionary<string, List<UpgradeInfo>>();
    static readonly Dictionary<int, SceneInfo> sceneDict = new Dictionary<int, SceneInfo>();
    static readonly Dictionary<int, List<LevelInfo>> levelByScene = new Dictionary<int, List<LevelInfo>>();
    static readonly Dictionary<string, CustomerInfo> customerDict = new Dictionary<string, CustomerInfo>();
    static bool initialized;

    public static bool IsInitialized => initialized && upgradeDict.Count > 0;

    public static void Init()
    {
        upgradeDict.Clear();
        childrenMap.Clear();
        sceneDict.Clear();
        levelByScene.Clear();
        customerDict.Clear();
        initialized = false;

        var upgradeInfos = CsvUtil.LoadObjects<UpgradeInfo>("upgrade");
        foreach (var info in upgradeInfos)
        {
            if (string.IsNullOrEmpty(info.identifier))
                continue;

            upgradeDict[info.identifier] = info;

            string parentKey = string.IsNullOrEmpty(info.prev) ? string.Empty : info.prev;
            if (!childrenMap.ContainsKey(parentKey))
                childrenMap[parentKey] = new List<UpgradeInfo>();
            childrenMap[parentKey].Add(info);
        }

        foreach (var info in CsvUtil.LoadObjects<SceneInfo>("scene"))
        {
            if (!sceneDict.ContainsKey(info.scene))
                sceneDict[info.scene] = info;
        }

        foreach (var info in CsvUtil.LoadObjects<LevelInfo>("level"))
        {
            if (!levelByScene.ContainsKey(info.scene))
                levelByScene[info.scene] = new List<LevelInfo>();
            levelByScene[info.scene].Add(info);
        }

        foreach (var info in CsvUtil.LoadObjects<CustomerInfo>("customer"))
        {
            if (string.IsNullOrEmpty(info.identifier))
                continue;

            customerDict[info.identifier] = info;
        }

        initialized = true;
    }

    public static void ParseEncounterEntry(string entry, out string identifier, out int full)
    {
        identifier = entry ?? string.Empty;
        full = 1;

        if (string.IsNullOrEmpty(entry))
            return;

        int splitIndex = entry.LastIndexOf('_');
        if (splitIndex <= 0)
            return;

        string suffix = entry.Substring(splitIndex + 1);
        if (!int.TryParse(suffix, out int parsedFull) || parsedFull <= 0)
            return;

        identifier = entry.Substring(0, splitIndex);
        full = parsedFull;
    }

    public static UpgradeInfo Get(string identifier)
    {
        upgradeDict.TryGetValue(identifier, out var info);
        return info;
    }

    public static IEnumerable<UpgradeInfo> GetAll()
    {
        return upgradeDict.Values;
    }

    public static List<UpgradeInfo> GetRoots()
    {
        if (!childrenMap.TryGetValue(string.Empty, out var roots))
            return new List<UpgradeInfo>();

        return roots;
    }

    public static List<UpgradeInfo> GetChildren(string parentIdentifier)
    {
        if (string.IsNullOrEmpty(parentIdentifier)
            || !childrenMap.TryGetValue(parentIdentifier, out var children))
            return new List<UpgradeInfo>();

        return children;
    }

    public static bool HasUpgrade(string identifier)
    {
        return !string.IsNullOrEmpty(identifier) && upgradeDict.ContainsKey(identifier);
    }

    public static SceneInfo GetScene(int sceneId)
    {
        sceneDict.TryGetValue(sceneId, out var info);
        return info;
    }

    public static List<LevelInfo> GetLevelRows(int sceneId)
    {
        if (!levelByScene.TryGetValue(sceneId, out var rows))
            return new List<LevelInfo>();

        return rows;
    }

    public static CustomerInfo GetCustomer(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return null;

        customerDict.TryGetValue(identifier, out var info);
        return info;
    }

    public static int GetMaxSceneId()
    {
        if (sceneDict.Count == 0)
            return 0;

        return sceneDict.Keys.Max();
    }
}
