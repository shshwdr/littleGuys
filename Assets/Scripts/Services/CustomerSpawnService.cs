using System.Linq;
using UniRx;

public class CustomerSpawnService
{
    readonly GameModel model;

    public CustomerSpawnService(GameModel model)
    {
        this.model = model;
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
        var recipe = model.ActiveRecipe.Value;
        var orderName = recipe != null ? recipe.DisplayName : "Soup";

        var customer = new CustomerData
        {
            Id = model.NextCustomerId++,
            OrderName = orderName,
            MaxPatience = model.Config.customerMaxPatience
        };
        customer.Patience.Value = customer.MaxPatience;
        model.Customers.Add(customer);
    }

    public CustomerData GetFirstWaitingCustomer()
    {
        return model.Customers.FirstOrDefault(c => !c.IsServed);
    }

    public void ServeCustomer(CustomerData customer)
    {
        if (customer == null || customer.IsServed)
            return;

        customer.IsServed = true;
        model.Customers.Remove(customer);
    }
}
