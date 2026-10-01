using System;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class WorldBreakerCardBehavior : CardBehaviorBase
    {
        private int _killProgress;
        private int _stacks;

        public override CardEffectType EffectType => CardEffectType.WorldBreaker;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            var ownedCard = CardInventory.Instance?.GetOwnedCard(state.CardId);
            _stacks = ownedCard?.WorldBreakerStacks ?? 0;
            _killProgress = ownedCard?.WorldBreakerKillProgress ?? 0;
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (count <= 0 || !IsPlayerOwnedKill(damageSource)) return;

            int killsPerStack = (int)GetParameter("KillsPerStack");
            if (killsPerStack <= 0) return;

            long totalProgress = (long)_killProgress + count;
            long stacksGained = totalProgress / killsPerStack;
            _killProgress = (int)(totalProgress % killsPerStack);
            _stacks = (int)System.Math.Min(int.MaxValue, (long)_stacks + stacksGained);
            CardInventory.Instance?.SetWorldBreakerProgress(CardId, _stacks, _killProgress);
        }

        public float GetDamageMultiplier()
            => 1f + _stacks * GetCurrentValue() * 0.01f;

        public float GetDefenseIgnoreFraction()
        {
            int stacksPerMilestone = (int)GetParameter("StacksPerDefenseIgnoreMilestone");
            if (stacksPerMilestone <= 0) return 0f;

            float ignorePercent = _stacks / stacksPerMilestone
                * GetParameter("DefenseIgnorePercentPerMilestone");
            ignorePercent = UnityEngine.Mathf.Min(
                ignorePercent,
                GetParameter("MaximumDefenseIgnorePercent"));
            return UnityEngine.Mathf.Clamp01(ignorePercent * 0.01f);
        }

        public int GetStackCount() => _stacks;

        private static bool IsPlayerOwnedKill(string damageSource)
        {
            return Enum.TryParse(damageSource, true, out DamageSource _)
                || damageSource == "Overkill"
                || damageSource == "ChainReactionExplosion"
                || damageSource == "LawOfCollapse";
        }
    }
}
