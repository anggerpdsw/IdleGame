using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Player
{
    /// <summary>Presentation only. No gameplay rules or state.</summary>
    public sealed class PlayerEffectsView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _barrierRenderer;
        [SerializeField] private SpriteRenderer _iceRenderer;
        [SerializeField] private SpriteRenderer _burnRenderer;
        
        [Header("Card Bonus")]
        [SerializeField] private Image _vampireImage;

        public void Configure(Player player) { }

        public void DisableAll()
        {
            SetBarrier(false);
            SetIce(false);
            SetBurn(false);
            SetVampire(false);
        }

        public void SetBarrier(bool value) { if (_barrierRenderer != null) _barrierRenderer.enabled = value; }
        public void SetIce(bool value) { if (_iceRenderer != null) _iceRenderer.enabled = value; }
        public void SetBurn(bool value) { if (_burnRenderer != null) _burnRenderer.enabled = value; }
        public void SetVampire(bool value) { if (_vampireImage != null) _vampireImage.gameObject.SetActive(value); }
    }
}