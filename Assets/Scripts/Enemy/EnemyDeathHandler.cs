using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Mission;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.UI;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Handles enemy death sequence in exact order.
    /// 10-step process: record kill → ultimate triggers → cleanup → rewards → mission → destroy.
    /// Order is critical - DO NOT CHANGE.
    /// </summary>
    public static class EnemyDeathHandler
    {
        /// <summary>
        /// Execute full death sequence.
        /// Preserves exact order from original EnemyAi.Die().
        /// </summary>
        public static void ProcessDeath(EnemyAi enemy, string lastDamageSource)
        {
            // Step 1: Record kill in save system
            RecordEnemyKill(enemy, lastDamageSource);

            // Step 2-3: Ultimate triggers (Lightning, Cloud)
            ProcessUltimateTriggers(enemy, lastDamageSource);

            // Step 4-6: Cleanup registrations
            CleanupRegistrations(enemy);

            // Step 7-8: Drop rewards and materials
            DropAllRewards(enemy);

            // Step 9: Update missions
            UpdateMissions(enemy);

            // Step 9b: Necromancer special death handling
            if (enemy.EnemyData?.id == Behavior.Necromancer.ToString() &&
                enemy.TryGetComponent<NecromancerBehavior>(out var necroBehavior))
            {
                // Spawn grave, keep enemy inactive for revive
                necroBehavior.SpawnGrave(enemy.transform.position);
                enemy.gameObject.SetActive(false);
                // Skip destroy – Grave will handle cleanup after revive
                return;
            }

            // Step 10: Destroy game object (regular enemies)
            Object.Destroy(enemy.gameObject);
        }

        /// <summary>
        /// Record kill in save system, grouped by role.
        /// </summary>
        private static void RecordEnemyKill(EnemyAi enemy, string damageSource)
        {
            var enemyData = enemy.EnemyData;
            if (enemyData == null || string.IsNullOrEmpty(enemyData.id)) return;

            var saveManager = enemy.SaveMgr;
            var waveManager = enemy.WaveMgr;

            if (saveManager != null)
            {
                saveManager.RecordEnemyKill(enemyData.id, damageSource, enemy.Role.ToString());
                if (waveManager != null)
                    saveManager.AddKills(waveManager.CurrentTier, 1);
            }
        }

        /// <summary>
        /// Trigger ultimates based on damage source.
        /// Lightning: player/lightning source → register kill → spawn if threshold met.
        /// Cloud: player/cloud source → generate stack at death position.
        /// </summary>
        private static void ProcessUltimateTriggers(EnemyAi enemy, string lastDamageSource)
        {
            var ultimateManager = enemy.UltimateMgr;
            if (ultimateManager == null) return;

            var playerComponent = Player.Player.Instance;
            if (playerComponent == null) return;

            string player = UltimateDMG.Player.ToString();
            string lightning = UltimateDMG.Lightning.ToString();
            string cloud = UltimateDMG.Cloud.ToString();

            // Lightning trigger (killed by player or lightning)
            if (lastDamageSource == player || lastDamageSource == lightning)
            {
                if (LightningHandler.RegisterKill())
                {
                    // Lightning ready - spawn at player position
                    ultimateManager.TrySpawn(lightning, playerComponent.transform.position, playerComponent);
                }
            }

            // Cloud trigger (killed by player or cloud)
            if (lastDamageSource == player || lastDamageSource == cloud)
            {
                ultimateManager.TryGenerateStack(cloud, playerComponent, enemy.transform.position);
            }
        }

        /// <summary>
        /// Unregister from all manager systems.
        /// </summary>
        private static void CleanupRegistrations(EnemyAi enemy)
        {
            // Health bar
            var healthBarManager = EnemyHealthBarManager.Instance;
            if (healthBarManager != null)
                healthBarManager.UnregisterEnemy(enemy);

            // Statistics
            var statisticsManager = EnemyStatisticsManager.Instance;
            if (statisticsManager != null)
                statisticsManager.Unregister(enemy);

            // Aura manager (clean up active auras from this enemy)
            var auraManager = EnemyAuraManager.Instance;
            if (auraManager != null)
                auraManager.OnEnemyDeath(enemy);
        }

        /// <summary>
        /// Drop currency rewards and material items.
        /// </summary>
        private static void DropAllRewards(EnemyAi enemy)
        {
            // Currency rewards (gold/exp instant, gem/meat pickups)
            EnemyRewardDistributor.DropRewards(
                enemy.GoldReward,
                enemy.GemReward,
                enemy.MeatReward,
                enemy.ExpReward,
                enemy.transform.position,
                enemy.ItemPrefab,
                enemy.gameObject.name,
                enemy.SaveMgr,
                enemy.EconomyMgr);

            // Material drops
            EnemyRewardDistributor.DropMaterialItems(enemy.EnemyData);
        }

        /// <summary>
        /// Update mission progress based on enemy type.
        /// </summary>
        private static void UpdateMissions(EnemyAi enemy)
        {
            var enemyData = enemy.EnemyData;
            if (enemyData == null) return;

            var missionService = MissionService.Instance;
            if (missionService == null) return;

            // Boss kill mission
            if (enemyData.IsBoss)
            {
                missionService.UpdateProgress(MissionEventType.BossKilled, enemyData.id, 1);
                return;
            }

            // Generic enemy kill mission
            missionService.UpdateProgress(MissionEventType.EnemyKilled, enemyData.id, 1);

            // Specific enemy kill mission (e.g., "Kill X Goblins")
            missionService.UpdateProgress(MissionEventType.SpecificEnemyKilled, enemyData.id, 1);
        }
    }
}
