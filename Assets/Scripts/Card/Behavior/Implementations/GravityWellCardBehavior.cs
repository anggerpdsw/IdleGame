using UnityEngine;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class GravityWellCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.GravityWell;

        private float _nextTriggerTime;
        private float _wellExpiry;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _nextTriggerTime = 0f;
            _wellExpiry = 0f;
        }

        public override void Update(float deltaTime)
        {
            float now = Time.time;
            if (now >= _nextTriggerTime)
            {
                CreateWell();
                float interval = GetCurrentValue();
                _nextTriggerTime = now + Mathf.Max(0.1f, interval);
            }

            if (_wellExpiry > 0f && now >= _wellExpiry)
                _wellExpiry = 0f;
        }

        private void CreateWell()
        {
            var playerPos = Player.Player.Instance.transform.position;
            float radius = GetParameter("Radius", 5f);
            float durationSeconds = GetParameter("DurationSeconds", 2.5f);
            float slowPercent = GetParameter("SlowPercent", 25f);
            float pullStrength = GetParameter("PullStrength", 3f);

            var enemies = Physics2D.OverlapCircleAll(playerPos, radius, LayerMask.GetMask("Enemy"));

            foreach (var col in enemies)
            {
                if (!col.TryGetComponent<EnemyAi>(out var enemy)) continue;

                // Pull toward center
                Vector3 direction = (playerPos - enemy.transform.position).normalized;
                if (enemy.TryGetComponent<Rigidbody2D>(out var rb))
                    rb.linearVelocity = direction * pullStrength;

                // Apply slow status
                if (enemy.TryGetComponent<EnemyStatusEffectController>(out var statusCtrl))
                {
                    var slowStatus = new SlowStatus(slowPercent, durationSeconds);
                    statusCtrl.AddEffect(slowStatus);
                }
            }

            _wellExpiry = Time.time + durationSeconds;
        }
    }

}
