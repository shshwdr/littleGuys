using System;

[Serializable]
public class MetaSaveData
{
    public int MetaGold;
    public int[] UpgradeLevels = new int[5];

    public int GetLevel(UpgradeId id) => UpgradeLevels[(int)id];

    public void SetLevel(UpgradeId id, int level)
    {
        UpgradeLevels[(int)id] = level;
    }

    public static MetaSaveData CreateDefault()
    {
        return new MetaSaveData { UpgradeLevels = new int[5] };
    }
}
