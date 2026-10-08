using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    /// <summary>Coordinates player HUD presenters. No card-specific logic.</summary>
    public sealed class PlayerUIController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource _sfxSource;

        [Header("Sub-Presenters")]
        [SerializeField] private PlayerVitalsUI _vitalsUI;
        [SerializeField] private PlayerCombatUI _combatUI;
        [SerializeField] private PlayerStatusUI _statusUI;
        [SerializeField] private PlayerDefenseUI _defenseUI;
        [SerializeField] private CardBonusUI _cardBonusUI;

        private Player _player;

        public AudioSource SfxSource => _sfxSource;

        public void Configure(Player player)
        {
            _player = player;
            _vitalsUI?.Configure(player);
            _combatUI?.Configure(player);
            _statusUI?.Configure(player);
            _defenseUI?.Configure(player);
            _cardBonusUI?.Configure(player);
        }

        private void OnEnable()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged += HandleStatsChanged;
        }

        private void OnDisable()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged -= HandleStatsChanged;
        }

        private void HandleStatsChanged()
        {
            _combatUI?.RefreshAttackRange();
        }

        public void Tick()
        {
            _statusUI?.Tick();
            _combatUI?.Tick();
        }

        public void RefreshAll()
        {
            if (_player == null) return;
            _vitalsUI?.RefreshHealth(_player.CurrentHealth, _player.MaxHealth);
            _vitalsUI?.RefreshMana(_player.CurrentMana, _player.MaxMana);
        }

        public void RefreshHealth(float current, float max)
        {
            _vitalsUI?.RefreshHealth(current, max);
        }

        public void RefreshMana(float current, float max)
        {
            _vitalsUI?.RefreshMana(current, max);
        }

        public void RefreshShieldCooldown(bool active, float remaining, float duration)
        {
            _defenseUI?.RefreshShield(active, remaining, duration);
        }

        public void SetUnregeneration(bool active)
        {
            _statusUI?.SetUnregeneration(active);
        }

        public void RefreshCardBonusUI()
        {
            _cardBonusUI?.Refresh();
        }

        public void DrawAttackRange(float range)
        {
            _combatUI?.RefreshAttackRange();
        }
    }
}
