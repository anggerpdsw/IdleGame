using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class FeastOrFamineCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.FeastOrFamine;

        private const string FeastModifierId = "Card:FeastOrFamine_Feast";
        private const string FamineModifierId = "Card:FeastOrFamine_Famine";

        private float _feastChancePercent;
        private float _attackDamageBonusPercent;
        private float _defensePenaltyPercent;
        private int _durationWaves;
        private int _maximumFeastStacks;

        private int _currentFeastStacks;
        private int _currentFamineStacks;
        private int _remainingFeastWaves;
        private int _remainingFamineWaves;
        private bool _hasActiveEffect;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _feastChancePercent = GetParameter("FeastChancePercent", 50f);
            _attackDamageBonusPercent = GetParameter("AttackDamageBonusPercent", 8f);
            _defensePenaltyPercent = GetParameter("DefensePenaltyPercent", 15f);
            _durationWaves = Mathf.RoundToInt(GetParameter("DurationWaves", 1f));
            _maximumFeastStacks = Mathf.RoundToInt(GetParameter("MaximumFeastStacks", 3f));
        }

        public override void OnWaveComplete(int waveNumber)
        {
            if (_hasActiveEffect)
            {
                // Decrement remaining waves for active effect
                if (_remainingFeastWaves > 0)
                {
                    _remainingFeastWaves--;
                    if (_remainingFeastWaves <= 0)
                    {
                        _currentFeastStacks = 0;
                        ApplyModifiers();
                    }
                }
                else if (_remainingFamineWaves > 0)
                {
                    _remainingFamineWaves--;
                    if (_remainingFamineWaves <= 0)
                    {
                        _currentFamineStacks = 0;
                        ApplyModifiers();
                    }
                }
                _hasActiveEffect = _remainingFeastWaves > 0 || _remainingFamineWaves > 0;
            }

            // Roll for new effect
            RollNewEffect();
        }

        private void RollNewEffect()
        {
            if (Utilityku.Chance(_feastChancePercent))
            {
                // FEAST - Attack Damage bonus
                if (_currentFeastStacks < _maximumFeastStacks)
                {
                    _currentFeastStacks++;
                }
                _remainingFeastWaves = _durationWaves;
                _currentFamineStacks = 0;
                _remainingFamineWaves = 0;
                _hasActiveEffect = true;
            }
            else
            {
                // FAMINE - Defense penalty
                _currentFamineStacks = 1; // Single stack for penalty
                _remainingFamineWaves = _durationWaves;
                _currentFeastStacks = 0;
                _remainingFeastWaves = 0;
                _hasActiveEffect = true;
            }
            ApplyModifiers();
        }

        private void ApplyModifiers()
        {
            ModifierManager.Instance.RemoveModifier(FeastModifierId);
            ModifierManager.Instance.RemoveModifier(FamineModifierId);

            if (_currentFeastStacks > 0)
            {
                float bonusPercent = _currentFeastStacks * _attackDamageBonusPercent;
                var modifier = new StatModifier
                {
                    Id = FeastModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }

            if (_currentFamineStacks > 0)
            {
                // Defense penalty is a negative defense bonus
                float penaltyPercent = _defensePenaltyPercent;
                var modifier = new StatModifier
                {
                    Id = FamineModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.DefenseAmount,
                    Mode = ModifierMode.Percent,
                    Value = -penaltyPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(FeastModifierId);
            ModifierManager.Instance.RemoveModifier(FamineModifierId);
            _currentFeastStacks = 0;
            _currentFamineStacks = 0;
            _remainingFeastWaves = 0;
            _remainingFamineWaves = 0;
            _hasActiveEffect = false;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            ApplyModifiers();
        }
    }
}