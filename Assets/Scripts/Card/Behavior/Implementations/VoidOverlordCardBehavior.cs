
namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class VoidOverlordCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.VoidOverlord;

        private float _timer;
        private float _duration;
        private bool _active;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _timer = 0f;
            _duration = 0f;
            _active = false;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _timer = 0f;
            _duration = 0f;
            _active = false;
        }

        public override void Update(float deltaTime)
        {
            if (_active)
            {
                _duration -= deltaTime;
                if (_duration <= 0f)
                {
                    _active = false;
                    _duration = 0f;
                }
            }
            else
            {
                _timer += deltaTime;
                float interval = GetParameter("IntervalSeconds");
                if (_timer >= interval)
                {
                    _timer = 0f;
                    _duration = GetParameter("DurationSeconds");
                    _active = true;
                }
            }
        }

        public bool IsActive() => _active;
        public float GetDamageBonusPercent() => _active ? GetCurrentValue() : 0f;
        public bool GetPierceEnabled() => _active;
        public bool GetPreventEnemyHeal() => _active;
    }
}