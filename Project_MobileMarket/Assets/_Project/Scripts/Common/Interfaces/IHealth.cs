namespace MobileMarket.Common
{
    /// <summary>Anything with hit points that can die (enemies, the player).</summary>
    public interface IHealth
    {
        bool IsDead { get; }
    }
}
