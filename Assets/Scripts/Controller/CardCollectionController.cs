using UnityEngine;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine.UI;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    public class CardCollectionController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Transform _rollParent;
        [SerializeField] private CardRollButtonUI _rollPrefab;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _addSlotButton;
        [SerializeField] private TextMeshProUGUI _slotMaxCount;
        [SerializeField] private TextMeshProUGUI _slotMax2;
        [SerializeField] private TextMeshProUGUI _nextSlotCostGem;
        
        private readonly int[] _rollAmounts = { 1, 10, 100 };

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromCardCollection();
        public void OnAddSlot() => CardManager.Instance.ExpandSlot();

        private void Start()
        {
            RefreshSlotInfo();
            CreateRollButtons();
        }

        private void RefreshSlotInfo()
        {
            _slotMaxCount.text = 
                $"{CardManager.Instance.EquippedCardCount}/{CardManager.Instance.UnlockedSlotCount}";
            _slotMax2.text = _slotMaxCount.text;
            
            _nextSlotCostGem.text = 
                CardManager.Instance.UnlockedSlotCount == CardManager.Instance.MaxSlots 
                ? "MAX" 
                : $"{CardEquipmentService.Instance.NextSlotCostGem}";
        }

        private void CreateRollButtons()
        {
            foreach (Transform child in _rollParent) Destroy(child.gameObject);
            long cardRoll = CardManager.Instance.CardRollCount;
            foreach (int amount in _rollAmounts)
            {
                var ui = Instantiate(_rollPrefab, _rollParent);
                bool useCardRoll = cardRoll >= amount;
                int gemCost = CardRollService.CalculateRollGemCost(amount);
                ui.Setup(amount, gemCost, useCardRoll, () => CardManager.Instance.Roll(amount));
            }
        }

        private void OnEnable()
        {
            _backButton?.onClick.AddListener(OnBack);
            _addSlotButton?.onClick.AddListener(OnAddSlot);
            
            CardManager.OnInventoryChanged += CreateRollButtons;
            CardManager.OnEquipmentChanged += RefreshSlotInfo;
            CardManager.OnSlotExpanded += RefreshSlotInfo;
        }

        private void OnDisable()
        {
            _backButton?.onClick.RemoveListener(OnBack);
            _addSlotButton?.onClick.RemoveListener(OnAddSlot);
            
            CardManager.OnInventoryChanged -= CreateRollButtons;
            CardManager.OnEquipmentChanged -= RefreshSlotInfo;
            CardManager.OnSlotExpanded -= RefreshSlotInfo;
        }

    }
}
