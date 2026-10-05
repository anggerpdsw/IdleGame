using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Enemy;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class PanicFireCardBehavior : CardBehaviorBase
    {
        public override CardEffectType EffectType => CardEffectType.PanicFire;

        private const string ModifierId = "Card:PanicFire";
        private const string PierceModifierId = "Card:PanicFire_Pierce";

        private float _radius;
        private int _enemiesPerStack;
        private float _attackSpeedPerStackPercent;
        private int _maximumStacks;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            _radius = GetParameter("Radius", 6f);
            _enemiesPerStack = Mathf.RoundToInt(GetParameter("EnemiesPerStack", 3f));
            _attackSpeedPerStackPercent = GetParameter("AttackSpeedPerStackPercent", 3f);
            _maximumStacks = Mathf.RoundToInt(GetParameter("MaximumStacks", 8f));
        }

        public override void OnPlayerAttack()
        {
            UpdateModifier();
        }

        private void UpdateModifier()
        {
            var player = PlayerClass.Instance;
            if (player == null) return;

            int enemyCount = CountEnemiesInRange(player.transform.position, _radius);
            int stacks = Mathf.Min(enemyCount / _enemiesPerStack, _maximumStacks);
            bool isActive = stacks > 0;
            bool atMaxStacks = stacks >= _maximumStacks;

            ModifierManager.Instance.RemoveModifier(ModifierId);
            ModifierManager.Instance.RemoveModifier(PierceModifierId);

            if (isActive)
            {
                float bonusPercent = stacks * _attackSpeedPerStackPercent;
                var modifier = new StatModifier
                {
                    Id = ModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.AttackSpeed,
                    Mode = ModifierMode.Percent,
                    Value = bonusPercent,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(modifier);
            }

            // At max stacks, grant 1 extra pierce
            if (atMaxStacks)
            {
                var pierceMod = new StatModifier
                {
                    Id = PierceModifierId,
                    Source = ModifierSource.Card,
                    Stat = SkillType.PierceCount,
                    Mode = ModifierMode.Flat,
                    Value = 1f,
                    Permanent = false
                };
                ModifierManager.Instance.AddModifier(pierceMod);
            }
        }

        private int CountEnemiesInRange(Vector2 center, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, LayerMask.GetMask("Enemy"));
            int count = 0;
            foreach (var hit in hits)
            {
                if (hit != null && hit.GetComponent<EnemyAi>() != null)
                    count++;
            }
            return count;
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            base.OnUnequip(state);
            ModifierManager.Instance.RemoveModifier(ModifierId);
            ModifierManager.Instance.RemoveModifier(PierceModifierId);
        }

        public override void OnUpgrade(CardRuntimeState state, int oldLevel, int newLevel)
        {
            base.OnUpgrade(state, oldLevel, newLevel);
            UpdateModifier();
        }
    }
}