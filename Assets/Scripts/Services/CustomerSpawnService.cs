using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class CustomerSpawnService
{
    readonly GameModel model;
    readonly WorldLayout layout;
    readonly List<LevelInfo> levelRows = new List<LevelInfo>();

    public event Action SceneCompleted;
    public event Action<CustomerData> CustomerReadyToDepart;

    public CustomerSpawnService(GameModel model, WorldLayout layout, int sceneId)
    {
        this.model = model;
        this.layout = layout;
        model.CurrentSceneId = sceneId;
        levelRows.AddRange(CSVLoader.GetLevelRows(sceneId));
        model.CurrentSpawnInterval = GetRowInterval(0);
        model.LevelTimeRemaining = model.Config.levelTimeSeconds;
        model.LevelTimeChanged.Value = model.LevelTimeRemaining;
        model.SceneProgressChanged.Value = model.SceneProgress;
        model.BossFightChanged.Value = model.BossHasSpawned || model.InBossFight;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        TickLevelTimer(dt);
        TickSpawning(dt);
        TickPatience(dt);
    }

    void TickLevelTimer(float dt)
    {
        if (model.LevelTimeRemaining <= 0f)
            return;

        model.LevelTimeRemaining -= dt;
        if (model.LevelTimeRemaining <= 0f)
        {
            model.LevelTimeRemaining = 0f;
            model.State.Value = GameState.TimeOut;
        }

        model.LevelTimeChanged.Value = model.LevelTimeRemaining;
    }

    void TickSpawning(float dt)
    {
        if (ShouldBlockNormalSpawning())
            return;

        if (!model.HasSpawnedFirstCustomer)
        {
            SpawnNextCustomer();
            model.HasSpawnedFirstCustomer = true;
            model.CustomerSpawnTimer = 0f;
            return;
        }

        if (model.Customers.Count == 0)
        {
            TrySpawnWhenAllCustomersGone();
            return;
        }

        model.CustomerSpawnTimer += dt;
        if (model.CustomerSpawnTimer < model.CurrentSpawnInterval)
            return;

        model.CustomerSpawnTimer = 0f;
        if (model.Customers.Count < model.Config.maxCustomers)
            SpawnNextCustomer();
    }

    void TrySpawnWhenAllCustomersGone()
    {
        if (model.Customers.Count > 0 || ShouldBlockNormalSpawning())
            return;

        SpawnNextCustomer();
        model.CustomerSpawnTimer = 0f;
    }

    bool ShouldBlockNormalSpawning()
    {
        return model.BossHasSpawned;
    }

    float GetRowInterval(int rowIndex)
    {
        if (levelRows.Count == 0)
            return 30f;

        rowIndex = Mathf.Clamp(rowIndex, 0, levelRows.Count - 1);
        return levelRows[rowIndex].interval;
    }

    void TickPatience(float dt)
    {
        model.PatienceTimer += dt;
        if (model.PatienceTimer < 1f)
            return;

        model.PatienceTimer -= 1f;
        foreach (var customer in model.Customers)
        {
            if (customer.IsServed || customer.IsInSilhouettePerformance)
                continue;

            customer.Patience.Value -= model.Config.patienceDecayPerSecond;
            if (customer.Patience.Value <= 0f)
            {
                customer.Patience.Value = 0f;
                model.State.Value = GameState.GameOver;
                return;
            }
        }
    }

    void SpawnNextCustomer()
    {
        if (ShouldBlockNormalSpawning())
            return;

        if (levelRows.Count == 0)
        {
            SpawnCustomerFromEntry("normal_1", false);
            return;
        }

        int rowIndex = Mathf.Clamp(model.LevelEncounterRowIndex, 0, levelRows.Count - 1);
        var row = levelRows[rowIndex];
        if (row.encounters == null || row.encounters.Count == 0)
            return;

        int customerIndex = model.LevelEncounterCustomerIndex;
        if (customerIndex >= row.encounters.Count)
        {
            AdvanceEncounterRow();
            rowIndex = Mathf.Clamp(model.LevelEncounterRowIndex, 0, levelRows.Count - 1);
            row = levelRows[rowIndex];
            customerIndex = model.LevelEncounterCustomerIndex;
        }

        string entry = row.encounters[customerIndex];
        SpawnCustomerFromEntry(entry, false);
        model.CurrentSpawnInterval = GetRowInterval(rowIndex);

        model.LevelEncounterCustomerIndex++;
        if (model.LevelEncounterCustomerIndex >= row.encounters.Count)
            AdvanceEncounterRow();
    }

    void AdvanceEncounterRow()
    {
        model.LevelEncounterCustomerIndex = 0;
        if (model.LevelEncounterRowIndex < levelRows.Count - 1)
            model.LevelEncounterRowIndex++;
    }

    void SpawnBoss()
    {
        if (model.BossHasSpawned)
            return;

        var sceneInfo = CSVLoader.GetScene(model.CurrentSceneId);
        if (sceneInfo == null || string.IsNullOrEmpty(sceneInfo.boss))
            return;

        model.BossHasSpawned = true;
        model.BossFightChanged.Value = true;
        SpawnCustomerFromEntry(sceneInfo.boss, true);
        model.InBossFight = true;
    }

    void SpawnCustomerFromEntry(string entry, bool isBoss)
    {
        model.TotalCustomersSpawned++;
        CSVLoader.ParseEncounterEntry(entry, out string typeId, out int requiredFull);

        var customerInfo = CSVLoader.GetCustomer(typeId);
        string displayName = customerInfo != null ? customerInfo.GetDisplayText() : typeId;
        if (string.IsNullOrEmpty(displayName))
            displayName = typeId;

        int spawnSlot = FindNextSpawnSlot();
        var customer = new CustomerData
        {
            Id = model.NextCustomerId++,
            Name = displayName,
            CustomerTypeId = typeId,
            Effect = customerInfo != null ? customerInfo.effect : null,
            EffectValue = customerInfo != null ? customerInfo.value : 0,
            EffectTimer = 0f,
            IsBoss = isBoss,
            SpawnSlotIndex = spawnSlot,
            RequiredSatiety = requiredFull,
            ReceivedSatiety = 0,
            MaxPatience = model.Config.customerMaxPatience
        };
        customer.Patience.Value = customer.MaxPatience;
        customer.IsAwaitingEntrance = true;
        model.Customers.Add(customer);
    }

    int FindNextSpawnSlot()
    {
        var usedSlots = model.Customers
            .Where(c => c.SpawnSlotIndex >= 0)
            .Select(c => c.SpawnSlotIndex)
            .ToHashSet();

        int maxSlots = layout.CustomerSlotCount > 0
            ? layout.CustomerSlotCount
            : model.Config.maxCustomers;

        for (int i = 0; i < maxSlots; i++)
        {
            if (!usedSlots.Contains(i))
                return i;
        }

        return model.Customers.Count;
    }

    public CustomerData GetFirstWaitingCustomer()
    {
        return model.Customers.FirstOrDefault(c => !c.IsServed && !c.IsFullySatiated);
    }

    public void ServeCustomer(CustomerData customer)
    {
        if (customer == null || customer.IsServed)
            return;

        customer.IsServed = true;
        model.Gold.Value += customer.ReceivedSatiety;

        var customerInfo = CSVLoader.GetCustomer(customer.CustomerTypeId);
        if (customerInfo != null && customerInfo.@base > 0)
            model.Gold.Value += customerInfo.@base;

        model.Customers.Remove(customer);
        model.ServedCustomerCount++;

        if (customer.IsBoss)
        {
            model.InBossFight = false;
            model.BossFightChanged.Value = false;
            SceneCompleted?.Invoke();
            model.State.Value = GameState.LevelComplete;
            return;
        }

        var sceneInfo = CSVLoader.GetScene(model.CurrentSceneId);
        int sceneFull = sceneInfo != null ? sceneInfo.full : 6;
        model.SceneProgress++;
        model.SceneProgressChanged.Value = model.SceneProgress;

        if (model.SceneProgress >= sceneFull && !model.BossHasSpawned)
        {
            SpawnBoss();
            return;
        }

        TrySpawnWhenAllCustomersGone();
    }

    public void AddSatiety(CustomerData customer, int satiety)
    {
        if (customer == null || customer.IsServed)
            return;

        customer.ReceivedSatiety += satiety;
        if (customer.IsFullySatiated)
            CustomerReadyToDepart?.Invoke(customer);
    }

    public void ResetLevelState(int sceneId)
    {
        levelRows.Clear();
        levelRows.AddRange(CSVLoader.GetLevelRows(sceneId));
        model.CurrentSceneId = sceneId;
        model.SceneProgress = 0;
        model.BossHasSpawned = false;
        model.InBossFight = false;
        model.LevelEncounterRowIndex = 0;
        model.LevelEncounterCustomerIndex = 0;
        model.HasSpawnedFirstCustomer = false;
        model.CustomerSpawnTimer = 0f;
        model.CurrentSpawnInterval = GetRowInterval(0);
        model.LevelTimeRemaining = model.Config.levelTimeSeconds;
        model.SceneProgressChanged.Value = 0;
        model.BossFightChanged.Value = false;
        model.LevelTimeChanged.Value = model.LevelTimeRemaining;
    }

    public void CheatTriggerBossFight()
    {
        if (model.State.Value != GameState.Playing)
            return;

        if (model.BossHasSpawned)
        {
            var boss = model.Customers.FirstOrDefault(c => c.IsBoss && !c.IsServed);
            if (boss != null)
                AddSatiety(boss, boss.RequiredSatiety - boss.ReceivedSatiety);
            return;
        }

        var sceneInfo = CSVLoader.GetScene(model.CurrentSceneId);
        int sceneFull = sceneInfo != null ? sceneInfo.full : 6;

        model.SceneProgress = sceneFull;
        model.SceneProgressChanged.Value = model.SceneProgress;
        SpawnBoss();
    }
}
