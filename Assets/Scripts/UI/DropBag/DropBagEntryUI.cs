using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// One entry row inside the Drop Bag list: [Icon] Name xQuantity.
    /// Plain data holder — DropBagUI binds the serialized references
    /// (from a prefab or from runtime-created children).
    /// </summary>
    public class DropBagEntryUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _qtyText;

        /// <summary>Bind runtime-created children (prefab path uses serialized fields instead).</summary>
        public void Bind(Image icon, TextMeshProUGUI nameText, TextMeshProUGUI qtyText)
        {
            _icon = icon;
            _nameText = nameText;
            _qtyText = qtyText;
        }

        public void Set(Items.ItemData itemData, string itemId, int quantity, Sprite fallbackSprite)
        {
            if (_icon != null)
                _icon.sprite = itemData != null && itemData.Icon != null ? itemData.Icon : fallbackSprite;
            if (_nameText != null)
                _nameText.text = itemData != null ? itemData.Name : itemId;
            if (_qtyText != null)
                _qtyText.text = $"x{quantity}";
        }
    }
}
