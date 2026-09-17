using UnityEngine;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Pet.Skills
{
    /// <summary>
    /// Voidling active skill - AOE void explosion around player.
    /// Deals damage and applies Slow to all enemies in radius.
    /// Cast condition: enemy count >= threshold OR elite nearby OR emergency mode.
    /// </summary>
    public class VoidPulse : PetSkill
    {
        private const float SKILL_RADIUS = 3.5f;
        private const float SLOW_PERCENT = 0.35f; // 35% slow
        private const float SLOW_DURATION = 2.5f;
        private const int MIN_ENEMY_COUNT = 3; // Cast if 3+ enemies in range

        private static readonly int EnemyLayerMask = LayerMask.GetMask("Enemy");

        public VoidPulse()
        {
            SkillId = "void_pulse";
            BaseCooldown = 12f;
            BaseDamageMultiplier = 1.5f; // 150% pet attack
        }

        protected override bool CheckCustomConditions()
        {
            var player = Player.Player.Instance;
            if (player == null) return false;

            // In emergency mode, always cast when available
            if (Pet.IsEmergencyMode) return true;

            // Check enemy count and elite presence
            Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, SKILL_RADIUS, EnemyLayerMask);

            int enemyCount = 0;
            bool hasElite = false;

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var enemy) && enemy.CurrentHealth > 0)
                {
                    enemyCount++;
                    if (enemy.Role == Role.BOSS)
                    {
                        hasElite = true;
                    }
                }
            }

            // Cast if: enough enemies OR elite/boss present
            return enemyCount >= MIN_ENEMY_COUNT || hasElite;
        }

        protected override void OnExecute()
        {
            var player = Player.Player.Instance;
            if (player == null) return;

            Vector3 centerPosition = player.transform.position;

            // Find all enemies in radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(centerPosition, SKILL_RADIUS, EnemyLayerMask);

            float damage = CalculateDamage();

            List<EnemyAi> hitEnemies = new();

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var enemy) && enemy.CurrentHealth > 0)
                {
                    hitEnemies.Add(enemy);

                    // Apply damage
                    var damageData = new DamageData(
                        damage: damage,
                        type: DamageType.Normal,
                        crit: CriticalType.None,
                        source: "Pet_VoidPulse"
                    )
                    {
                        Element = Element.Wind
                    };

                    enemy.TakeDamage(damageData);

                    // Apply Slow status effect
                    var slowEffect = new SlowStatus(SLOW_PERCENT, SLOW_DURATION);

                    if (enemy.TryGetComponent<EnemyStatusEffectController>(out var statusController))
                    {
                        statusController.AddEffect(slowEffect);
                    }
                }
            }

            // TODO: VFX - spawn void pulse visual effect at center position
            // TODO: SFX - play void pulse sound

            Debug.Log($"[VoidPulse] Hit {hitEnemies.Count} enemies with void explosion");
        }
    }
}
