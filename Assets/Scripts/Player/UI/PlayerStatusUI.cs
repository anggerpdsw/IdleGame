using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerStatusUI : MonoBehaviour
    {
        [SerializeField] private Image _iceCooldown;
        [SerializeField] private Image _burnCooldown;
        [SerializeField] private Image _unregenCooldown;

        private Player _player;

        public void Configure(Player player) => _player = player;

        public void Tick()
        {
            float ice = PlayerStatusEffectManager.Instance?.GetIceCooldownFill() ?? 0f;
            float burn = PlayerStatusEffectManager.Instance?.GetBurnCooldownFill() ?? 0f;

            UpdateCooldown(_iceCooldown, ice);
            UpdateCooldown(_burnCooldown, burn);
        }

        public void SetUnregeneration(bool active)
        {
            if (_unregenCooldown != null)
                _unregenCooldown.gameObject.SetActive(active);
        }

        private static void UpdateCooldown(Image image, float fill)
        {
            if (image == null) return;
            fill = Mathf.Clamp01(fill);
            image.fillAmount = fill;
            image.gameObject.SetActive(fill > 0f);
        }
    }
}
