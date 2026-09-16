using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Finds enemy with highest absolute HP within range.
    /// </summary>
    public class HighestHPTargeter : ITargeter
    {
        private readonly float _range;
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public HighestHPTargeter(float range)
        {
            _range = range;
        }

        public Transform FindTarget(IBehaviorContext context)
        {
            var hits = Physics2D.OverlapCircleAll(context.PetPosition, _range, EnemyLayerMask);
            if (hits.Length == 0) return null;

            Transform highestHP = null;
            float maxHP = float.MinValue;

            foreach (var hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                if (!hit.TryGetComponent<Enemy.EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;

                if (enemy.CurrentHealth > maxHP)
                {
                    maxHP = enemy.CurrentHealth;
                    highestHP = hit.transform;
                }
            }

            return highestHP;
        }
    }
}
