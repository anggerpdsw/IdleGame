using UnityEngine;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Pure movement math for enemy steering.
    /// All methods stateless - no allocations in hot path.
    /// Formulas preserved exactly from EnemyAi original implementation.
    /// </summary>
    public static class EnemyMovementCalculator
    {
        /// <summary>
        /// Calculate seek force toward target.
        /// Returns zero vector if already within attack range (stop moving).
        /// </summary>
        public static Vector2 CalculateSeek(Vector2 enemyPos, Vector2 targetPos, float attackRange, float moveSpeed)
        {
            float distance = Vector2.Distance(enemyPos, targetPos);
            if (distance <= attackRange) return Vector2.zero;
            return (targetPos - enemyPos).normalized * moveSpeed;
        }

        /// <summary>
        /// Calculate flee force away from target.
        /// Used during regeneration aura flee behavior.
        /// </summary>
        public static Vector2 CalculateFlee(Vector2 enemyPos, Vector2 targetPos, float moveSpeed)
        {
            return (enemyPos - targetPos).normalized * moveSpeed;
        }

        /// <summary>
        /// Calculate separation force from nearby enemies using spatial grid.
        /// Linear falloff: strength = (1 - distance/radius) * weight.
        /// Clamped to moveSpeed magnitude.
        /// </summary>
        public static Vector2 CalculateSeparation(
            EnemyAi self,
            Vector2 enemyPos,
            Vector2Int cell,
            float separationRadius,
            float separationWeight,
            float moveSpeed)
        {
            // Safety: if move speed is 0 or negative, no separation needed
            if (moveSpeed <= 0f) return Vector2.zero;

            Vector2 separationSum = Vector2.zero;

            // Get all enemies in 3x3 neighbor cells
            var neighbors = EnemySpatialGrid.GetNeighborsInCells(cell);

            foreach (var other in neighbors)
            {
                if (other == self || other == null) continue;

                Vector2 diff = enemyPos - (Vector2)other.transform.position;
                float distance = diff.magnitude;

                // Only apply separation if within radius AND in nearby cells
                if (distance > 0.01f && distance < separationRadius)
                {
                    // Linear falloff (more stable than inverse square)
                    // Strength = (1 - distance/radius) * weight
                    // When distance = 0 → strength = weight (max push)
                    // When distance = radius → strength = 0 (no push)
                    float strength = (1f - distance / separationRadius) * separationWeight;
                    separationSum += diff.normalized * strength;
                }
            }

            // Don't normalize, let magnitude be natural from sum
            // Just clamp so it doesn't exceed move speed
            if (separationSum.magnitude > moveSpeed)
                separationSum = separationSum.normalized * moveSpeed;

            return separationSum;
        }

        /// <summary>
        /// Calculate final velocity from seek/flee and separation forces.
        /// Prioritizes separation when neighbors are close (exponential falloff on seek).
        /// Clamps final result to moveSpeed.
        /// </summary>
        public static Vector2 CalculateFinalVelocity(Vector2 seek, Vector2 separation, float moveSpeed)
        {
            // Safety: if move speed is 0 or negative, return zero velocity
            if (moveSpeed <= 0f) return Vector2.zero;

            // STRATEGY: Prioritize separation when colliding
            // - If separation strong (neighbors close), reduce seek influence
            // - If no collision, seek dominates

            float separationStrength = separation.magnitude / moveSpeed; // 0-1 range
            separationStrength = Mathf.Clamp01(separationStrength);

            // When separationStrength high (neighbors close), seek reduced drastically
            // Use exponential falloff: seek * (1 - strength²)
            Vector2 adjustedSeek = seek * (1f - separationStrength * separationStrength);

            Vector2 combined = adjustedSeek + separation;

            // Limit max speed
            if (combined.magnitude > moveSpeed)
                combined = combined.normalized * moveSpeed;

            return combined;
        }

        /// <summary>
        /// Update sprite facing based on target position.
        /// Returns true if should face left, false if should face right.
        /// </summary>
        public static bool ShouldFaceLeft(Vector2 enemyPos, Vector2 targetPos)
        {
            const float epsilon = 0.01f;
            return enemyPos.x > targetPos.x + epsilon;
        }
    }
}
