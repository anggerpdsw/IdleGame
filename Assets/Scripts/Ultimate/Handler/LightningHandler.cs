using System;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Controller;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Handler for Lightning ultimate ability.
    /// Triggers every N kills (configurable via triggerKillCount), then chains through up to 6 enemies
    /// with a 0.05s delay between each bounce. The final (6th) strike is the climactic strike
    /// with 2x damage and longer stun.
    /// </summary>
    public class LightningHandler : MonoBehaviour, IUltimateHandler
    {
        public string UltimateId => UltimateDMG.Lightning.ToString();

        [SerializeField] private GameObject _lightningPrefab;

        public GameObject GetPrefab() => _lightningPrefab;

        // Static kill counter shared across all instances
        private static int _killCountSinceLastLightning = 0;
        private static int _triggerKillCount = 0;

        public static event Action<float> OnLightningProgressChanged;
        public static event Action OnLightningReady;

        /// <summary>
        /// Current progress toward next lightning trigger (0.0 to 1.0).
        /// </summary>
        public static float Progress
        {
            get
            {
                if (_triggerKillCount <= 0) return 0f;
                return Mathf.Clamp01((float)_killCountSinceLastLightning / _triggerKillCount);
            }
        }

        /// <summary>
        /// Call when an enemy is killed to track progress toward lightning trigger.
        /// Returns true if lightning stack was generated (kill count reached threshold).
        /// </summary>
        public static bool RegisterKill()
        {
            string ultimateId = UltimateDMG.Lightning.ToString();
            var lightningData = UltimateManager.Instance?.GetUltimate(ultimateId);

            if (lightningData == null || !lightningData.GetActive()) return false;
            _triggerKillCount = lightningData.GetTriggerKillCount(20);
            _killCountSinceLastLightning++;
            float progress = (float)_killCountSinceLastLightning / _triggerKillCount;
            OnLightningProgressChanged?.Invoke(Mathf.Clamp01(progress));

            bool stackGenerated = false;
            if (_killCountSinceLastLightning >= _triggerKillCount)
            {
                _killCountSinceLastLightning = 0;
                OnLightningProgressChanged?.Invoke(0f);
                OnLightningReady?.Invoke();

                // Generate stack via UltimateManager (handles auto-cast internally)
                var player = Player.Player.Instance;
                if (player != null)
                {
                    stackGenerated = UltimateManager.Instance?.TryGenerateTriggerStack(ultimateId, player) ?? false;
                }
            }
            return stackGenerated;
        }

        public bool TrySpawn(Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (player == null || ultimateData == null) return false;
            if (!ultimateData.GetActive()) return false;

            SpawnOne(player, position, ultimateData);
            return true;
        }

        private void SpawnOne(Player.Player player, Vector3 spawnPos, UltimateData ultimateData)
        {
            GameObject lightningObj = Instantiate(_lightningPrefab, spawnPos, Quaternion.identity, player.transform);
            if (lightningObj == null) return;

            if (!lightningObj.TryGetComponent(out LightningInstance lightningInstance))
            {
                Destroy(lightningObj);
                return;
            }

            lightningInstance.Initialize(player, ultimateData);
        }

        public int GetActiveCount() => 0; // Doesn't persist, instantaneous chain effect

        public void OnInstanceDestroyed() { } // Doesn't need tracking
    }
}