using UnityEngine;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class AngelCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.Immortal;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnWaveComplete, CardEventType.OnWaveStart };

        private float _cooldownRemaining;
        private int _immunityWavesRemaining;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _cooldownRemaining = 0f;
            _immunityWavesRemaining = 0;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            var player = PlayerClass.Instance;
            if (player != null) player.SetBarrierEffect(false);
        }

        // JANGAN reset _cooldownRemaining di sini
        // Biarkan cooldown terus berjalan via Update()
        public override void OnWaveStart(int waveNumber) => SetAngelCooldown();
        public override void OnWaveComplete(int waveNumber) => SetAngelCooldown();

        public override void Update(float deltaTime)
        {
            if (_cooldownRemaining > 0f)
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - deltaTime);
        }

        public bool CanTrigger() => _cooldownRemaining <= 0f && _immunityWavesRemaining <= 0;

        public bool TriggerAngel()
        {
            if (!CanTrigger()) return false;

            var player = PlayerClass.Instance;
            if (player == null) return false;

            float cooldownSeconds = GetCurrentValue();
            _cooldownRemaining = cooldownSeconds;
            _immunityWavesRemaining = 1; // immunity lasts one full wave

            // Visual barrier only; no defense stat modifier.
            player.SetBarrierEffect(true);

            return true;
        }

        private void SetAngelCooldown() 
        {
            if (_immunityWavesRemaining > 0)
            {
                _immunityWavesRemaining--;
                if (_immunityWavesRemaining == 0)
                {
                    var player = PlayerClass.Instance;
                    if (player != null) player.SetBarrierEffect(false);
                }
            }
        }

        public float GetCooldownRemaining() => Mathf.Max(_cooldownRemaining, 0f);
        public bool HasImmunity() => _immunityWavesRemaining > 0;
    }
}