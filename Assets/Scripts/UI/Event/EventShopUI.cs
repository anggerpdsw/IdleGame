using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Events;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Event Shop UI.
    /// Displays shop items, handles purchases via EventShopService.
    /// </summary>
    public class EventShopUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _shopRoot;
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private GameObject _itemPrefab;
        [SerializeField] private TextMeshProUGUI _currencyText;

        private void Awake()
        {
            if (_shopRoot != null) _shopRoot.SetActive(false);
        }

        public void Show()
        {
            if (_shopRoot != null) _shopRoot.SetActive(true);
            RefreshShop();
        }

        public void Hide()
        {
            if (_shopRoot != null) _shopRoot.SetActive(false);
        }

        private void RefreshShop()
        {
            // Clear existing items
            if (_itemContainer != null)
            {
                foreach (Transform child in _itemContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // Get shop items
            var items = EventShopService.GetShopItems();
            if (items == null) return;

            // Create item slots
            foreach (var item in items)
            {
                if (_itemPrefab != null && _itemContainer != null)
                {
                    var itemObj = Instantiate(_itemPrefab, _itemContainer);
                    var slot = itemObj.GetComponent<EventShopItemUI>();
                    if (slot != null)
                    {
                        slot.Initialize(item.itemId, item.price, this);
                    }
                }
            }

            // Update currency display
            UpdateCurrency();
        }

        public void OnPurchaseClicked(string itemId, long price)
        {
            bool success = EventShopService.PurchaseItem(itemId, price);
            if (success)
            {
                RefreshShop(); // Refresh to show purchased state
            }
        }

        private void UpdateCurrency()
        {
            if (_currencyText == null) return;

            var eventService = ServiceLocator.EventService;
            if (eventService == null) return;

            var saveData = (eventService as EventService)?.GetSaveData();
            if (saveData != null)
            {
                _currencyText.text = $"{saveData.eventCurrency}";
            }
        }
    }

    /// <summary>
    /// Individual shop item slot.
    /// </summary>
    public class EventShopItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private GameObject _purchasedIndicator;

        private string _itemId;
        private long _price;
        private EventShopUI _shopUI;

        public void Initialize(string itemId, long price, EventShopUI shopUI)
        {
            _itemId = itemId;
            _price = price;
            _shopUI = shopUI;

            if (_nameText != null) _nameText.text = itemId;
            if (_priceText != null) _priceText.text = price.ToString();

            bool purchased = EventShopService.IsPurchased(itemId);
            if (_buyButton != null)
            {
                _buyButton.interactable = !purchased;
                _buyButton.onClick.RemoveAllListeners();
                _buyButton.onClick.AddListener(OnBuyClicked);
            }

            if (_purchasedIndicator != null)
                _purchasedIndicator.SetActive(purchased);
        }

        private void OnBuyClicked()
        {
            _shopUI?.OnPurchaseClicked(_itemId, _price);
        }
    }
}
