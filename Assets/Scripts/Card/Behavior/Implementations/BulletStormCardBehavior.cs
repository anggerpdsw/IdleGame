using UnityEngine;
using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BulletStormCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.BulletStorm;

        private int _attackCounter;
        private int _triggerCount;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _attackCounter = 0;
            _triggerCount = Mathf.Max(1, Mathf.CeilToInt(state.CurrentValue));
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _attackCounter = 0;
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            _triggerCount = Mathf.Max(1, Mathf.CeilToInt(state.CurrentValue));
        }

        public override void OnPlayerAttack()
        {
            _attackCounter++;
            int triggerCount = Mathf.Max(1, Mathf.CeilToInt(
                _triggerCount / CardModifierService.GetProjectileEffectTriggerRateMultiplier()));
            if (_attackCounter >= triggerCount)
            {
                _attackCounter = 0;
                TriggerBurst();
            }
        }

        private void TriggerBurst()
        {
            var player = PlayerClass.Instance;
            if (player != null)
            {
                player.SpawnBulletStormBurst();
            }
        }
    }
}