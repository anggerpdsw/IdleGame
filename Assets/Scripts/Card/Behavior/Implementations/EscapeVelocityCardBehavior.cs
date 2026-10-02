using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class EscapeVelocityCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.EscapeVelocity;

        private const string ModifierId = "Card:EscapeVelocity";
        private float _buildTimeSeconds;
        private float _damagePerStackPercent;
        private int _maximumStacks;
        private float _decayDelaySeconds;
        private float _decayIntervalSeconds;

        private float _moveTimer;
        private float _decayTimer;
        private int _currentStacks;
        private Vector3 _lastPosition;
        private bool _hasPosition;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _buildTimeSeconds = GetParameter("BuildTimeSeconds", 0.5f);
            _damagePerStackPercent = GetParameter("DamagePerStackPercent", 2f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 8f));
            _decayDelaySeconds = GetParameter("DecayDelaySeconds", 1.5f);
            _decayIntervalSeconds = GetParameter("DecayIntervalSeconds", 0.5f);

            var player = PlayerClass.Instance;
            if (player != null)
            {
                _lastPosition = player.transform.position;
                _hasPosition = true;
            }
        }

        public override void Update(float deltaTime)
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            Vector3 currentPos = player.transform.position;
            float distanceMoved = Vector3.Distance(currentPos, _lastPosition);

            if (_hasPosition && distanceMoved > 0.01f)
            {
                // Player is moving - build momentum
                _moveTimer += deltaTime;
                _decayTimer = 0f; // Reset decay timer

                if (_moveTimer >= _buildTimeSeconds && _currentStacks < _maximumStacks)
                {
                    _currentStacks++;
                    _moveTimer = 0f;
                    ApplyModifier();
                }
            }
            else
            {
                // Player is not moving - handle decay
                _moveTimer = 0f;
                _decayTimer += deltaTime;

                if (_decayTimer >= _decayDelaySeconds && _currentStacks > 0)
                {
                    _decayTimer -= _decayIntervalSeconds;
                    _currentStacks = Mathf.Max(0, _currentStacks - 1);
                    ApplyModifier();
                }
            }

            _lastPosition = currentPos;
            _hasPosition = true;
        }

        private void ApplyModifier()
        {
            ModifierManager.Instance.RemoveModifier(ModifierId);

            if (_currentStacks > 0)
            {
                float bonusPercent = _currentStacks * _damagePerStackPercent;
                var modifier = new StatModifier
                {
                    Id = ModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(ModifierId);
            _currentStacks = 0;
            _moveTimer = 0f;
            _decayTimer = 0f;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            ApplyModifier();
        }
    }
}