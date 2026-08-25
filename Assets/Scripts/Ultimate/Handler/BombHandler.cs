using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Handler for Bomb ultimate ability.
    /// Manages bomb spawning, prefab lookup, and active count tracking.
    /// The actual bomb explosion logic remains in BombInstance.cs.
    /// </summary>
    public class BombHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Bomb.ToString();

        [SerializeField] private GameObject _bombPrefab;

        public GameObject GetPrefab() => _bombPrefab;

        /// <summary>
        /// Try to spawn a bomb at the given position.
        /// Handles chance checks, count limits, and instance creation.
        /// </summary>
        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Check count limit
            int activeCount = UltimateFactory.GetActiveCount(UltimateId);
            if (activeCount >= ultimateData.GetCount()) return false;

            // Try to instantiate
            GameObject bombObj = Instantiate(_bombPrefab, position, Quaternion.identity, player.transform);
            if (bombObj == null) return false;

            // Initialize bomb instance
            if (!bombObj.TryGetComponent(out BombInstance bombInstance))
            {
                Destroy(bombObj);
                return false;
            }

            bombInstance.Initialize(player, ultimateData);

            // Flip sprite if needed
            if (bombObj.TryGetComponent(out SpriteRenderer sr))
                sr.flipX = position.x < player.transform.position.x;

            // Track active count
            UltimateFactory.IncrementActiveCount(UltimateId);
            return true;
        }

        public int GetActiveCount() => UltimateFactory.GetActiveCount(UltimateId);

        public void OnInstanceDestroyed() => UltimateFactory.OnInstanceDestroyed(UltimateId);
    }
}
