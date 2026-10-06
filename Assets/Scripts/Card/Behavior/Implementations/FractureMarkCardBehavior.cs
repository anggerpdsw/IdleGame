using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class FractureMarkCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.FractureMark;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled, CardEventType.OnPlayerAttack };

        private class FractureMark
        {
            public float ExpiryTime;
            public int HitsRemaining;
        }

        private Dictionary<EnemyAi, FractureMark> _marks = new();
        private int _attacksThisFrame;
        private const string ModifierId = "Card:FractureMark";

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _marks.Clear();
            _attacksThisFrame = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            _marks.Clear();
            _attacksThisFrame = 0;
            ModifierManager.Instance.RemoveModifier(ModifierId);
        }

        public override void OnPlayerAttack()
        {
            float markChance = GetCurrentValue() * 0.01f;
            if (Random.value < markChance)
            {
                var enemies = Physics2D.OverlapCircleAll(
                    Player.Player.Instance.transform.position,
                    PlayerStatsManager.Instance.GetStat(SkillType.AttackRange),
                    LayerMask.GetMask("Enemy"));

                if (enemies.Length > 0)
                {
                    if (enemies[Random.Range(0, enemies.Length)].TryGetComponent<EnemyAi>(out var enemy))
                        MarkEnemy(enemy);
                }
            }

            _attacksThisFrame++;
        }

        public override void Update(float deltaTime)
        {
            var expired = new List<EnemyAi>();
            foreach (var kvp in _marks)
            {
                if (kvp.Value.ExpiryTime < Time.time)
                    expired.Add(kvp.Key);
            }
            foreach (var enemy in expired)
                _marks.Remove(enemy);

            _attacksThisFrame = 0;
        }

        private void MarkEnemy(EnemyAi enemy)
        {
            if (enemy == null) return;
            float duration = GetParameter("MarkDurationSeconds");
            _marks[enemy] = new FractureMark
            {
                ExpiryTime = Time.time + duration,
                HitsRemaining = Mathf.RoundToInt(GetParameter("MaximumBonusHits"))
            };
        }

        public float GetBonusMultiplier(EnemyAi target)
        {
            if (target == null || !_marks.TryGetValue(target, out var mark)) return 1f;
            if (mark.ExpiryTime < Time.time)
            {
                _marks.Remove(target);
                return 1f;
            }

            mark.HitsRemaining--;
            if (mark.HitsRemaining <= 0)
                _marks.Remove(target);

            float bonus = GetParameter("DamageBonusPercent", 25f);
            return 1f + (bonus * 0.01f);
        }
    }
}
