using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DeathMomentumCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.DeathMomentum;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled };

        private const string MoveSpeedModifierId = "Card:DeathMomentum_MoveSpeed";
        private const string AttackDamageModifierId = "Card:DeathMomentum_AttackDamage";
        private const string AttackSpeedModifierId = "Card:DeathMomentum_AttackSpeed";

        private float _movementSpeedPerStackPercent;
        private float _attackDamagePerStackPercent;
        private float _attackSpeedPerStackPercent;
        private int _maximumStacks;
        private float _stackDurationSeconds;
        private float _stopLossPerSecond;
        private float _graceDurationSeconds;

        private int _currentStacks;
        private float _stackTimer;
        private float _stopTimer;
        private float _graceTimer;
        private Vector3 _lastPosition;
        private bool _hasPosition;
        private bool _inGracePeriod;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _movementSpeedPerStackPercent = GetParameter("MovementSpeedPerStackPercent", 3f);
            _attackDamagePerStackPercent = GetParameter("AttackDamagePerStackPercent", 2f);
            _attackSpeedPerStackPercent = GetParameter("AttackSpeedPerStackPercent", 1f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 15f));
            _stackDurationSeconds = GetParameter("StackDurationSeconds", 6f);
            _stopLossPerSecond = GetParameter("StopLossPerSecond", 1f);
            _graceDurationSeconds = GetParameter("GraceDurationSeconds", 2f);

            var player = PlayerClass.Instance;
            if (player != null)
            {
                _lastPosition = player.transform.position;
                _hasPosition = true;
            }
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            for (int i = 0; i < count; i++)
            {
                AddStack();
            }
        }

        public override void OnEnemyKilled(EnemyAi enemy, string damageSource)
        {
            AddStack();
        }

        private void AddStack()
        {
            if (_currentStacks < _maximumStacks)
            {
                _currentStacks++;
                _stackTimer = _stackDurationSeconds;
                _graceTimer = _graceDurationSeconds;
                _inGracePeriod = true;
                ApplyModifiers();
            }
            else
            {
                // Refresh duration
                _stackTimer = _stackDurationSeconds;
                _graceTimer = _graceDurationSeconds;
                _inGracePeriod = true;
            }
        }

        public override void Update(float deltaTime)
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            Vector3 currentPos = player.transform.position;
            float distanceMoved = Vector3.Distance(currentPos, _lastPosition);

            bool isMoving = _hasPosition && distanceMoved > 0.01f;

            if (isMoving)
            {
                // Player is moving - reset stop timer, tick grace timer
                _stopTimer = 0f;
                if (_inGracePeriod)
                {
                    _graceTimer -= deltaTime;
                    if (_graceTimer <= 0f)
                        _inGracePeriod = false;
                }
            }
            else
            {
                // Player is stopped
                if (_inGracePeriod)
                {
                    _graceTimer -= deltaTime;
                    if (_graceTimer <= 0f)
                        _inGracePeriod = false;
                }
                else
                {
                    // Grace period over - lose stacks over time
                    _stopTimer += deltaTime;
                    if (_stopTimer >= 1f / _stopLossPerSecond && _currentStacks > 0)
                    {
                        _currentStacks = Mathf.Max(0, _currentStacks - 1);
                        _stopTimer = 0f;
                        ApplyModifiers();
                    }
                }
            }

            // Stack duration decay
            if (_currentStacks > 0)
            {
                _stackTimer -= deltaTime;
                if (_stackTimer <= 0f)
                {
                    _currentStacks = 0;
                    _inGracePeriod = false;
                    ApplyModifiers();
                }
            }

            _lastPosition = currentPos;
            _hasPosition = true;
        }

        private void ApplyModifiers()
        {
            ModifierManager.Instance.RemoveModifier(MoveSpeedModifierId);
            ModifierManager.Instance.RemoveModifier(AttackDamageModifierId);
            ModifierManager.Instance.RemoveModifier(AttackSpeedModifierId);

            if (_currentStacks > 0)
            {
                float moveSpeedBonus = _currentStacks * _movementSpeedPerStackPercent;
                float attackDamageBonus = _currentStacks * _attackDamagePerStackPercent;
                float attackSpeedBonus = _currentStacks * _attackSpeedPerStackPercent;

                var moveMod = new StatModifier
                {
                    Id = MoveSpeedModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.MoveSpeed,
                    Mode = ModifierMode.Percent,
                    Value = moveSpeedBonus,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(moveMod);

                var dmgMod = new StatModifier
                {
                    Id = AttackDamageModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackDamage,
                    Mode = ModifierMode.Percent,
                    Value = attackDamageBonus,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(dmgMod);

                var speedMod = new StatModifier
                {
                    Id = AttackSpeedModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackSpeed,
                    Mode = ModifierMode.Percent,
                    Value = attackSpeedBonus,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(speedMod);
            }
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(MoveSpeedModifierId);
            ModifierManager.Instance.RemoveModifier(AttackDamageModifierId);
            ModifierManager.Instance.RemoveModifier(AttackSpeedModifierId);
            _currentStacks = 0;
            _inGracePeriod = false;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            ApplyModifiers();
        }
    }
}