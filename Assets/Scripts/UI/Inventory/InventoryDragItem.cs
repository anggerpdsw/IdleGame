using TMPro;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI.Inventory
{
    /// <summary>
    /// Dragged item visual.
    /// </summary>
    public class InventoryDragItem : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private CanvasGroup _canvasGroup;

        public InventoryItem Item { get; private set; }

        /// <summary>Currently dragged item (set on drag start, cleared on destroy).</summary>
        public static InventoryItem DraggedItem { get; internal set; }

        public void Initialize(InventoryItem item)
        {
            Item = item;
            DraggedItem = item;
            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null) return;


            if (_iconImage != null && itemData.Icon != null)
            {
                _iconImage.sprite = itemData.Icon;
            }

            if (_quantityText != null)
            {
                _quantityText.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
            }

            if (_rarityBorder != null && itemData.ItemRarity != Rarity.None)
            {
                _rarityBorder.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(DraggedItem, Item))
                DraggedItem = null;
        }
    }
}