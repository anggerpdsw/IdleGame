using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Finds enemy with lowest absolute HP within range.
    /// </summary>
    public class LowestHPTargeter : ITargeter
    {
        private readonly float _range;
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public LowestHPTargeter(float range)
        {
            _range = range;
        }

        public Transform FindTarget(IBehaviorContext context)
        {
            var hits = Physics2D.OverlapCircleAll(context.PetPosition, _range, EnemyLayerMask);
            if (hits.Length == 0) return null;

            Transform lowestHP = null;
            float minHP = float.MaxValue;

            foreach (var hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                if (!hit.TryGetComponent<Enemy.EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;

                if (enemy.CurrentHealth < minHP)
                {
                    minHP = enemy.CurrentHealth;
                    lowestHP = hit.transform;
                }
            }

            return lowestHP;
        }
    }
}
