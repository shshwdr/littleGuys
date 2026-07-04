using UnityEngine;

public static class MetaSaveService
{
    const string SaveKey = "MetaSaveData";

    public static MetaSaveData Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return MetaSaveData.CreateDefault();

        string json = PlayerPrefs.GetString(SaveKey);
        var data = JsonUtility.FromJson<MetaSaveData>(json);
        if (data == null || data.UpgradeLevels == null || data.UpgradeLevels.Length != UpgradeDefinition.All.Length)
            return MetaSaveData.CreateDefault();

        return data;
    }

    public static void Save(MetaSaveData data)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    public static GameConfigData ApplyUpgrades(GameConfigData baseConfig, MetaSaveData meta)
    {
        var config = Object.Instantiate(baseConfig);
        config.totalWorkers += meta.GetLevel(UpgradeId.InitialWorkers);
        int moveSpeedLevel = meta.GetLevel(UpgradeId.MoveSpeed);
        config.workerMoveSpeed *= 1f + 0.1f * moveSpeedLevel;
        return config;
    }

    public static void ApplyUnlocks(GameModel model, MetaSaveData meta)
    {
        if (meta.GetLevel(UpgradeId.UnlockVegSoup) >= 1)
        {
            model.UnlockedRecipes.Add("vegsoup");
            model.GetZone(ZoneType.Cook).IsUnlocked = true;
        }

        if (meta.GetLevel(UpgradeId.UnlockSplitter) >= 1)
            model.GetZone(ZoneType.Splitter).IsUnlocked = true;

        if (meta.GetLevel(UpgradeId.UnlockStirFry) >= 1)
        {
            model.UnlockedRecipes.Add("stirfry");
            model.GetZone(ZoneType.Wok).IsUnlocked = true;
        }

        model.ActiveRecipeId.Value = GetHighestUnlockedRecipeId(model);
    }

    public static string GetHighestUnlockedRecipeId(GameModel model)
    {
        string bestId = null;
        int bestSatiety = -1;

        foreach (var recipeId in model.UnlockedRecipes)
        {
            var recipe = model.GetRecipe(recipeId);
            if (recipe == null || recipe.Satiety <= bestSatiety)
                continue;

            bestSatiety = recipe.Satiety;
            bestId = recipeId;
        }

        return bestId;
    }

    public static bool CanPurchase(MetaSaveData meta, UpgradeId id)
    {
        var def = UpgradeDefinition.Get(id);
        int level = meta.GetLevel(id);
        if (level >= def.MaxLevel)
            return false;

        if ((int)id > 0 && meta.GetLevel((UpgradeId)((int)id - 1)) < 1)
            return false;

        return meta.MetaGold >= def.Price;
    }

    public static bool TryPurchase(MetaSaveData meta, UpgradeId id)
    {
        if (!CanPurchase(meta, id))
            return false;

        var def = UpgradeDefinition.Get(id);
        meta.MetaGold -= def.Price;
        meta.SetLevel(id, meta.GetLevel(id) + 1);
        Save(meta);
        return true;
    }
}
