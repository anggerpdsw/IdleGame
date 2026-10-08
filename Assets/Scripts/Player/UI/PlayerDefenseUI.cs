using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerDefenseUI : MonoBehaviour
    {
        [SerializeField] private Image _shield;

        private Player _player;

        public void Configure(Player player) => _player = player;

        public void RefreshShield(bool active, float remaining, float duration)
        {
            if (_shield == null) return;
            _shield.gameObject.SetActive(active);
            _shield.fillAmount = active ? 1f - Mathf.Clamp01(remaining / duration) : 0f;
        }
    }
}
