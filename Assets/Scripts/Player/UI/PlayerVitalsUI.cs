using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerVitalsUI : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Image _fillHealth;

        [Header("Mana")]
        [SerializeField] private Slider _manaBar;
        [SerializeField] private Image _fillMana;

        private Player _player;

        public void Configure(Player player) => _player = player;

        public void RefreshHealth(float current, float max)
        {
            if (_healthBar == null) return;
            _healthBar.maxValue = max;
            _healthBar.value = current;

            if (_fillHealth == null) return;
            float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _fillHealth.color = percent > .75f
                ? Color.Lerp(GameColors.yellow, GameColors.green,
                    Mathf.InverseLerp(.75f, 1f, percent))
                : Color.Lerp(GameColors.red, GameColors.yellow,
                    Mathf.InverseLerp(0f, .75f, percent));
        }

        public void RefreshMana(float current, float max)
        {
            if (_manaBar == null) return;
            _manaBar.maxValue = max;
            _manaBar.value = current;

            if (_fillMana == null) return;
            float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _fillMana.color = Color.Lerp(GameColors.empty, GameColors.blue, percent);
        }
    }
}
