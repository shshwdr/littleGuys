using System.Linq;
using UniRx;

public class CustomerSpawnService
{
    readonly GameModel model;
    readonly WorldLayout layout;

    public CustomerSpawnService(GameModel model, WorldLayout layout)
    {
        this.model = model;
        this.layout = layout;
    }

    public void Tick(float dt)
    {
        if (model.State.Value != GameState.Playing)
            return;

        if (!model.HasSpawnedFirstCustomer)
        {
            SpawnCustomer();
            model.HasSpawnedFirstCustomer = true;
            model.CustomerSpawnTimer = 0f;
        }
        else
        {
            model.CustomerSpawnTimer += dt;
            if (model.CustomerSpawnTimer >= model.Config.customerSpawnInterval)
            {
                model.CustomerSpawnTimer = 0f;
                if (model.Customers.Count < model.Config.maxCustomers)
                    SpawnCustomer();
            }
        }

        model.PatienceTimer += dt;
        if (model.PatienceTimer >= 1f)
        {
            model.PatienceTimer -= 1f;
            foreach (var customer in model.Customers)
            {
                if (customer.IsServed)
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
    }

    void SpawnCustomer()
    {
        model.TotalCustomersSpawned++;
        int requiredSatiety = (model.TotalCustomersSpawned - 1) / 3 + 1;
        int spawnSlot = FindNextSpawnSlot();

        var customer = new CustomerData
        {
            Id = model.NextCustomerId++,
            SpawnSlotIndex = spawnSlot,
            RequiredSatiety = requiredSatiety,
            ReceivedSatiety = 0,
            MaxPatience = model.Config.customerMaxPatience
        };
        customer.Patience.Value = customer.MaxPatience;
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
        model.Customers.Remove(customer);
        model.ServedCustomerCount++;
        CheckUnlocks();
    }

    public void AddSatiety(CustomerData customer, int satiety)
    {
        if (customer == null || customer.IsServed)
            return;

        customer.ReceivedSatiety += satiety;
        if (customer.IsFullySatiated)
            ServeCustomer(customer);
    }

    void CheckUnlocks()
    {
        if (model.ServedCustomerCount >= 1 && model.UnlockedRecipes.Add("vegsoup"))
        {
            UnlockZone(ZoneType.Cook);
            model.RecipeUnlocked.OnNext("vegsoup");
        }

        if (model.ServedCustomerCount >= 3 && model.UnlockedRecipes.Add("stirfry"))
        {
            UnlockZone(ZoneType.Wok);
            model.RecipeUnlocked.OnNext("stirfry");
        }
    }

    void UnlockZone(ZoneType zoneType)
    {
        var zone = model.GetZone(zoneType);
        if (zone.IsUnlocked)
            return;

        zone.IsUnlocked = true;
        model.ZoneUnlocked.OnNext(zoneType);
    }
}
