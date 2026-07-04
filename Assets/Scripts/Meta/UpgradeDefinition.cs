public readonly struct UpgradeDefinition
{
    public UpgradeId Id { get; }
    public string DisplayName { get; }
    public int MaxLevel { get; }
    public int Price { get; }

    public UpgradeDefinition(UpgradeId id, string displayName, int maxLevel, int price)
    {
        Id = id;
        DisplayName = displayName;
        MaxLevel = maxLevel;
        Price = price;
    }

    public static readonly UpgradeDefinition[] All =
    {
        new UpgradeDefinition(UpgradeId.InitialWorkers, "Initial Workers +1", 10, 1),
        new UpgradeDefinition(UpgradeId.UnlockVegSoup, "Unlock Veg Soup", 1, 2),
        new UpgradeDefinition(UpgradeId.UnlockSplitter, "Unlock Splitter", 1, 3),
        new UpgradeDefinition(UpgradeId.MoveSpeed, "Move Speed +10%", 10, 2),
        new UpgradeDefinition(UpgradeId.UnlockStirFry, "Unlock Stir Fry", 1, 5)
    };

    public static UpgradeDefinition Get(UpgradeId id) => All[(int)id];
}
