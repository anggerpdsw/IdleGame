using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Finds nearest enemy to pet within range.
    /// </summary>
    public class NearestTargeter : ITargeter
    {
        private readonly float _range;
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public NearestTargeter(float range)
        {
            _range = range;
        }

        public Transform FindTarget(IBehaviorContext context)
        {
            var hits = Physics2D.OverlapCircleAll(context.PetPosition, _range, EnemyLayerMask);
            if (hits.Length == 0) return null;

            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                if (!hit.TryGetComponent<Enemy.EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;

                float distance = Vector2.Distance(context.PetPosition, hit.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }
    }
}
