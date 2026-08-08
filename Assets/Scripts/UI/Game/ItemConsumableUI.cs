using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.UI.Game
{
    /// <summary>
    /// Single consumable slot on the HUD (the ItemConsumable prefab).
    /// Dumb renderer: knows nothing about the panel — sets icon + count,
    /// hides itself when the player owns 0, wires its Button click outward.
    /// Radial fill on the icon Image shows cooldown progress.
    /// </summary>
    public class ItemConsumableUI : MonoBehaviour
    {
        [Tooltip("Potion Image on this slot (root Image).")]
        [SerializeField] private Image _icon;
        [Tooltip("Count Text on this slot (child Count).")]
        [SerializeField] private TextMeshProUGUI _count;
        [Tooltip("Button on the slot root.")]
        [SerializeField] private Button _button;

        /// <summary>Item id this slot renders.</summary>
        public string ItemId { get; private set; }

        public void Initialize(string itemId)
        {
            ItemId = itemId;
            if (_icon == null) _icon = GetComponent<Image>();
            if (_count == null) _count = GetComponentInChildren<TextMeshProUGUI>(true);
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

        /// <summary>Renders the held quantity. Hides the slot when the player owns none.</summary>
        public void SetQuantity(int quantity)
        {
            var data = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItem(ItemId) : null;

            bool show = quantity > 0 && data != null;
            gameObject.SetActive(show);
            if (!show) return;

            if (_icon != null && data.Icon != null)
                _icon.sprite = data.Icon;
            if (_count != null)
                _count.text = quantity.ToString();
        }

        /// <summary>Sets cooldown radial fill (0 = ready, 1 = full cooldown). Also blocks interaction during cooldown.</summary>
        public void SetCooldown(float fillAmount)
        {
            if (_icon != null)
                _icon.fillAmount = 1f - fillAmount; // Invert: 1 = ready (full), 0 = on cooldown (empty)
            if (_button != null)
                _button.interactable = fillAmount <= 0f;
        }
    }
}