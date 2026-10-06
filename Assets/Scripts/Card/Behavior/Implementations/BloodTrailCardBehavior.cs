using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;

using PlayerClass = IdleDefenseSurvival.Player.Player;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class BloodTrailCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.BloodTrail;
        public override CardEventType[] SubscribedEvents => System.Array.Empty<CardEventType>();

        private float _trailDamagePercent;
        private float _trailRadius;
        private float _trailDurationSeconds;
        private float _slowPercent;
        private float _minimumMovementDistance;
        private int _maximumTrailSegments;

        private Vector3 _lastPosition;
        private bool _hasPosition;
        private readonly List<TrailSegment> _trailSegments = new();

        private class TrailSegment
        {
            public Vector3 Position;
            public float RemainingTime;
        }

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _trailDamagePercent = GetParameter("TrailDamagePercent", 18f);
            _trailRadius = GetParameter("TrailRadius", 1.5f);
            _trailDurationSeconds = GetParameter("TrailDurationSeconds", 3f);
            _slowPercent = GetParameter("SlowPercent", 20f) / 100f;
            _minimumMovementDistance = GetParameter("MinimumMovementDistance", 1.5f);
            _maximumTrailSegments = Mathf.RoundToInt(GetParameter("MaximumTrailSegments", 12f));

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

            if (_hasPosition && distanceMoved >= _minimumMovementDistance)
            {
                // Create trail segment at midpoint
                Vector3 segmentPos = Vector3.Lerp(_lastPosition, currentPos, 0.5f);
                _trailSegments.Add(new TrailSegment
                {
                    Position = segmentPos,
                    RemainingTime = _trailDurationSeconds
                });

                // Limit trail segments
                while (_trailSegments.Count > _maximumTrailSegments)
                {
                    _trailSegments.RemoveAt(0);
                }

                _lastPosition = currentPos;
            }

            // Update and process trail segments
            float damagePerSecond = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * _trailDamagePercent / 100f;

            for (int i = _trailSegments.Count - 1; i >= 0; i--)
            {
                var segment = _trailSegments[i];
                segment.RemainingTime -= deltaTime;

                if (segment.RemainingTime <= 0f)
                {
                    _trailSegments.RemoveAt(i);
                    continue;
                }

                // Damage and slow enemies in radius
                Collider2D[] hits = Physics2D.OverlapCircleAll(segment.Position, _trailRadius, LayerMask.GetMask("Enemy"));
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent<EnemyAi>(out var enemy))
                    {
                        if (enemy.CurrentHealth <= 0) continue;

                        // Apply damage
                        var damageData = new DamageData(damagePerSecond * deltaTime, DamageType.Normal, CriticalType.None, "BloodTrail")
                        {
                            Element = Element.None
                        };
                        enemy.TakeDamage(damageData);

                        // Apply slow
                        var slowEffect = new SlowStatus(_slowPercent, deltaTime * 2f);
                        enemy.EnemyStatusEffect?.AddEffect(slowEffect);
                    }
                }
            }

            _hasPosition = true;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            _trailSegments.Clear();
            _hasPosition = false;
        }
    }
}