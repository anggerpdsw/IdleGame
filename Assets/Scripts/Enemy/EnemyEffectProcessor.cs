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
                    ApplySlowToPlayer(action, sourceEnemy, player);
                    break;
                case StatusEffectType.Stun:
                    ApplyStunToPlayer(action, sourceEnemy, player);
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
                    ApplySlowToPlayer(action, sourceEnemy, player);
                    break;
                case StatusEffectType.Stun:
                    ApplyStunToPlayer(action, sourceEnemy, player);
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
        private static void ApplySlowToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player)
        {
            if (action.value <= 0f) return;
            float percent = Mathf.Clamp01(action.value * 0.01f); // Convert 50 to 0.5
            float duration = action.duration > 0f ? action.duration : 1f;
            // Use a unique source ID based on enemy instance and effect type
            string sourceId =
                $"EnemyEffect_{sourceEnemy?.EnemyData?.id}_" +
                $"{sourceEnemy.GetInstanceID()}_{action.effect}";
            // Apply through PlayerSlowManager (will create if needed)
            PlayerStatusEffectManager.Instance.ApplyEffect(sourceId, PlayerStatusEffectManager.PlayerEffectType.Slow, percent, duration);
        }

        /// <summary>
        /// Applies stun effect to player using source-based manager.
        /// </summary>
        private static void ApplyStunToPlayer(EnemyEffectAction action, EnemyAi sourceEnemy, Player.Player player)
        {
            if (action.duration <= 0f) return;
            if (sourceEnemy == null) return;

            // Generate unique source ID based on enemy definition + instance + effect type
            string sourceId =
                $"EnemyEffect_{sourceEnemy.EnemyData.id}_" +
                $"{sourceEnemy.GetInstanceID()}_{action.effect}";

            // Apply through PlayerStunManager (source-based tracking, like slow)
            PlayerStatusEffectManager.Instance.ApplyEffect(sourceId, PlayerStatusEffectManager.PlayerEffectType.Stun, 0f, action.duration);
        }

        /// <summary>
        /// Effect trigger types for logging/debugging.
        /// </summary>
        private enum EffectTriggerType { OnHit, OnTakeDamage, Aura }
    }
}