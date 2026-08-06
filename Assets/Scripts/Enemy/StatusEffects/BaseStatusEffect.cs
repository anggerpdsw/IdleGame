using System;
using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Enemy.StatusEffects
{
    /// <summary>
    /// Base class for status effects providing common functionality.
    /// </summary>
    public abstract class BaseStatusEffect : IStatusEffect
    {
        public abstract StatusEffectType Type { get; }
        public virtual StatusEffectCategory Category => GetCategoryForType(Type);
        public float Duration { get; set; }
        public float ElapsedTime { get; set; }
        public virtual bool IsExpired => ElapsedTime >= Duration;
        public int StackCount { get; protected set; } = 1;
        public virtual int MaxStacks { get; protected set; } = 1;
        public virtual StackPolicy StackPolicy => StackPolicy.RefreshDuration;

        protected EnemyAi _enemy;

        protected BaseStatusEffect(float duration)
        {
            Duration = duration;
            ElapsedTime = 0f;
        }

        public virtual void OnApply(EnemyAi enemy)
        {
            _enemy = enemy;
            ElapsedTime = 0f;
            StackCount = 1;
        }

        public virtual void Tick(EnemyAi enemy, float deltaTime)
        {
            ElapsedTime += deltaTime;
        }

        public virtual void OnExpire(EnemyAi enemy)
        {
            _enemy = null;
        }

        public virtual void OnStackAdded(EnemyAi enemy, int newStackCount)
        {
            StackCount = newStackCount;
            ElapsedTime = 0f; // Refresh duration on new stack by default
        }

        public virtual float GetCurrentValue() => 0f;

        public virtual IStatusEffect Clone()
        {
            var clone = (BaseStatusEffect)Activator.CreateInstance(GetType(), Duration);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }

        protected static StatusEffectCategory GetCategoryForType(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Burn or StatusEffectType.Poison or StatusEffectType.Bleed or
                StatusEffectType.Shock or StatusEffectType.ManaBurn => StatusEffectCategory.DamageOverTime,

                StatusEffectType.Freeze or StatusEffectType.Stun or StatusEffectType.Root or
                StatusEffectType.Fear or StatusEffectType.Silence or StatusEffectType.Charm or
                StatusEffectType.Confusion or StatusEffectType.Knockback or StatusEffectType.Taunt =>
                    StatusEffectCategory.CrowdControl,

                StatusEffectType.Slow or StatusEffectType.MoveSpeedDown or StatusEffectType.AttackSpeedDown =>
                    StatusEffectCategory.MovementImpairment,

                StatusEffectType.Weaken or StatusEffectType.Vulnerable or StatusEffectType.ArmorBreak or
                StatusEffectType.MagicResistBreak or StatusEffectType.HealingReduction or
                StatusEffectType.DamageReduction or StatusEffectType.CriticalVulnerability or
                StatusEffectType.EvasionDown or StatusEffectType.CooldownIncrease or
                StatusEffectType.LifeStealReduction or StatusEffectType.ShieldBreak =>
                    StatusEffectCategory.StatDebuff,

                StatusEffectType.Invulnerability or StatusEffectType.ControlImmune or
                StatusEffectType.DamageOverTimeImmune or StatusEffectType.TauntImmune =>
                    StatusEffectCategory.Immunity,

                StatusEffectType.Stealth => StatusEffectCategory.Special,

                _ => StatusEffectCategory.Utility,
            };
        }

        protected float GetEnemyMaxHealth() => _enemy?.CurrentHealth ?? 0f;

        protected void DealDamageToEnemy(float damage, DamageType type = DamageType.Normal, CriticalType critical = CriticalType.None, string source = "StatusEffect")
        {
            if (_enemy == null) return;

            var damageData = new DamageData(damage, type, critical, source)
            {
                Element = Element.None
            };
            _enemy.TakeDamage(damageData);
        }
    }
}