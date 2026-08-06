using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Handler for Tank ultimate ability.
    /// Manages tank spawning, prefab lookup, and active count tracking.
    /// The actual tank logic remains in TankInstance.cs.
    /// </summary>
    public class TankHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Tank.ToString();

        [SerializeField] private GameObject _tankPrefab;

        public GameObject GetPrefab() => _tankPrefab;

        /// <summary>
        /// Try to spawn a tank at a destination on the attack range boundary.
        /// Handles count limits and instance creation.
        /// </summary>
        public bool TrySpawn(Player.Player player, Vector3 destination, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Check count limit
            int activeCount = UltimateFactory.GetActiveCount(UltimateId);
            if (activeCount >= ultimateData.GetCount()) return false;

            // Try to instantiate
            GameObject tankObj = Instantiate(_tankPrefab, player.transform.position, Quaternion.identity, player.transform);
            if (tankObj == null) return false;

            // Initialize tank instance
            if (!tankObj.TryGetComponent(out TankInstance tankInstance))
            {
                Destroy(tankObj);
                return false;
            }

            tankInstance.Initialize(player, destination, ultimateData.GetDuration());

            // Track active count
            UltimateFactory.IncrementActiveCount(UltimateId);
            return true;
        }

        public int GetActiveCount() => UltimateFactory.GetActiveCount(UltimateId);

        public void OnInstanceDestroyed() => UltimateFactory.OnInstanceDestroyed(UltimateId);
    }
}
