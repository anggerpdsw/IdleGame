using UnityEngine;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Basic projectile attack action.
    /// Spawns projectile from pool, applies modifiers to damage.
    /// Integrates with existing ProjectilePool system.
    /// </summary>
    public class BasicAttackAction : IAction
    {
        public bool Execute(IBehaviorContext context, ActionModifierData modifiers)
        {
            if (!context.HasValidTarget) return false;

            var projectilePool = ProjectilePool.Instance;
            if (projectilePool == null) return false;

            var projectile = projectilePool.Get();
            if (projectile == null) return false;

            // Position at pet location
            projectile.transform.position = context.PetPosition;

            // Calculate final damage with modifiers
            float baseDamage = context.Pet.GetAttack();
            float finalDamage = baseDamage * modifiers.DamageMultiplier;

            // Initialize projectile (uses existing damage multiplier pattern)
            // Note: Projectile.Initialize doesn't natively support Pet owner
            // Uses damage multiplier as workaround until Projectile is extended
            if (context.Player != null)
            {
                projectile.Initialize(context.CurrentTarget, context.Player, modifiers.DamageMultiplier);
            }

            return true;
        }
    }
}
