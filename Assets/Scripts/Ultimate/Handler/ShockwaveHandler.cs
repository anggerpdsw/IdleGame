using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Handler for Shockwave ultimate ability.
    /// Manages shockwave spawning, prefab lookup, and active count tracking.
    /// The actual shockwave logic remains in ShockwaveInstance.cs.
    /// </summary>
    public class ShockwaveHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Shockwave.ToString();

        [SerializeField] private GameObject _shockwavePrefab;

        public GameObject GetPrefab() => _shockwavePrefab;

        /// <summary>
        /// Try to spawn a shockwave at the player's position.
        /// Shockwave doesn't need to check chance/count (handled by manager with cooldown).
        /// </summary>
        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Try to instantiate
            GameObject shockwaveObj = Instantiate(_shockwavePrefab, position, Quaternion.identity, player.transform);
            if (shockwaveObj == null) return false;

            // Initialize shockwave instance
            if (!shockwaveObj.TryGetComponent(out ShockwaveInstance shockwaveInstance))
            {
                Destroy(shockwaveObj);
                return false;
            }

            shockwaveInstance.Initialize(player, ultimateData);

            // This auto-destroys after its effect duration, so we don't track active count
            // (This doesn't have a persistent "active count" like Bomb or ToxicCloud)
            return true;
        }

        public int GetActiveCount() => 0; // doesn't persist, so always 0

        public void OnInstanceDestroyed() { } // doesn't need tracking
    }
}
