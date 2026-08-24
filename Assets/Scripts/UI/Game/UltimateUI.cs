using System;
using IdleDefenseSurvival.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Game
{
    public class UltimateUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _cooldownText;
        [SerializeField] private TextMeshProUGUI _stackText;
        [SerializeField] private Button _button;

        public string UltimateID { get; private set; }
        public Button Button => _button;
        public bool IsReady => _button != null && _button.interactable;

        public void Initialize(string ultimateId)
        {
            UltimateID = ultimateId;
            if (_icon == null) _icon = GetComponent<Image>();
            if (_cooldownText == null) _cooldownText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_stackText == null) _stackText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (_button == null) _button = GetComponent<Button>();

            // Ensure icon is set up for radial fill
            if (_icon != null)
            {
                _icon.type = Image.Type.Filled;
                _icon.fillMethod = Image.FillMethod.Radial360;
                _icon.fillOrigin = (int)Image.Origin360.Top;
                _icon.fillClockwise = true;
                _icon.fillAmount = 1f; // Full = ready

                _icon.sprite = PlayerResources.GetUltimateSource(UltimateID);
            }

            // Hide stack text initially
            if (_stackText != null)
            {
                _stackText.enabled = false;
                _stackText.text = "";
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
            fillAmount = Mathf.Clamp01(fillAmount);
            // Invert: 1 = ready (full), 0 = on cooldown (empty)
            if (_icon != null)
                _icon.fillAmount = 1f - fillAmount;
            if (_cooldownText != null)
            {
                if (fillAmount > 0f)
                {
                    var ultimateManager = Ultimate.UltimateManager.Instance;
                    if (ultimateManager != null && ultimateManager.TryGetUltimate(UltimateID, out var data))
                    {
                        float cooldown = data.GetCooldown();
                        float remaining = cooldown * fillAmount;
                        _cooldownText.text = $"{remaining:F1}s";
                        _cooldownText.enabled = true;
                    }
                }
                else
                {
                    _cooldownText.enabled = false;
                }
            }

            if (_button != null)
                _button.interactable = fillAmount <= 0f;
        }

        /// <summary>
        /// Sets the stack count display.
        /// Shows "xN" when count > 0, hides when 0.
        /// </summary>
        public void SetStack(int count)
        {
            if (_stackText == null) return;

            if (count > 0)
            {
                _stackText.text = $"x{count}";
                _stackText.enabled = true;
            }
            else
            {
                _stackText.text = "";
                _stackText.enabled = false;
            }
        }
    }
}