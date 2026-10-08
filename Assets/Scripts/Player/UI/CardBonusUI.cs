using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Card.Behavior;

namespace IdleDefenseSurvival.Player
{
    public sealed class CardBonusUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CardBonusWidget _widgetPrefab;
        [SerializeField] private Transform _container;

        private readonly Dictionary<CardEffectType, CardBonusWidget> _activeWidgets = new();
        private Player _player;

        public void Configure(Player player) => _player = player;

        private void OnEnable()
        {
            if (CardRuntimeManager.Instance != null)
                CardRuntimeManager.Instance.OnBehaviorsUpdated += Refresh;
        }

        private void OnDisable()
        {
            if (CardRuntimeManager.Instance != null)
                CardRuntimeManager.Instance.OnBehaviorsUpdated -= Refresh;
        }

        public void Refresh()
        {
            if (CardRuntimeManager.Instance == null) return;

            var providers = CardRuntimeManager.Instance.GetHUDProviders();
            var activeTypes = new HashSet<CardEffectType>();

            foreach (var provider in providers)
            {
                if (provider == null) continue;

                var data = provider.GetHUDData();

                // Skip only if icon is null (card wants to be hidden)
                // Allow empty text (e.g. Angel ready state shows icon only)
                if (data.Icon == null) continue;

                activeTypes.Add(provider.EffectType);
                var widget = GetOrCreateWidget(provider.EffectType);
                widget.Refresh(data);
            }

            RemoveInactiveWidgets(activeTypes);
        }

        private CardBonusWidget GetOrCreateWidget(CardEffectType type)
        {
            if (_activeWidgets.TryGetValue(type, out var existing))
                return existing;

            var widget = Instantiate(_widgetPrefab, _container);
            widget.name = $"{type}Widget";
            _activeWidgets.Add(type, widget);
            return widget;
        }

        private void RemoveInactiveWidgets(HashSet<CardEffectType> activeTypes)
        {
            var removed = new List<CardEffectType>();

            foreach (var pair in _activeWidgets)
            {
                if (activeTypes.Contains(pair.Key)) continue;
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                removed.Add(pair.Key);
            }

            foreach (var type in removed)
                _activeWidgets.Remove(type);
        }
    }
}
