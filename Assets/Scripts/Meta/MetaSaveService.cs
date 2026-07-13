using UnityEngine;

public static class MetaSaveService
{
    const string SaveKey = "MetaSaveData";

    // 全局共用同一份实例，避免多处 Load 出副本后互相 Save 覆盖（例如升级购买盖掉教程进度）。
    static MetaSaveData cached;

    public static MetaSaveData Load()
    {
        EnsureCsvLoaded();

        if (cached != null)
            return cached;

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            cached = MetaSaveData.CreateDefault();
            return cached;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        var data = JsonUtility.FromJson<MetaSaveData>(json);
        cached = data ?? MetaSaveData.CreateDefault();
        return cached;
    }

    public static void Save(MetaSaveData data)
    {
        if (data == null)
            return;

        cached = data;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        cached = null;
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    public static GameConfigData ApplyUpgrades(GameConfigData baseConfig, MetaSaveData meta)
    {
        EnsureCsvLoaded();
        var config = Object.Instantiate(baseConfig);
        float moveSpeedBonusPercent = 0f;
        float sacrificePatienceBonusPercent = 0f;

        foreach (var info in CSVLoader.GetAll())
        {
            int level = meta.GetLevel(info.identifier);
            if (level <= 0)
                continue;

            switch (info.effect)
            {
                case "worker":
                    config.totalWorkers += info.value * level;
                    break;
                case "moveSpeed":
                    moveSpeedBonusPercent += info.value * level;
                    break;
                case "zoneCapacity":
                    config.maxWorkersPerZone += info.value * level;
                    break;
                case "sacrificePatience":
                    sacrificePatienceBonusPercent += info.value * level;
                    break;
                case "time":
                    config.levelTimeSeconds += info.value * level;
                    break;
                case "customerTips":
                    config.customerTipsBonus += info.value * level;
                    break;
                case "dishPrice":
                    config.dishPriceBonus += info.value * level;
                    break;
                case "doubleCut":
                    config.doubleCutEnabled = true;
                    break;
                case "doubleCook":
                    config.doubleCookEnabled = true;
                    break;
                case "doubleSplit":
                    config.doubleSplitEnabled = true;
                    break;
                case "yummyMinion":
                    config.yummyMinionEnabled = true;
                    config.yummyMinionSatiety += info.value * level;
                    break;
                case "patienceFood":
                    config.patienceFoodPercent += info.value * level;
                    break;
            }
        }

        if (moveSpeedBonusPercent > 0f)
            config.workerMoveSpeed *= 1f + moveSpeedBonusPercent / 100f;

        if (sacrificePatienceBonusPercent > 0f)
            config.customerSacrificePatienceRestore *= 1f + sacrificePatienceBonusPercent / 100f;

        return config;
    }

    public static void ApplyUnlocks(GameModel model, MetaSaveData meta)
    {
        EnsureCsvLoaded();

        foreach (var info in CSVLoader.GetAll())
        {
            if (meta.GetLevel(info.identifier) < 1)
                continue;

            switch (info.effect)
            {
                // 升级树中第一个/第二个解锁的菜谱分别是 dishIdentifier "1" 和 "2"。
                case "vegSoup":
                    UnlockDish(model, "1");
                    break;
                case "stirFry":
                    UnlockDish(model, "2");
                    break;
                case "splitMachine":
                    model.GetZone(ZoneType.Splitter).IsUnlocked = true;
                    break;
            }
        }

        model.ActiveRecipeId.Value = GetHighestUnlockedRecipeId(model);
    }

    // 解锁某 dishIdentifier 对应的菜谱及其所需机器。
    static void UnlockDish(GameModel model, string dishIdentifier)
    {
        var recipeId = DishRecipeBuilder.GetRecipeIdForDish(model.Recipes, dishIdentifier);
        if (string.IsNullOrEmpty(recipeId))
            return;

        model.UnlockedRecipes.Add(recipeId);
        DishRecipeBuilder.UnlockZonesForRecipe(model, model.GetRecipe(recipeId));
    }

    public static bool IsSpeedUpUnlocked(MetaSaveData meta)
    {
        return true;
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

    public static bool IsLocked(MetaSaveData meta, UpgradeInfo info)
    {
        if (info == null)
            return true;

        if (string.IsNullOrEmpty(info.prev))
            return false;

        return meta.GetLevel(info.prev) < 1;
    }

    public static bool CanPurchase(MetaSaveData meta, string identifier)
    {
        var info = CSVLoader.Get(identifier);
        if (info == null)
            return false;

        int level = meta.GetLevel(identifier);
        if (level >= info.maxLevel)
            return false;

        if (IsLocked(meta, info))
            return false;

        return meta.MetaGold >= info.cost;
    }

    public static bool TryPurchase(MetaSaveData meta, string identifier)
    {
        if (!CanPurchase(meta, identifier))
            return false;

        var info = CSVLoader.Get(identifier);
        meta.MetaGold -= info.cost;
        meta.SetLevel(identifier, meta.GetLevel(identifier) + 1);
        Save(meta);
        return true;
    }

    static void EnsureCsvLoaded()
    {
        if (CSVLoader.IsInitialized)
            return;

        CSVLoader.Init();
    }
}
