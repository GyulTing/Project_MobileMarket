namespace MobileMarket.Enemy
{
    public enum Faction
    {
        Player,
        Bandit,
        Paramilitary,
        Zombie,
    }

    public static class FactionRelations
    {
        /// <summary>Different factions fight each other; members of the same faction never do.</summary>
        public static bool IsHostile(Faction a, Faction b) => a != b;
    }
}
