using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Ultimate
{
    public class CloudHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Cloud.ToString();

        [SerializeField] private GameObject _cloudPrefab;

        public GameObject GetPrefab() => _cloudPrefab;

        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Check count limit
            int activeCount = UltimateFactory.GetActiveCount(UltimateId);
            if (activeCount >= ultimateData.GetCount()) return false;

            // Try to instantiate
            GameObject cloudObj = Instantiate(_cloudPrefab, position, Quaternion.identity, UIManager.Instance.UltimateRoot);
            if (cloudObj == null) return false;

            // Initialize cloud instance
            if (!cloudObj.TryGetComponent(out CloudInstance cloudInstance))
            {
                Destroy(cloudObj);
                return false;
            }

            cloudInstance.Initialize(player, ultimateData);

            // Flip sprite if needed
            if (cloudObj.TryGetComponent(out SpriteRenderer sr))
                sr.flipX = position.x < player.transform.position.x;

            // Track active count
            UltimateFactory.IncrementActiveCount(UltimateId);
            return true;
        }

        public int GetActiveCount() => UltimateFactory.GetActiveCount(UltimateId);

        public void OnInstanceDestroyed() => UltimateFactory.OnInstanceDestroyed(UltimateId);
    }
}
