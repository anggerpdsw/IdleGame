using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Pet target selection system.
    /// Scores enemies based on priority rules and returns best target.
    /// Stateless utility class - no instance state.
    /// </summary>
    public static class PetTargeting
    {
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        /// <summary>
        /// Find best target for pet based on priority rules.
        /// Returns null if no valid targets in range.
        /// </summary>
        public static Transform FindBestTarget(Vector3 petPosition, Vector3 playerPosition, float targetRange, List<string> priorities)
        {
            // Physics query for all enemies in range
            Collider2D[] hits = Physics2D.OverlapCircleAll(petPosition, targetRange, EnemyLayerMask);
            if (hits.Length == 0) return null;

            // Filter valid enemies
            var validEnemies = hits
                .Where(hit => hit != null && hit.gameObject.activeInHierarchy)
                .Where(hit => hit.TryGetComponent<EnemyAi>(out var enemy) && enemy.CurrentHealth > 0)
                .Select(hit => hit.transform)
                .ToList();

            if (validEnemies.Count == 0) return null;

            // Score each enemy
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

        /// <summary>
        /// Score target based on priority rules.
        /// Higher score = higher priority.
        /// </summary>
        public static float ScoreTarget(Transform target, Vector3 petPosition, Vector3 playerPosition, List<string> priorities)
        {
            if (target == null) return float.MinValue;

            var enemy = target.GetComponent<EnemyAi>();
            if (enemy == null || enemy.CurrentHealth <= 0) return float.MinValue;

            float score = 0f;

            // Apply each priority rule with weight
            int priorityWeight = priorities.Count;
            foreach (var priority in priorities)
            {
                score += ApplyPriorityRule(priority, target, enemy, petPosition, playerPosition) * priorityWeight;
                priorityWeight--; // First priority has highest weight
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
                _ => 0f
            };
        }

        private static float ScoreClosestToPlayer(Transform target, Vector3 playerPosition)
        {
            float distance = Vector3.Distance(target.position, playerPosition);
            // Inverse distance - closer = higher score (max score at distance 0)
            return Mathf.Max(0f, 100f - distance);
        }

        private static float ScoreClosestToPet(Transform target, Vector3 petPosition)
        {
            float distance = Vector3.Distance(target.position, petPosition);
            return Mathf.Max(0f, 100f - distance);
        }

        private static float ScoreElite(EnemyAi enemy)
        {
            // No Elite role in current enum - return 0
            return 0f;
        }

        private static float ScoreBoss(EnemyAi enemy)
        {
            // Boss enemies get highest priority
            return enemy.Role == Role.BOSS ? 100f : 0f;
        }

        private static float ScoreHighestHp(EnemyAi enemy)
        {
            // Normalize HP to 0-50 range
            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            return hpPercent * 50f;
        }

        private static float ScoreLowestHp(EnemyAi enemy)
        {
            // Inverse of highest HP
            float hpPercent = enemy.CurrentHealth / enemy.MaxHealth;
            return (1f - hpPercent) * 50f;
        }
    }
}
