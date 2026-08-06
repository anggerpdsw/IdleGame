using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    public class RootHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Root.ToString();

        [SerializeField] private GameObject _rootPrefab;

        public GameObject GetPrefab() => _rootPrefab;

        /// <summary>
        /// Try to spawn a root at the player's position.
        /// Root doesn't need to check chance/count (handled by manager with cooldown).
        /// </summary>
        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Try to instantiate
            GameObject rootObj = Instantiate(_rootPrefab, position, Quaternion.identity, player.transform);
            if (rootObj == null) return false;

            // Initialize root instance
            if (!rootObj.TryGetComponent(out RootInstance rootInstance))
            {
                Destroy(rootObj);
                return false;
            }

            rootInstance.Initialize(player, ultimateData);

            // This auto-destroys after its effect duration, so we don't track active count
            // (This doesn't have a persistent "active count" like Bomb or ToxicCloud)
            return true;
        }

        public int GetActiveCount() => 0; // doesn't persist, so always 0

        public void OnInstanceDestroyed() { } // doesn't need tracking
    }
}
