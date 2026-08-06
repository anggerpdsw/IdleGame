using System;
using UnityEngine;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Enemy.StatusEffects
{
    /// <summary>
    /// Burn - deals damage over time based on a percentage of enemy's max health or flat damage per second.
    /// Stacks additively (each application adds damage per tick).
    /// </summary>
    [Serializable]
    public sealed class BurnStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Burn;
        public override int MaxStacks => 10;
        public override StackPolicy StackPolicy => StackPolicy.Additive;

        private readonly float _damagePerSecond;
        private readonly bool _isPercentBased;

        public BurnStatus(float damagePerSecond, float duration, bool isPercentBased = false)
            : base(duration)
        {
            _damagePerSecond = damagePerSecond;
            _isPercentBased = isPercentBased;
        }

        public override float GetCurrentValue()
        {
            if (_isPercentBased && _enemy != null)
            {
                return _enemy.CurrentHealth * _damagePerSecond;
            }
            return _damagePerSecond * StackCount;
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);

            float damage = GetCurrentValue() * deltaTime;
            if (damage > 0f)
            {
                var damageData = new DamageData(damage, DamageType.Burn, CriticalType.None, "Burn")
                {
                    Element = Element.Fire
                };
                enemy.TakeDamage(damageData);
            }
        }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);
            // Visual feedback - could add burn particle effect here
        }

        public override void OnExpire(EnemyAi enemy)
        {
            // Remove burn visual effect
            base.OnExpire(enemy);
        }

        public override IStatusEffect Clone()
        {
            var clone = new BurnStatus(_damagePerSecond, Duration, _isPercentBased);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Poison - deals damage over time and can spread to nearby enemies.
    /// Stacks by taking maximum damage value.
    /// </summary>
    [Serializable]
    public sealed class PoisonStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Poison;
        public override int MaxStacks => 5;
        public override StackPolicy StackPolicy => StackPolicy.MaximumValue;

        private readonly float _damagePerSecond;
        private readonly float _spreadRadius;
        private readonly float _spreadChance;
        private float _spreadTimer;

        public PoisonStatus(float damagePerSecond, float duration, float spreadRadius = 3f, float spreadChance = 0.1f)
            : base(duration)
        {
            _damagePerSecond = damagePerSecond;
            _spreadRadius = spreadRadius;
            _spreadChance = spreadChance;
        }

        public override float GetCurrentValue() => _damagePerSecond * StackCount;

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);

            // Deal poison damage
            float damage = GetCurrentValue() * deltaTime;
            if (damage > 0f)
            {
                var damageData = new DamageData(damage, DamageType.Poison, CriticalType.None, "Poison")
                {
                    Element = Element.None
                };
                enemy.TakeDamage(damageData);
            }

            // Attempt to spread
            _spreadTimer += deltaTime;
            if (_spreadTimer >= 1f)
            {
                _spreadTimer = 0f;
                TrySpreadPoison(enemy);
            }
        }

        private void TrySpreadPoison(EnemyAi source)
        {
            if (UnityEngine.Random.Range(0f, 1f) > _spreadChance) return;

            Collider2D[] hits = Physics2D.OverlapCircleAll(source.transform.position, _spreadRadius, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAi>(out var target) && target != source)
                {
                    // Check if target already has poison
                    var controller = target.GetComponent<EnemyStatusEffectController>();
                    if (controller != null && !controller.HasEffect(StatusEffectType.Poison))
                    {
                        controller.AddEffect(new PoisonStatus(_damagePerSecond, Duration, _spreadRadius, _spreadChance * 0.5f));
                        break; // Only spread to one enemy per tick
                    }
                }
            }
        }

        public override IStatusEffect Clone()
        {
            var clone = new PoisonStatus(_damagePerSecond, Duration, _spreadRadius, _spreadChance);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Freeze - completely immobilizes the enemy.
    /// Does not stack; refreshes duration.
    /// </summary>
    [Serializable]
    public sealed class FreezeStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Freeze;
        public override int MaxStacks => 1;
        public override StackPolicy StackPolicy => StackPolicy.RefreshDuration;

        private float _originalMoveSpeed;
        private float _originalAttackSpeed;

        public FreezeStatus(float duration) : base(duration) { }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);

            _originalMoveSpeed = enemy.MoveSpeed;
            _originalAttackSpeed = enemy.AttackSpeed;

            enemy.SetMoveSpeed(0f);
            enemy.SetAttackSpeed(0f);

            // Visual feedback - add freeze effect
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);
            // Ensure enemy stays frozen
            if (enemy.MoveSpeed > 0f) enemy.SetMoveSpeed(0f);
            if (enemy.AttackSpeed > 0f) enemy.SetAttackSpeed(0f);
        }

        public override void OnExpire(EnemyAi enemy)
        {
            enemy.SetMoveSpeed(_originalMoveSpeed);
            enemy.SetAttackSpeed(_originalAttackSpeed);
            base.OnExpire(enemy);
        }

        public override IStatusEffect Clone()
        {
            var clone = new FreezeStatus(Duration);
            clone.StackCount = StackCount;
            return clone;
        }
    }

    /// <summary>
    /// Bleed - deals physical damage over time based on missing health.
    /// Stacks additively.
    /// </summary>
    [Serializable]
    public sealed class BleedStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Bleed;
        public override int MaxStacks => 15;
        public override StackPolicy StackPolicy => StackPolicy.Additive;

        private readonly float _baseDamagePerSecond;
        private readonly float _missingHealthMultiplier;

        public BleedStatus(float baseDamagePerSecond, float duration, float missingHealthMultiplier = 0.5f)
            : base(duration)
        {
            _baseDamagePerSecond = baseDamagePerSecond;
            _missingHealthMultiplier = missingHealthMultiplier;
        }

        public override float GetCurrentValue()
        {
            if (_enemy == null) return _baseDamagePerSecond * StackCount;

            float missingHealthPercent = 1f - (_enemy.CurrentHealth / Mathf.Max(1f, _enemy.MaxHealth));
            return (_baseDamagePerSecond + _baseDamagePerSecond * missingHealthPercent * _missingHealthMultiplier) * StackCount;
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);

            float damage = GetCurrentValue() * deltaTime;
            if (damage > 0f)
            {
                var damageData = new DamageData(damage, DamageType.Normal, CriticalType.None, "Bleed")
                {
                    Element = Element.None
                };
                enemy.TakeDamage(damageData);
            }
        }

        public override IStatusEffect Clone()
        {
            var clone = new BleedStatus(_baseDamagePerSecond, Duration, _missingHealthMultiplier);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Shock - deals lightning damage and can chain to nearby enemies.
    /// Stacks by maximum value.
    /// </summary>
    [Serializable]
    public sealed class ShockStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Shock;
        public override int MaxStacks => 3;
        public override StackPolicy StackPolicy => StackPolicy.MaximumValue;

        private readonly float _damagePerSecond;
        private readonly float _chainRadius;
        private readonly int _maxChains;
        private float _chainTimer;

        public ShockStatus(float damagePerSecond, float duration, float chainRadius = 4f, int maxChains = 3)
            : base(duration)
        {
            _damagePerSecond = damagePerSecond;
            _chainRadius = chainRadius;
            _maxChains = maxChains;
        }

        public override float GetCurrentValue() => _damagePerSecond * StackCount;

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);

            // Deal shock damage
            float damage = GetCurrentValue() * deltaTime;
            if (damage > 0f)
            {
                var damageData = new DamageData(damage, DamageType.Normal, CriticalType.None, "Shock")
                {
                    Element = Element.Lightning
                };
                enemy.TakeDamage(damageData);
            }

            // Chain to nearby enemies periodically
            _chainTimer += deltaTime;
            if (_chainTimer >= 0.5f)
            {
                _chainTimer = 0f;
                TryChainShock(enemy);
            }
        }

        private void TryChainShock(EnemyAi source)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(source.transform.position, _chainRadius, LayerMask.GetMask("Enemy"));
            int chained = 0;

            foreach (var hit in hits)
            {
                if (chained >= _maxChains) break;
                if (hit.TryGetComponent<EnemyAi>(out var target) && target != source)
                {
                    var damageData = new DamageData(_damagePerSecond * 0.5f, DamageType.Normal, CriticalType.None, "ShockChain")
                    {
                        Element = Element.Lightning
                    };
                    target.TakeDamage(damageData);
                    chained++;
                }
            }
        }

        public override IStatusEffect Clone()
        {
            var clone = new ShockStatus(_damagePerSecond, Duration, _chainRadius, _maxChains);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Slow - reduces enemy movement and attack speed.
    /// Stacks multiplicatively (compounding slows).
    /// </summary>
    [Serializable]
    public sealed class SlowStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Slow;
        public override int MaxStacks => 10;
        public override StackPolicy StackPolicy => StackPolicy.Multiplicative;

        private readonly float _slowPercent; // 0.3 = 30% slow
        private float _originalMoveSpeed;
        private float _originalAttackSpeed;

        public SlowStatus(float slowPercent, float duration)
            : base(duration)
        {
            _slowPercent = Mathf.Clamp01(slowPercent);
        }

        public override float GetCurrentValue()
        {
            // Multiplicative stacking: 0.3 slow + 0.3 slow = 1 - (0.7 * 0.7) = 0.51 effective slow
            float effectiveSlow = 1f - Mathf.Pow(1f - _slowPercent, StackCount);
            return effectiveSlow;
        }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);

            _originalMoveSpeed = enemy.MoveSpeed;
            _originalAttackSpeed = enemy.AttackSpeed;

            ApplySlow(enemy);
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);
            ApplySlow(enemy); // Reapply in case speed changed
        }

        private void ApplySlow(EnemyAi enemy)
        {
            float slow = GetCurrentValue();
            enemy.SetMoveSpeed(_originalMoveSpeed * (1f - slow));
            enemy.SetAttackSpeed(_originalAttackSpeed * (1f - slow));
        }

        public override void OnExpire(EnemyAi enemy)
        {
            enemy.SetMoveSpeed(_originalMoveSpeed);
            enemy.SetAttackSpeed(_originalAttackSpeed);
            base.OnExpire(enemy);
        }

        public override void OnStackAdded(EnemyAi enemy, int newStackCount)
        {
            base.OnStackAdded(enemy, newStackCount);
            ApplySlow(enemy);
        }

        public override IStatusEffect Clone()
        {
            var clone = new SlowStatus(_slowPercent, Duration);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Curse - amplifies damage taken and reduces healing received.
    /// Stacks by maximum value.
    /// </summary>
    [Serializable]
    public sealed class CurseStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Curse;
        public override int MaxStacks => 5;
        public override StackPolicy StackPolicy => StackPolicy.MaximumValue;

        private readonly float _damageTakenIncrease; // 0.2 = 20% more damage taken
        private readonly float _healingReduction;    // 0.5 = 50% less healing

        public CurseStatus(float damageTakenIncrease, float healingReduction, float duration)
            : base(duration)
        {
            _damageTakenIncrease = damageTakenIncrease;
            _healingReduction = healingReduction;
        }

        public override float GetCurrentValue() => _damageTakenIncrease * StackCount;

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);
            // Curse is handled in TakeDamage by checking for this effect
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);
            // Visual feedback for curse
        }

        public float GetDamageTakenMultiplier() => 1f + _damageTakenIncrease * StackCount;
        public float GetHealingMultiplier() => 1f - _healingReduction * StackCount;

        public override IStatusEffect Clone()
        {
            var clone = new CurseStatus(_damageTakenIncrease, _healingReduction, Duration);
            clone.StackCount = StackCount;
            clone.MaxStacks = MaxStacks;
            return clone;
        }
    }

    /// <summary>
    /// Fear - causes enemy to flee from player.
    /// Does not stack; refreshes duration.
    /// </summary>
    [Serializable]
    public sealed class FearStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Fear;
        public override int MaxStacks => 1;
        public override StackPolicy StackPolicy => StackPolicy.RefreshDuration;

        private float _originalMoveSpeed;
        private Transform _playerTransform;

        public FearStatus(float duration) : base(duration) { }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);

            _originalMoveSpeed = enemy.MoveSpeed;
            _playerTransform = enemy.PlayerTransform;
            // Fear makes enemy run away at increased speed
            enemy.SetMoveSpeed(_originalMoveSpeed * 1.5f);
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);

            // Find player if not cached
            if (_playerTransform == null)
            {
                _playerTransform = enemy.PlayerTransform;
            }

            if (_playerTransform == null) return;

            // Move away from player
            Vector2 direction = (enemy.transform.position - _playerTransform.position).normalized;
            enemy.transform.position += (Vector3)(direction * enemy.MoveSpeed * deltaTime);
        }

        public override void OnExpire(EnemyAi enemy)
        {
            enemy.SetMoveSpeed(_originalMoveSpeed);
            base.OnExpire(enemy);
        }

        public override IStatusEffect Clone()
        {
            var clone = new FearStatus(Duration);
            clone.StackCount = StackCount;
            return clone;
        }
    }

    /// <summary>
    /// Stun - completely prevents enemy action.
    /// Does not stack; refreshes duration.
    /// </summary>
    [Serializable]
    public sealed class StunStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Stun;
        public override int MaxStacks => 1;
        public override StackPolicy StackPolicy => StackPolicy.RefreshDuration;

        public StunStatus(float duration) : base(duration) { }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);
            enemy.ApplyStunt(Duration);
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);
        }

        public override IStatusEffect Clone()
        {
            var clone = new StunStatus(Duration);
            clone.StackCount = StackCount;
            return clone;
        }
    }

    /// <summary>
    /// Root - prevents movement but allows attacking.
    /// Does not stack; refreshes duration.
    /// </summary>
    [Serializable]
    public sealed class RootStatus : BaseStatusEffect
    {
        public override StatusEffectType Type => StatusEffectType.Root;
        public override int MaxStacks => 1;
        public override StackPolicy StackPolicy => StackPolicy.RefreshDuration;

        private float _originalMoveSpeed;

        public RootStatus(float duration) : base(duration) { }

        public override void OnApply(EnemyAi enemy)
        {
            base.OnApply(enemy);
            _originalMoveSpeed = enemy.MoveSpeed;
            enemy.SetMoveSpeed(0f);
        }

        public override void Tick(EnemyAi enemy, float deltaTime)
        {
            base.Tick(enemy, deltaTime);
            if (enemy.MoveSpeed > 0f) enemy.SetMoveSpeed(0f);
        }

        public override void OnExpire(EnemyAi enemy)
        {
            enemy.SetMoveSpeed(_originalMoveSpeed);
            base.OnExpire(enemy);
        }

        public override IStatusEffect Clone()
        {
            var clone = new RootStatus(Duration);
            clone.StackCount = StackCount;
            return clone;
        }
    }
}