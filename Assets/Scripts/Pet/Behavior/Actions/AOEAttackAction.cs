using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// AOE attack around target or pet.
    /// Deals damage to all enemies within radius.
    /// Integrates with existing damage pipeline.
    /// </summary>
    public class AOEAttackAction : IAction
    {
        private readonly float _radius;
        private readonly bool _centerOnTarget; // true = around target, false = around pet
        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public AOEAttackAction(float radius, bool centerOnTarget = true)
        {
            _radius = radius;
            _centerOnTarget = centerOnTarget;
        }

        public bool Execute(IBehaviorContext context, ActionModifierData modifiers)
        {
            Vector3 center = _centerOnTarget && context.HasValidTarget
                ? context.CurrentTarget.position
                : context.PetPosition;

            var hits = Physics2D.OverlapCircleAll(center, _radius, EnemyLayerMask);
            if (hits.Length == 0) return false;

            float baseDamage = context.Pet.GetAttack();
            float finalDamage = baseDamage * modifiers.DamageMultiplier;
            bool anyHit = false;

            foreach (var hit in hits)
            {
                if (!hit.gameObject.activeInHierarchy) continue;
                if (!hit.TryGetComponent<Enemy.EnemyAi>(out var enemy)) continue;
                if (enemy.CurrentHealth <= 0) continue;

                var damageData = new DamageData(
                    damage: finalDamage,
                    type: DamageType.Normal,
                    crit: CriticalType.None,
                    source: "PetAOE"
                )
                {
                    Element = Element.None
                };

                enemy.TakeDamage(damageData);
                anyHit = true;
            }

            return anyHit;
        }
    }
}
