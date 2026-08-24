using UnityEngine;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Player
{
    public class AuraCollider : MonoBehaviour
    {
        [SerializeField] private CircleCollider2D _collider;
        private readonly HashSet<EnemyAi> _enemiesInAura = new();
        private float _cachedFrostAura;
        private int _enemyLayerMask;

        private void Awake()
        {
            _collider.enabled = false;
            _enemyLayerMask = LayerMask.GetMask("Enemy");

            var player = GameObject.FindWithTag("Player");
            if (player != null && transform.parent != player.transform)
            {
                transform.SetParent(player.transform);
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                transform.localScale = Vector3.one;
            }

            RefreshCachedValues();
        }

        private void Start()
        {
            UpdateColliderState();
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged += UpdateColliderState;
        }

        private void RefreshCachedValues()
            => _cachedFrostAura = Mathf.Max(0f, CardModifierService.GetEffectResult(CardEffectType.FrostAura));

        public void UpdateColliderState()
        {
            if (_collider == null) return;
            float oldRadius = _collider.radius;
            bool anyAuraActive = CardModifierService.HasAuraEffect();

            if (_collider.enabled != anyAuraActive)
                _collider.enabled = anyAuraActive;

            if (anyAuraActive)
            {
                float newRadius = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
                _collider.radius = newRadius;
                if (!Mathf.Approximately(oldRadius, newRadius))
                    ForceRefreshEnemies();
            }
            else
                ClearAllAuraEffects();
        }

        private void ForceRefreshEnemies()
        {
            ClearAllAuraEffects();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, _collider.radius, _enemyLayerMask))
                if (hit.TryGetComponent<EnemyAi>(out var enemy))
                {
                    _enemiesInAura.Add(enemy);
                    ApplyAuraEffects(enemy);
                }
        }

        private void ApplyAuraEffects(EnemyAi enemy)
        {
            if (enemy == null || _cachedFrostAura <= 0f) return;
            enemy.ApplySlow(SlowSource.Card, SlowType.Aura, _cachedFrostAura);
        }

        private void RemoveAuraEffects(EnemyAi enemy)
        {
            if (enemy == null) return;
            enemy.RemoveSlow(SlowSource.Card);
        }

        public void ClearAllAuraEffects()
        {
            _enemiesInAura.RemoveWhere(e => e == null);
            foreach (var e in _enemiesInAura) RemoveAuraEffects(e);
            _enemiesInAura.Clear();
        }

        public void RefreshAllAuraEffects()
        {
            _enemiesInAura.RemoveWhere(e => e == null);
            foreach (var e in _enemiesInAura) { RemoveAuraEffects(e); ApplyAuraEffects(e); }
        }

        private void OnModifierChanged()
        {
            RefreshCachedValues();
            UpdateColliderState();
            if (_collider.enabled) RefreshAllAuraEffects();
        }

        private void OnEnable() => CardModifierService.OnModifierChanged += OnModifierChanged;
        private void OnDisable()
        {
            CardModifierService.OnModifierChanged -= OnModifierChanged;
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged -= UpdateColliderState;
        }
        private void OnDestroy() => ClearAllAuraEffects();

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<EnemyAi>(out var enemy) || !_enemiesInAura.Add(enemy)) return;
            ApplyAuraEffects(enemy);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.TryGetComponent<EnemyAi>(out var enemy) || !_enemiesInAura.Remove(enemy)) return;
            RemoveAuraEffects(enemy);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_collider != null && _collider.enabled)
            {
                Gizmos.color = GameColors.debugBlueGizmo.WithAlpha(0.3f);
                Gizmos.DrawWireSphere(transform.position, _collider.radius);
            }
        }
#endif
    }
}