using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class OverkillConversionCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.OverkillConversion;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private const string RangeModifierId = "Card:OverkillConversion_Range";

        private float _conversionRatioPercent;
        private float _maximumRangeBonusPercent;
        private float _durationSeconds;
        private int _maximumStacks;
        private float _detectionRadius;

        private int _currentStacks;
        private float _stackTimer;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _conversionRatioPercent = GetParameter("ConversionRatioPercent", 3f) / 100f;
            _maximumRangeBonusPercent = GetParameter("MaximumRangeBonusPercent", 30f);
            _durationSeconds = GetParameter("DurationSeconds", 4f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 5f));
            _detectionRadius = GetParameter("DetectionRadius", 5f);

            // Grant initial range bonus if enemies are nearby on equip
            var player = PlayerClass.Instance;
            if (player != null)
            {
                var nearby = Physics2D.OverlapCircleAll(
                    player.transform.position,
                    _detectionRadius,
                    LayerMask.GetMask("Enemy"));
                if (nearby.Length > 0 && _currentStacks < _maximumStacks)
                {
                    _currentStacks++;
                    _stackTimer = _durationSeconds;
                    ApplyModifier();
                }
            }
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            // This is handled through the projectile's overkill damage tracking
            // The actual conversion happens when overkill damage is dealt
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            // Check if this kill had overkill damage
            if (enemy.LastOverkillDamage > 0f)
            {
                AddStackFromOverkill(enemy.LastOverkillDamage);
            }
        }

        private void AddStackFromOverkill(float overkillDamage)
        {
            if (_currentStacks >= _maximumStacks) return;

            float rangeBonus = overkillDamage * _conversionRatioPercent;
            // Convert to percentage of base attack range
            float baseRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            float rangeBonusPercent = (rangeBonus / Mathf.Max(0.1f, baseRange)) * 100f;
            rangeBonusPercent = Mathf.Min(rangeBonusPercent, _maximumRangeBonusPercent / _maximumStacks);

            _currentStacks++;
            _stackTimer = _durationSeconds;
            ApplyModifier();
        }

        public override void Update(float deltaTime)
        {
            if (_currentStacks > 0)
            {
                _stackTimer -= deltaTime;
                if (_stackTimer <= 0f)
                {
                    _currentStacks = Mathf.Max(0, _currentStacks - 1);
                    ApplyModifier();
                }
            }
        }

        private void ApplyModifier()
        {
            ModifierManager.Instance.RemoveModifier(RangeModifierId);

            if (_currentStacks > 0)
            {
                float rangeBonusPercent = _currentStacks * (_maximumRangeBonusPercent / _maximumStacks);
                var modifier = new StatModifier
                {
                    Id = RangeModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackRange,
                    Mode = ModifierMode.Percent,
                    Value = rangeBonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(RangeModifierId);
            _currentStacks = 0;
            _stackTimer = 0f;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            ApplyModifier();
        }
    }
}