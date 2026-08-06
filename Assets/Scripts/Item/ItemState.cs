
namespace IdleDefenseSurvival.Item
{
    /// <summary>
    /// Enum representing the lifecycle states of a currency item.
    /// Replaces multiple boolean flags for cleaner state management.
    /// </summary>
    public enum ItemState
    {
        /// <summary>Item is spawning with spread animation</summary>
        Spawning,

        /// <summary>Item is hovering idle, waiting for click or magnetic collection</summary>
        Idle,

        /// <summary>Item has been clicked/triggered, starting collection sequence</summary>
        Collecting,

        /// <summary>Item is moving towards player with shrink animation</summary>
        MovingToPlayer,

        /// <summary>Item reached player, adding currency and cleaning up</summary>
        CollectingCurrency,

        /// <summary>Item is being returned to pool or destroyed</summary>
        Despawning
    }
}