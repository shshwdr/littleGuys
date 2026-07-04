using System.Collections.Generic;
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

public static class CSVLoader
{
    static readonly Dictionary<string, UpgradeInfo> upgradeDict = new Dictionary<string, UpgradeInfo>();
    static readonly Dictionary<string, List<UpgradeInfo>> childrenMap = new Dictionary<string, List<UpgradeInfo>>();
    static bool initialized;

    public static bool IsInitialized => initialized && upgradeDict.Count > 0;

    public static void Init()
    {
        upgradeDict.Clear();
        childrenMap.Clear();
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

        initialized = true;
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
}
