using UnityEngine;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Finds enemy that has most other enemies nearby (cluster center).
    /// Good for AOE abilities.
    /// </summary>
    public class ClusterTargeter : ITargeter
    {
        private readonly float _range;
        private readonly float _clusterRadius;
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public ClusterTargeter(float range, float clusterRadius = 3f)
        {
            _range = range;
            _clusterRadius = clusterRadius;
        }

        public Transform FindTarget(IBehaviorContext context)
        {
            var hits = Physics2D.OverlapCircleAll(context.PetPosition, _range, EnemyLayerMask);
            if (hits.Length == 0) return null;

            // Build list of valid enemies
            var enemies = new List<Transform>();
            foreach (var hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                if (!hit.TryGetComponent<Enemy.EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;
                enemies.Add(hit.transform);
            }

            if (enemies.Count == 0) return null;

            // Find enemy with most neighbors within cluster radius
            Transform bestClusterCenter = null;
            int maxNeighbors = 0;

            foreach (var candidate in enemies)
            {
                int neighborCount = 0;
                foreach (var other in enemies)
                {
                    if (candidate == other) continue;
                    float distance = Vector2.Distance(candidate.position, other.position);
                    if (distance <= _clusterRadius) neighborCount++;
                }

                if (neighborCount > maxNeighbors)
                {
                    maxNeighbors = neighborCount;
                    bestClusterCenter = candidate;
                }
            }

            return bestClusterCenter;
        }
    }
}
