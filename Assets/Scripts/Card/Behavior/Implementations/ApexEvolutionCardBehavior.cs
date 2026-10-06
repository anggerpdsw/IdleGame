using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Card.Behavior.Implementations
{
    public sealed class ApexEvolutionCardBehavior : CardBehaviorBase
    {
        private readonly HashSet<string> _selectedMutationIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CardMutationDefinition> _pendingChoices = new();
        private int _killsTowardOffer;

        public override CardEffectType EffectType => CardEffectType.ApexEvolution;
        public override CardEventType[] SubscribedEvents => new[] { CardEventType.OnEnemyKilled, CardEventType.OnWaveStart };
        public IReadOnlyList<CardMutationDefinition> PendingChoices => _pendingChoices;
        public bool HasPendingChoices => _pendingChoices.Count > 0;

        public override void OnEquip(CardRuntimeState state)
        {
            base.OnEquip(state);
            ResetBattleState();
        }

        public override void OnUnequip(CardRuntimeState state)
        {
            ResetBattleState();
        }

        public override void OnWaveStart(int waveNumber)
        {
            ResetBattleState();
        }

        public override void OnEnemyKilled(int count, string enemyType, string damageSource)
        {
            if (count <= 0 || !IsPlayerOwnedKill(damageSource)) return;
            _killsTowardOffer += count;
            TryOfferMutation();
        }

        public bool SelectMutation(string mutationId)
        {
            if (string.IsNullOrWhiteSpace(mutationId)) return false;

            CardMutationDefinition selected = null;
            foreach (var choice in _pendingChoices)
            {
                if (string.Equals(choice.Id, mutationId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = choice;
                    break;
                }
            }

            if (selected == null || !_selectedMutationIds.Add(selected.Id)) return false;
            ApplyMutation(selected);
            _pendingChoices.Clear();
            TryOfferMutation();
            CardRuntimeManager.Instance?.NotifyApexEvolutionChoicesUpdated();
            return true;
        }

        public float GetDevourerDamageMultiplier(float targetHealthFraction)
        {
            var mutation = GetSelectedMutation("devourer");
            if (mutation == null) return 1f;

            float threshold = mutation.HealthThresholdPercent * 0.01f;
            return targetHealthFraction >= threshold
                ? 1f + mutation.Value * 0.01f
                : 1f;
        }

        public float GetProjectileEffectTriggerRateMultiplier()
        {
            var mutation = GetSelectedMutation("storm_heart");
            return mutation == null ? 1f : 1f + mutation.Value * 0.01f;
        }

        private void TryOfferMutation()
        {
            if (_pendingChoices.Count > 0 || _definition?.Mutations == null) return;

            int killsPerOffer = Mathf.Max(1, Mathf.CeilToInt(GetCurrentValue()));
            if (_killsTowardOffer < killsPerOffer) return;
            _killsTowardOffer -= killsPerOffer;

            var available = new List<CardMutationDefinition>();
            foreach (var mutation in _definition.Mutations)
            {
                if (mutation != null && !_selectedMutationIds.Contains(mutation.Id))
                    available.Add(mutation);
            }

            int choiceCount = Mathf.Max(1, Mathf.RoundToInt(GetParameter("ChoicesPerOffer")));
            int count = Mathf.Min(choiceCount, available.Count);
            for (int i = 0; i < count; i++)
            {
                int selectedIndex = UnityEngine.Random.Range(0, available.Count);
                _pendingChoices.Add(available[selectedIndex]);
                available.RemoveAt(selectedIndex);
            }

            if (_pendingChoices.Count > 0)
                CardRuntimeManager.Instance?.NotifyApexEvolutionChoicesUpdated();
        }

        private void ApplyMutation(CardMutationDefinition mutation)
        {
            if (!string.IsNullOrWhiteSpace(mutation.SkillType)
                && Enum.TryParse(mutation.SkillType, true, out SkillType skillType)
                && Enum.TryParse(mutation.Mode, true, out ModifierMode mode))
            {
                ModifierManager.Instance.AddModifier(new StatModifier
                {
                    Id = $"Card:ApexEvolution:{mutation.Id}",
                    Source = ModifierSource.Card,
                    Stat = skillType,
                    Mode = mode,
                    Value = mutation.Value,
                    Permanent = false
                });
            }

            PlayerStatsManager.Instance?.RefreshStats();
        }

        private CardMutationDefinition GetSelectedMutation(string mutationId)
        {
            if (!_selectedMutationIds.Contains(mutationId) || _definition?.Mutations == null) return null;
            foreach (var mutation in _definition.Mutations)
            {
                if (string.Equals(mutation.Id, mutationId, StringComparison.OrdinalIgnoreCase))
                    return mutation;
            }
            return null;
        }

        private void ResetBattleState()
        {
            foreach (string mutationId in _selectedMutationIds)
                ModifierManager.Instance.RemoveModifier($"Card:ApexEvolution:{mutationId}");

            _selectedMutationIds.Clear();
            _pendingChoices.Clear();
            _killsTowardOffer = 0;
            PlayerStatsManager.Instance?.RefreshStats();
            CardRuntimeManager.Instance?.NotifyApexEvolutionChoicesUpdated();
        }

        private static bool IsPlayerOwnedKill(string damageSource)
        {
            return Enum.TryParse(damageSource, true, out DamageSource _)
                || damageSource == "Overkill"
                || damageSource == "ChainReactionExplosion"
                || damageSource == "LawOfCollapse";
        }
    }
}
