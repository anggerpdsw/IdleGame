using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class DeathReversalCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.DeathReversal;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnPlayerDamaged, CardEventType.OnWaveStart };

        private readonly Queue<PlayerSnapshot> _snapshots = new();
        private float _snapshotTimer;
        private bool _usedThisWave;

        private readonly struct PlayerSnapshot
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly float Health;

            public PlayerSnapshot(float time, Vector3 position, float health)
            {
                Time = time;
                Position = position;
                Health = health;
            }
        }

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _usedThisWave = false;
            _snapshotTimer = 0f;
            _snapshots.Clear();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            _usedThisWave = false;
            _snapshots.Clear();
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
        }

        public override void OnWaveStart(int waveNumber)
        {
            _usedThisWave = false;
            _snapshots.Clear();
            _snapshotTimer = 0f;
        }

        public override void OnPlayerDamaged(float damage, float currentHp, float maxHp)
        {
            if (currentHp > 0) return; // Only trigger on death
            if (_usedThisWave) return;
            if (!CanTrigger()) return;

            TriggerReversal();
        }

        public override void Update(float deltaTime)
        {
            if (_usedThisWave) return;
            _snapshotTimer -= deltaTime;
            if (_snapshotTimer <= 0f)
            {
                _snapshotTimer = GetParameter("SnapshotIntervalSeconds");
                var player = PlayerClass.Instance;
                if (player != null)
                {
                    float now = Time.time;
                    _snapshots.Enqueue(new PlayerSnapshot(now, player.transform.position, player.CurrentHealth));

                    float rewindSeconds = GetParameter("RewindSeconds");
                    while (_snapshots.Count > 1 && now - _snapshots.Peek().Time > rewindSeconds)
                        _snapshots.Dequeue();
                }
            }
        }

        public bool CanTrigger() => !_usedThisWave && _snapshots.Count > 0;

        public bool TriggerReversal()
        {
            if (!CanTrigger()) return false;

            var player = PlayerClass.Instance;
            if (player == null) return false;

            PlayerSnapshot snapshot = _snapshots.Peek();
            float targetTime = Time.time - GetParameter("RewindSeconds");
            foreach (var candidate in _snapshots)
            {
                if (candidate.Time > targetTime) break;
                snapshot = candidate;
            }

            player.transform.position = snapshot.Position;

            float maxHP = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            _usedThisWave = true;
            float targetHP = Mathf.Max(snapshot.Health, maxHP * GetCurrentValue() * 0.01f);
            player.Heal(Mathf.Min(targetHP, maxHP));

            // Clear enemy projectiles in radius
            Vector3 playerPos = player.transform.position;
            float clearRadius = GetParameter("ProjectileClearRadius");
            ProjectilePool.Instance?.ClearWhere(proj =>
                proj.Owner == ProjectileOwner.Enemy &&
                Vector3.Distance(proj.transform.position, playerPos) <= clearRadius
            );

            return true;
        }
    }
}