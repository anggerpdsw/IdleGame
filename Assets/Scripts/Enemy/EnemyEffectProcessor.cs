using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// Processes enemy effects defined in EnemyData.
    /// Handles OnHit, OnTakeDamage, and Aura effects.
    /// </summary>
    public static class EnemyEffectProcessor
    {
        /// <summary>
        /// Processes OnHit effects when enemy successfully hits player.
        /// </summary>
        public static void ProcessOnHit(EnemyAi enemy, Player.Player player)
        {
            if (enemy?.EnemyData?.effects == null) return;
            foreach (var effect in enemy.EnemyData.effects)
            {
                if (effect?.onHit == null) continue;
                foreach (var action in effect.onHit)
                {
                    if (action == null || action.effect == StatusEffectType.None) continue;
                    ApplyEffectToPlayer(action, enemy, player, EffectTriggerType.OnHit);
                }
            }
        }

        /// <summary>
        /// Processes Vampiric LifeSteal after enemy successfully damages player.
        /// Called from Projectile.HitPlayer after actualDamageDealt is known.
        /// </summary>
        public static void ProcessVampiricLifeSteal(EnemyAi enemy, float actualDamageDealt)
        {
            if (enemy?.EnemyData?.effects == null) return;
            if (actualDamageDealt <= 0f) return;

            foreach (var effect in enemy.EnemyData.effects)
            {
                if (effect?.type != "Vampiric") continue;
                if (effect?.onHit == null) continue;

                foreach (var action in effect.onHit)
                {
                    if (action == null || action.effect != StatusEffectType.LifeSteal) continue;
                    if (action.value <= 0f) continue;

                    float lifeStealPercent = action.value * 0.01f; // 40 → 0.40
                    float healthThreshold = enemy.MaxHealth * 0.80f;

                    if (enemy.CurrentHealth <= healthThreshold)
                    {
                        // Self-heal: 40%
                        float selfHeal = actualDamageDealt * lifeStealPercent;
                        enemy.Heal(selfHeal);
                    }
                    else
                    {
                        // Aura heal: 20% distributed to nearby enemies
                        float auraHealTotal = actualDamageDealt * (lifeStealPercent * 0.5f);
                        DistributeAuraHeal(enemy, auraHealTotal, action.radius);
                    }
                }
            }
        }

        private static void DistributeAuraHeal(EnemyAi source, float healAmount, float radius)
        {
            if (healAmount <= 0f || radius <= 0f) return;

            var buffer = new Collider2D[64];
            int count = Physics2D.OverlapCircle(
                source.transform.position,
                radius,
                new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Enemy") },
                buffer);

            for (int i = 0; i < count; i++)
            {
                if (!buffer[i].TryGetComponent<EnemyAi>(out var target)) continue;
                if (target == source) continue;
                if (target.CurrentHealth >= target.MaxHealth) continue;
                if (!target.gameObject.activeInHierarchy) continue;

                target.Heal(healAmount);
            }
        }

        /// <summary>
        /// Processes OnTakeDamage effects when enemy receives damage.
        /// </summary>
        public static void ProcessOnTakeDamage(EnemyAi enemy, DamageData damageData)
        {
            if (enemy?.EnemyData?.effects == null) return;
            // Only trigger if actual damage was dealt
            if (damageData.Damage <= 0f) return;
            foreach (var effect in enemy.EnemyData.effects)
            {
                if (effect?.onTakeDamage == null) continue;
                foreach (var action in effect.onTakeDamage)
                {
                    if (action == null || action.effect == StatusEffectType.None) continue;
                    ApplyEffectToAttacker(action, enemy, damageData, EffectTriggerType.OnTakeDamage);
                }
            }
        }

        /// <summary>
        /// Applies an effect action to the player.
        /// </summary>
        private static void ApplyEffectToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player, EffectTriggerType trigger)
        {
            if (player == null) return;
            switch (action.effect)
            {
                case StatusEffectType.Slow:
                    ApplySlowToPlayer(action, sourceEnemy, player, trigger);
                    break;
                case StatusEffectType.Stun:
                    ApplyStunToPlayer(action, sourceEnemy, player, trigger);
                    break;
                case StatusEffectType.Burn:
                    ApplyBurnToPlayer(action, sourceEnemy, player, trigger);
                    break;
                case StatusEffectType.LifeSteal:
                    // LifeSteal is processed separately in ProcessVampiricLifeSteal()
                    // after actualDamageDealt is known (called from Projectile.HitPlayer)
                    break;
                default:
                    Debug.LogWarning(
                        $"[EnemyEffectProcessor] Unknown effect type: " +
                        $"{action.effect} on enemy " +
                        $"{sourceEnemy?.EnemyData?.id}");
                    break;
            }
        }

        /// <summary>
        /// Applies an effect action to the attacker (player/projectile source).
        /// </summary>
        private static void ApplyEffectToAttacker(EnemyEffectAction action, EnemyAi sourceEnemy, DamageData damageData, EffectTriggerType trigger)
        {
            // For OnTakeDamage, the attacker is the player (or projectile source)
            // The damageData.Source tells us who dealt the damage
            if (damageData.Source != ProjectileOwner.Player.ToString()) return;
            var player = Player.Player.Instance;
            if (player == null) return;
            switch (action.effect)
            {
                case StatusEffectType.Slow:
                    ApplySlowToPlayer(action, sourceEnemy, player, trigger);
                    break;
                case StatusEffectType.Stun:
                    ApplyStunToPlayer(action, sourceEnemy, player, trigger);
                    break;
                case StatusEffectType.Burn:
                    ApplyBurnToPlayer(action, sourceEnemy, player, trigger);
                    break;
                default:
                    Debug.LogWarning(
                        $"[EnemyEffectProcessor] Unknown effect type: " +
                        $"{action.effect} on enemy " +
                        $"{sourceEnemy?.EnemyData?.id}");
                    break;
            }
        }

        /// <summary>
        /// Applies slow effect to player.
        /// </summary>
        private static void ApplySlowToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player, EffectTriggerType trigger)
        {
            if (action.value <= 0f) return;
            float percent = Mathf.Clamp01(action.value * 0.01f); // Convert 50 to 0.5
            float duration = action.duration > 0f ? action.duration : 1f;
            var sourceId = BuildSourceId(sourceEnemy, action.effect, trigger);
            PlayerStatusEffectManager.Instance.ApplyEffect(
                sourceId,
                PlayerStatusEffectManager.PlayerEffectType.Slow,
                percent,
                duration);
        }

        /// <summary>
        /// Applies stun effect to player using source-based manager.
        /// </summary>
        private static void ApplyStunToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player, EffectTriggerType trigger)
        {
            if (action.duration <= 0f) return;
            if (sourceEnemy == null) return;

            var sourceId = BuildSourceId(sourceEnemy, action.effect, trigger);
            PlayerStatusEffectManager.Instance.ApplyEffect(
                sourceId,
                PlayerStatusEffectManager.PlayerEffectType.Stun,
                0f,
                action.duration);
        }

        /// <summary>
        /// Applies burn effect to player.
        /// </summary>
        private static void ApplyBurnToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player, EffectTriggerType trigger)
        {
            if (action.value <= 0f) return;
            // value stored as percent of max health (e.g., 10 = 10%)
            float percent = Mathf.Clamp01(action.value * 0.01f);
            float duration = Mathf.Max(0.1f, action.duration);

            var sourceId = BuildSourceId(sourceEnemy, action.effect, trigger);
            PlayerStatusEffectManager.Instance.ApplyEffect(
                sourceId,
                PlayerStatusEffectManager.PlayerEffectType.Burn,
                percent,
                duration);
        }

        private static EnemyAuraManager.StatusSourceId BuildSourceId(
            EnemyAi sourceEnemy, StatusEffectType effect, EffectTriggerType trigger)
        {
            var auraType = trigger switch
            {
                EffectTriggerType.OnHit => EnemyAuraManager.EffectTrigger.OnHit,
                EffectTriggerType.OnTakeDamage => EnemyAuraManager.EffectTrigger.OnTakeDamage,
                EffectTriggerType.Aura => EnemyAuraManager.EffectTrigger.Aura,
                _ => EnemyAuraManager.EffectTrigger.Aura
            };

            return new EnemyAuraManager.StatusSourceId(
                sourceEnemy?.GetInstanceID() ?? 0,
                (int)effect,
                auraType);
        }

        /// <summary>
        /// Effect trigger types for logging/debugging.
        /// </summary>
        private enum EffectTriggerType { OnHit, OnTakeDamage, Aura }
    }
}