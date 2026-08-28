using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Ultimate
{
    public class FountainHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Fountain.ToString();

        [SerializeField] private GameObject _fountainPrefab;

        public GameObject GetPrefab() => _fountainPrefab;

        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;

            // Check if ultimate is active
            if (!ultimateData.GetActive()) return false;

            // Try to instantiate
            const float GOLDEN_ANGLE = 137.507764f;

            float attackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            float offset = Random.Range(0f, 360f);

            for (int i = 0; i < ultimateData.GetCount(); i++)
            {
                float angle = (offset + i * GOLDEN_ANGLE + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                float radius = attackRange + Random.Range(0f, 1.5f);

                Vector3 spawnPos = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                SpawnOne(player, spawnPos, ultimateData);
            }
            return true;
        }

        private void SpawnOne(Player.Player player, Vector3 spawnPos, UltimateData ultimateData)
        {
            GameObject fountainObj = Instantiate(_fountainPrefab, spawnPos, Quaternion.identity, UIManager.Instance.UltimateRoot);
            if (fountainObj == null) return;

            if (!fountainObj.TryGetComponent(out FountainInstance fountainInstance))
            {
                Destroy(fountainObj);
                return;
            }

            fountainInstance.Initialize(player, ultimateData);
        }

        public int GetActiveCount() => 0; // doesn't persist, so always 0

        public void OnInstanceDestroyed() { } // doesn't need tracking
    }
}
