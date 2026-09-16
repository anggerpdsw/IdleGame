using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet.Skills
{
    /// <summary>
    /// Voidling basic attack - fires void projectile at target.
    /// Reuses existing ProjectilePool and Projectile system.
    /// Damage = Pet Attack stat (no skill multiplier for basic attack).
    /// </summary>
    public class VoidBolt : PetSkill
    {
        public VoidBolt()
        {
            SkillId = "void_bolt";
            BaseCooldown = 0f; // Basic attack uses attack speed, not skill cooldown
            BaseDamageMultiplier = 1.0f; // 100% pet attack
        }

        protected override bool CheckCustomConditions()
        {
            // Requires valid target
            return Pet.Target != null && Pet.IsTargetValid();
        }

        protected override void OnExecute()
        {
            if (Pet.Target == null) return;

            var projectilePool = ProjectilePool.Instance;
            if (projectilePool == null)
            {
                Debug.LogWarning("[VoidBolt] ProjectilePool not available");
                return;
            }

            var projectile = projectilePool.Get();
            if (projectile == null) return;

            // Position at pet location
            projectile.transform.position = Pet.Position;

            // Initialize projectile with pet damage
            // Note: Projectile system currently supports Player/Tank/Enemy owners
            // For pet projectiles, we use a workaround:
            // Create a DamageData with Pet as source, then apply damage directly
            // This preserves existing Projectile architecture without modification

            float damage = Pet.GetAttack();

            // Get player reference for projectile initialization compatibility
            var player = Player.Player.Instance;
            if (player != null)
            {
                // Initialize as player projectile but with custom damage multiplier
                // Damage multiplier = (petAttack / playerAttack) to achieve pet damage
                float playerAttack = PlayerStatsManager.Instance.GetStat(Stats.SkillType.AttackDamage);
                float damageMultiplier = playerAttack > 0 ? damage / playerAttack : 1f;

                projectile.Initialize(Pet.Target, player, damageMultiplier);
            }
            else
            {
                Debug.LogWarning("[VoidBolt] Player instance not available");
            }
        }
    }
}
