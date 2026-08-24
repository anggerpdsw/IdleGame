using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.UI.Game
{
    public class UltimateUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _cooldown;
        [SerializeField] private Button _button;

        public string UltimateID { get; private set; }

        public void Initialize(string ultimateID)
        {
            UltimateID = ultimateID;
            if (_icon == null) _icon = GetComponent<Image>();
            if (_cooldown == null) _cooldown = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_button == null) _button = GetComponent<Button>();

            // Ensure icon is set up for radial fill
            if (_icon != null)
            {
                _icon.type = Image.Type.Filled;
                _icon.fillMethod = Image.FillMethod.Radial360;
                _icon.fillOrigin = (int)Image.Origin360.Top;
                _icon.fillClockwise = true;
                _icon.fillAmount = 1f; // Full = ready
            }
        }

        /// <summary>Ties the slot's button to the panel's use handler.</summary>
        public void BindClick(Action onClick)
        {
            if (_button != null && onClick != null)
                _button.onClick.AddListener(() => onClick());
        }

        /// <summary>
        /// Sets cooldown radial fill (0 = ready, 1 = full cooldown). 
        /// Also blocks interaction during cooldown.
        /// </summary>
        public void SetCooldown(float fillAmount)
        {
            if (_icon != null)
                _icon.fillAmount = 1f - fillAmount; // Invert: 1 = ready (full), 0 = on cooldown (empty)
            if (_button != null)
                _button.interactable = fillAmount <= 0f;
        }
    }
}