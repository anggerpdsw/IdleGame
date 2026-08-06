using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;
using UnityEngine;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Modifiers.Effects
{
    // ============ HEALING EFFECTS ============

    /// <summary>
    /// Heals player every second by a flat amount.
    /// </summary>
    public sealed class HealEverySecondEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.HealEverySecond;
        public override string Description => $"Heals {_config.Value:F0} HP every second.";

        private float _timer;

        public override void OnActivate(EquipmentContext context)
        {
            base.OnActivate(context);
            _timer = 0f;
        }

        public override bool OnUpdate(EquipmentContext context, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= 1f)
            {
                _timer = 0f;
                float healAmount = GetCurrentValue(context);
                if (healAmount > 0f && context.Player != null)
                {
                    context.Player.Heal(healAmount);
                }
            }
            return true;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Heals player when health drops below threshold.
    /// </summary>
    public sealed class AutoHealEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.AutoHeal;
        public override string Description => $"Auto heals {_config.Value:F0} HP when below 30% health. Cooldown: {_config.Cooldown}s.";

        public override bool OnUpdate(EquipmentContext context, float deltaTime)
        {
            if (context.Player == null) return true;

            float maxHealth = context.GetStat(MainStat.HP);
            float currentHealth = context.Player.CurrentHealth;
            float threshold = maxHealth * 0.3f;

            if (currentHealth <= threshold && CanTrigger(context, EffectTriggerType.OnHealthChanged))
            {
                float healAmount = GetCurrentValue(context);
                context.Player.Heal(healAmount);
                SetLastTriggerTime(context.CurrentTime);
            }
            return true;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Heals on critical hit.
    /// </summary>
    public sealed class CriticalHealEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.CriticalHeal;
        public override string Description => $"Heals {_config.Value:F0}% of damage dealt on critical hit.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnCriticalHit) return false;
            if (!CanTrigger(context, trigger)) return false;

            float damage = data?.DamageData.GetFinalDamage() ?? 0f;
            float healPercent = GetCurrentValue(context) * 0.01f;
            float healAmount = damage * healPercent;

            if (healAmount > 0f && context.Player != null)
            {
                context.Player.Heal(healAmount);
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ DAMAGE OVER TIME EFFECTS ============

    /// <summary>
    /// Applies burn to enemies on hit.
    /// </summary>
    public sealed class BurnEnemyEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.BurnEnemy;
        public override string Description => $"{_config.Chance:F0}% chance to burn enemy on hit, dealing {_config.Value:F0}% damage per second for 5s.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnHit && trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyHit;
            if (enemy != null)
            {
                var controller = enemy.GetComponent<EnemyStatusEffectController>();
                if (controller != null)
                {
                    float dps = GetCurrentValue(context) * 0.01f * enemy.CurrentHealth; // Percent of current health per second
                    controller.AddEffect(new BurnStatus(dps, 5f, true));
                    SetLastTriggerTime(context.CurrentTime);
                    return true;
                }
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Applies freeze/slow to enemies on hit.
    /// </summary>
    public sealed class FreezeEnemyEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.FreezeEnemy;
        public override string Description => $"{_config.Chance:F0}% chance to freeze enemy on hit for {_config.Value:F1}s.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnHit && trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyHit;
            if (enemy != null)
            {
                var controller = enemy.GetComponent<EnemyStatusEffectController>();
                if (controller != null)
                {
                    controller.AddEffect(new FreezeStatus(GetCurrentValue(context)));
                    SetLastTriggerTime(context.CurrentTime);
                    return true;
                }
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Applies poison to enemies on hit.
    /// </summary>
    public sealed class PoisonEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.Poison;
        public override string Description => $"{_config.Chance:F0}% chance to poison enemy on hit, dealing {_config.Value:F0}% damage per second for 10s.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnHit && trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyHit;
            if (enemy != null)
            {
                var controller = enemy.GetComponent<EnemyStatusEffectController>();
                if (controller != null)
                {
                    float dps = GetCurrentValue(context) * 0.01f * enemy.CurrentHealth;
                    controller.AddEffect(new PoisonStatus(dps, 10f));
                    SetLastTriggerTime(context.CurrentTime);
                    return true;
                }
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ TRIGGERED EFFECTS ============

    /// <summary>
    /// Creates explosion on enemy kill.
    /// </summary>
    public sealed class ExplosionOnKillEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.ExplosionOnKill;
        public override string Description => $"On kill, creates explosion dealing {_config.Value:F0}% damage in {_config.Cooldown:F1}m radius.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnKill && trigger != EffectTriggerType.OnEnemyDeath) return false;
            if (!CanTrigger(context, trigger)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyKilled;
            if (enemy != null)
            {
                Vector3 position = enemy.transform.position;
                float damagePercent = GetCurrentValue(context) * 0.01f;
                float radius = _config.Cooldown; // Using cooldown field for radius

                CreateExplosion(context, position, damagePercent, radius);
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        private void CreateExplosion(EquipmentContext context, Vector3 position, float damagePercent, float radius)
        {
            float playerDamage = context.GetStat(MainStat.Attack);
            float explosionDamage = playerDamage * damagePercent;

            // Find enemies in radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var targetEnemy))
                {
                    DamageData damageData = new(explosionDamage, DamageType.Normal, CriticalType.None, "ExplosionOnKill")
                    {
                        Element = Element.Fire
                    };
                    targetEnemy.TakeDamage(damageData);
                }
            }

            // Visual effect
            if (_config.Value > 0)
            {
                // TODO: Spawn explosion VFX
            }
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Chain lightning on hit.
    /// </summary>
    public sealed class ChainLightningEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.ChainLightning;
        public override string Description => $"{_config.Chance:F0}% chance to chain lightning to {_config.Value:F0} nearby enemies dealing 50% damage each.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnHit && trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var sourceEnemy = data?.Enemy ?? context.LastEnemyHit;
            if (sourceEnemy != null)
            {
                int chainCount = Mathf.RoundToInt(GetCurrentValue(context));
                float damagePercent = 0.5f; // 50% damage per chain

                ChainLightning(context, sourceEnemy.transform.position, chainCount, damagePercent, new HashSet<EnemyAi> { sourceEnemy });
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        private void ChainLightning(EquipmentContext context, Vector3 startPos, int remainingChains, float damagePercent, HashSet<EnemyAi> hitEnemies)
        {
            if (remainingChains <= 0) return;

            float playerDamage = context.GetStat(MainStat.Attack);
            float chainDamage = playerDamage * damagePercent;

            // Find nearest enemy not yet hit
            Collider2D[] hits = Physics2D.OverlapCircleAll(startPos, 5f, LayerMask.GetMask("Enemy"));
            EnemyAi nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var enemy) && !hitEnemies.Contains(enemy))
                {
                    float dist = Vector3.Distance(startPos, enemy.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = enemy;
                    }
                }
            }

            if (nearest != null)
            {
                hitEnemies.Add(nearest);
                DamageData damageData = new(chainDamage, DamageType.Normal, CriticalType.None, "ChainLightning")
                {
                    Element = Element.Lightning
                };
                nearest.TakeDamage(damageData);

                // Continue chain
                ChainLightning(context, nearest.transform.position, remainingChains - 1, damagePercent * 0.8f, hitEnemies);
            }
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Multi-shot effect - fires additional projectiles.
    /// </summary>
    public sealed class MultiShotEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.MultiShot;
        public override string Description => $"Fires {_config.Value:F0} additional projectiles per attack.";

        public override void OnActivate(EquipmentContext context)
        {
            base.OnActivate(context);
            // This effect modifies player's multi-shoot stat
            // The actual implementation is in Player attack logic reading this effect
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ SUMMONING EFFECTS ============

    /// <summary>
    /// Summons skeleton minion on kill.
    /// </summary>
    public sealed class SummonSkeletonEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.SummonSkeleton;
        public override string Description => $"{_config.Chance:F0}% chance to summon a skeleton on kill. Max {_config.Value:F0} skeletons.";

        private int _activeSkeletons;

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnKill && trigger != EffectTriggerType.OnEnemyDeath) return false;
            if (!CanTrigger(context, trigger)) return false;
            if (_activeSkeletons >= GetCurrentValue(context)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyKilled;
            if (enemy != null)
            {
                SummonSkeleton(context, enemy.transform.position);
                _activeSkeletons++;
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        private void SummonSkeleton(EquipmentContext context, Vector3 position)
        {
            // TODO: Implement skeleton summoning via UltimateManager or dedicated system
            // For now, spawn a simple minion
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }

        public override void OnDeactivate(EquipmentContext context)
        {
            base.OnDeactivate(context);
            _activeSkeletons = 0;
        }

        public override void Reset(EquipmentContext context)
        {
            base.Reset(context);
            _activeSkeletons = 0;
        }
    }

    // ============ DEFENSIVE EFFECTS ============

    /// <summary>
    /// Reflects damage back to attacker.
    /// </summary>
    public sealed class ReflectDamageEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.ReflectDamage;
        public override string Description => $"Reflects {_config.Value:F0}% of damage taken back to attacker.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnDamageTaken) return false;
            if (!CanTrigger(context, trigger)) return false;

            float damageTaken = data?.DamageData.GetFinalDamage() ?? 0f;
            float reflectPercent = GetCurrentValue(context) * 0.01f;
            float reflectedDamage = damageTaken * reflectPercent;

            // Find attacker (would need reference in TriggerData)
            var attacker = data?.CustomData?.GetValueOrDefault("Attacker") as EnemyAi;
            if (attacker != null && reflectedDamage > 0f)
            {
                DamageData reflectData = new(reflectedDamage, DamageType.TrueDamage, CriticalType.None, "ReflectDamage");
                attacker.TakeDamage(reflectData);
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Grants shield periodically.
    /// </summary>
    public sealed class ShieldEvery10SecondsEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.ShieldEvery10Seconds;
        public override string Description => $"Grants shield equal to {_config.Value:F0}% of max HP every 10 seconds.";

        private float _timer;

        public override void OnActivate(EquipmentContext context)
        {
            base.OnActivate(context);
            _timer = 0f;
        }

        public override bool OnUpdate(EquipmentContext context, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= 10f)
            {
                _timer = 0f;
                float maxHealth = context.GetStat(MainStat.HP);
                float shieldAmount = maxHealth * (GetCurrentValue(context) * 0.01f);

                if (context.Player != null && shieldAmount > 0f)
                {
                    // Grant shield through player's shield system
                    // This would need integration with Player's shield logic
                }
            }
            return true;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ MOBILITY EFFECTS ============

    /// <summary>
    /// Dash attack - teleports to enemy on attack.
    /// </summary>
    public sealed class DashAttackEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.DashAttack;
        public override string Description => $"{_config.Chance:F0}% chance to dash to target on attack, dealing {_config.Value:F0}% bonus damage.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var target = data?.Enemy ?? context.LastEnemyHit;
            if (target != null && context.Player != null)
            {
                // Teleport player to enemy (behind them)
                Vector3 dashPos = target.transform.position - (target.transform.position - context.Player.transform.position).normalized * 1f;
                context.Player.transform.position = dashPos;

                // Bonus damage on next hit
                float bonusPercent = GetCurrentValue(context) * 0.01f;
                // This would need to be applied to next attack
                SetLastTriggerTime(context.CurrentTime);
                return true;
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ ECONOMY EFFECTS ============

    /// <summary>
    /// Extra gold on kill.
    /// </summary>
    public sealed class ExtraCoinEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.ExtraCoin;
        public override string Description => $"Gain {_config.Value:F0}% extra gold from enemies.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnGoldGained && trigger != EffectTriggerType.OnKill) return false;
            if (!CanTrigger(context, trigger)) return false;

            // This effect modifies gold gain multiplier
            // Actual implementation in EconomyManager when adding gold
            return false; // Passive effect, handled elsewhere
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    /// <summary>
    /// Damage scales with gold.
    /// </summary>
    public sealed class DamagePerGoldEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.DamagePerGold;
        public override string Description => $"Increases damage by {_config.Value:F0}% per 1000 gold held.";

        public override float GetCurrentValue(EquipmentContext context)
        {
            if (EconomyManager.Instance == null) return 0f;
            long gold = EconomyManager.Instance.GetCurrency(CurrencyType.Gold);
            float goldThousands = gold / 1000f;
            return _config?.Value * goldThousands * 0.01f ?? 0f;
        }
    }

    // ============ ULTIMATE EFFECTS ============

    /// <summary>
    /// Increases ultimate damage.
    /// </summary>
    public sealed class UltimateDamageEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.UltimateDamage;
        public override string Description => $"Increases ultimate damage by {_config.Value:F0}%.";

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }

    // ============ UNIQUE EFFECTS ============

    /// <summary>
    /// Chance to instantly kill non-boss enemies.
    /// </summary>
    public sealed class InstantKillChanceEffect : BaseEquipmentEffect
    {
        public override SpecialEffectType EffectType => SpecialEffectType.InstantKillChance;
        public override string Description => $"{_config.Value:F2}% chance to instantly kill non-boss enemies on hit.";

        public override bool OnTrigger(EquipmentContext context, EffectTriggerType trigger, TriggerData data)
        {
            if (trigger != EffectTriggerType.OnHit && trigger != EffectTriggerType.OnAttack) return false;
            if (!CanTrigger(context, trigger)) return false;

            var enemy = data?.Enemy ?? context.LastEnemyHit;
            if (enemy != null && enemy.Role != Role.BOSS)
            {
                float chance = GetCurrentValue(context) * 0.01f;
                if (UnityEngine.Random.Range(0f, 1f) < chance)
                {
                    enemy.TakeDamage(new DamageData(enemy.CurrentHealth * 10f, DamageType.TrueDamage, CriticalType.None, "InstantKill"));
                    SetLastTriggerTime(context.CurrentTime);
                    return true;
                }
            }
            return false;
        }

        public override float GetCurrentValue(EquipmentContext context)
        {
            return _config?.Value ?? 0f;
        }
    }
}