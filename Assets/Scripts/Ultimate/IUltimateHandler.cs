using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Interface for all ultimate abilities.
    /// Each ultimate implements this to define its spawn behavior and lifecycle.
    /// </summary>
    public interface IUltimateHandler
    {
        /// <summary>
        /// Unique identifier for this ultimate (e.g., "bomb", "tank", "shockwave").
        /// </summary>
        string UltimateId { get; }

        /// <summary>
        /// Attempt to spawn this ultimate at the given position.
        /// Returns true if spawn was successful, false otherwise.
        /// 
        /// This method should:
        /// 1. Check if ultimate can spawn (active, within count limit, cooldown ready)
        /// 2. Instantiate the ultimate GameObject
        /// 3. Initialize with player data and ultimate config
        /// 4. Register with factory for active count tracking
        /// 5. Return true on success
        /// </summary>
        bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData);

        /// <summary>
        /// Get the current number of active instances of this ultimate.
        /// </summary>
        int GetActiveCount();

        /// <summary>
        /// Notify factory when an instance is destroyed.
        /// Called by the ultimate itself when it's about to be destroyed.
        /// </summary>
        void OnInstanceDestroyed();

        /// <summary>
        /// Get the prefab asset for this ultimate.
        /// Returns null if prefab is not found.
        /// </summary>
        GameObject GetPrefab();
    }
}
