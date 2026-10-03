using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet
{
    public static class PetTargeting
    {
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        /// <summary>
        /// Find best target for pet. Scan from PLAYER position with extended radius.
        /// Includes target lock/hysteresis to prevent flickering.
        /// Uses EnemySpawner's active enemy cache for O(1) retrieval (zero allocation).
        /// </summary>
        public static Transform FindBestTarget(
            Vector3 petPosition,
            Vector3 playerPosition,
            float targetRange,
            List<string> priorities,
            PetRuntime petRuntime = null)
        {
            // Scan radius covers full threat circle around player
            float scanRadius = targetRange + 3f; // Max orbitRadius from data is 3.0

            // Use EnemySpawner's spatial grid for zero-allocation enemy retrieval
            var spawner = EnemySpawner.Instance;
            List<EnemyAi> activeEnemies = null;

            if (spawner != null)
            {
                activeEnemies = spawner.GetActiveEnemies();
            }

            if (activeEnemies == null || activeEnemies.Count == 0)
                return null;

            var validEnemies = s_validEnemyList;
            validEnemies.Clear();

            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                if (enemy.CurrentHealth <= 0) continue;

                // Filter by scan radius from player position
                float distToPlayer = Vector3.Distance(enemy.transform.position, playerPosition);
                if (distToPlayer > scanRadius) continue;

                validEnemies.Add(enemy.transform);
            }

            if (validEnemies.Count == 0) return null;

            // TARGET LOCK: Keep current target if still valid and not significantly worse
            if (petRuntime != null && petRuntime.Target != null && petRuntime.IsTargetValid())
            {
                float currentScore = ScoreTarget(petRuntime.Target, petPosition, playerPosition, priorities);
                float lockBestScore = currentScore;
                Transform lockBestTarget = petRuntime.Target;

                foreach (var enemy in validEnemies)
                {
                    if (enemy == petRuntime.Target) continue;
                    float score = ScoreTarget(enemy, petPosition, playerPosition, priorities);
                    if (score > lockBestScore * petRuntime.TargetSwitchThreshold)
                    {
                        lockBestScore = score;
                        lockBestTarget = enemy;
                    }
                }

                return lockBestTarget;
            }

            // No lock or lock broken — find best fresh target
            Transform bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var enemy in validEnemies)
            {
                float score = ScoreTarget(enemy, petPosition, playerPosition, priorities);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        private static readonly List<Transform> s_validEnemyList = new(32);

        public static float ScoreTarget(Transform target, Vector3 petPosition, Vector3 playerPosition, List<string> priorities)
        {
            if (target == null) return float.MinValue;

            var enemy = target.GetComponent<EnemyAi>();
            if (enemy == null || enemy.CurrentHealth <= 0) return float.MinValue;

            float score = 0f;
            int priorityWeight = priorities.Count;

            foreach (var priority in priorities)
            {
                score += ApplyPriorityRule(priority, target, enemy, petPosition, playerPosition) * priorityWeight;
                priorityWeight--;
            }

            return score;
        }

        private static float ApplyPriorityRule(string rule, Transform target, EnemyAi enemy, Vector3 petPosition, Vector3 playerPosition)
        {
            return rule switch
            {
                "ClosestToPlayer" => ScoreClosestToPlayer(target, playerPosition),
                "ClosestToPet" => ScoreClosestToPet(target, petPosition),
                "Elite" => ScoreElite(enemy),
                "Boss" => ScoreBoss(enemy),
                "HighestHp" => ScoreHighestHp(enemy),
                "LowestHp" => ScoreLowestHp(enemy),
                "Cluster" => ScoreCluster(target, playerPosition),
                _ => 0f
            };
        }

        private static float ScoreClosestToPlayer(Transform target, Vector3 playerPosition)
        {
            float distance = Vector3.Distance(target.position, playerPosition);
            return Mathf.Max(0f, 100f - distance);
        }

        private static float ScoreClosestToPet(Transform target, Vector3 petPosition)
        {
            float distance = Vector3.Distance(target.position, petPosition);
            return Mathf.Max(0f, 100f - distance);
        }

        private static float ScoreElite(EnemyAi enemy)
        {
            // EnemyData.IsElite checks enemyType == EnemyType.IsElite
            return enemy.EnemyData.IsElite ? 50f : 0f;
        }

        private static float ScoreBoss(EnemyAi enemy)
        {
            return enemy.Role == Role.BOSS ? 100f : 0f;
        }

        private static float ScoreHighestHp(EnemyAi enemy)
        {
            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            return hpPercent * 50f;
        }

        private static float ScoreLowestHp(EnemyAi enemy)
        {
            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            return (1f - hpPercent) * 50f;
        }

        private static float ScoreCluster(Transform target, Vector3 playerPosition)
        {
            // Count nearby enemies using spatial grid (reuse spawner buffer)
            var spawner = EnemySpawner.Instance;
            if (spawner != null)
            {
                var enemies = spawner.GetActiveEnemies();
                int clusterCount = 0;
                Vector3 center = target.position;
                float clusterRadius = 3f;
                float clusterRadiusSq = clusterRadius * clusterRadius;

                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                    if (enemy.CurrentHealth <= 0) continue;

                    Vector3 diff = enemy.transform.position - center;
                    if (diff.sqrMagnitude <= clusterRadiusSq)
                        clusterCount++;
                }

                return Mathf.Min(clusterCount * 15f, 75f);
            }

            // Fallback: use Physics2D (should rarely happen)
            Collider2D[] buffer = new Collider2D[32];
            ContactFilter2D filter = new();
            filter.SetLayerMask(EnemyLayerMask);
            filter.useLayerMask = true;

            int fallbackCount = Physics2D.OverlapCircle(
                target.position,
                3f,
                filter,
                buffer);
            int fallbackClusterCount = 0;
            for (int i = 0; i < fallbackCount; i++)
            {
                if (buffer[i] != null && buffer[i].TryGetComponent<EnemyAi>(out var e) && e.CurrentHealth > 0)
                    fallbackClusterCount++;
            }
            return Mathf.Min(fallbackClusterCount * 15f, 75f);
        }
    }
}