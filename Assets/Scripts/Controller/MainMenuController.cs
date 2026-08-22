using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.UI;
using TMPro;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Handles the Main Menu UI for tier selection.
    /// Allows the player to navigate between unlocked tiers using Prev/Next buttons
    /// and start the gameplay with the selected tier.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Tier UI Elements")]
        [Tooltip("Label that shows the currently selected tier (e.g., T3)")]
        [SerializeField] private TextMeshProUGUI _tierLabel;
        [Tooltip("Label that shows the wave progress of the selected tier (e.g., 145 / 350)")]
        [SerializeField] private TextMeshProUGUI _progressLabel;
        private int _selectedTier;
        
        [Header("Open Menu")]
        [SerializeField] private Button _vipButton;
        [SerializeField] private Button _cardButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _craftingButton;

        [Header("Game Start")]
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _startButton;

        [Header("Daily Reward")]
        [SerializeField] private Button _dailyButton;
        [SerializeField] private GameObject _dailyBadge;
        [SerializeField] private DailyRewardUI _dailyRewardPanelPrefab;

        [Header("Mission")]
        [SerializeField] private Button _missionButton;
        [SerializeField] private GameObject _missionBadge;

        private void Start()
        {
            InitializeTierSelection();
        }

        private void OnEnable()
        {
            SaveManager.OnSaveLoaded += OnSaveLoaded;

            if (_vipButton != null) _vipButton.onClick.AddListener(OnActiveVIP);
            if (_cardButton != null) _cardButton.onClick.AddListener(OnLoadCard);
            if (_inventoryButton != null) _inventoryButton.onClick.AddListener(OnLoadInventory);
            if (_craftingButton != null) _craftingButton.onClick.AddListener(OnLoadCrafting);
            if (_prevButton != null) _prevButton.onClick.AddListener(OnPrevClicked);
            if (_nextButton != null) _nextButton.onClick.AddListener(OnNextClicked);
            if (_startButton != null) _startButton.onClick.AddListener(OnStartGame);
            if (_dailyButton != null) _dailyButton.onClick.AddListener(OnShowDaily);
            DailyRewardManager.OnClaimableStateChanged += RefreshBadge;
            DailyRewardManager.OnInitialized += OnDailyRewardInitialized;
            if (DailyRewardManager.Instance != null && DailyRewardManager.Instance.Service != null)
                RefreshBadge(DailyRewardManager.Instance.Service.HasClaimableReward);
        }

        private void OnDisable()
        {
            if (_vipButton != null) _vipButton.onClick.RemoveListener(OnActiveVIP);
            if (_cardButton != null) _cardButton.onClick.RemoveListener(OnLoadCard);
            if (_inventoryButton != null) _inventoryButton.onClick.RemoveListener(OnLoadInventory);
            if (_craftingButton != null) _craftingButton.onClick.RemoveListener(OnLoadCrafting);
            if (_prevButton != null) _prevButton.onClick.RemoveListener(OnPrevClicked);
            if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
            if (_startButton != null) _startButton.onClick.RemoveListener(OnStartGame);
            if (_dailyButton != null) _dailyButton.onClick.RemoveListener(OnShowDaily);
            DailyRewardManager.OnClaimableStateChanged -= RefreshBadge;
            DailyRewardManager.OnInitialized -= OnDailyRewardInitialized;

            SaveManager.OnSaveLoaded -= OnSaveLoaded;
        }

        private void OnDailyRewardInitialized() => RefreshBadge(DailyRewardManager.Instance.Service.HasClaimableReward);

        private void OnSaveLoaded() => InitializeTierSelection();

        private void InitializeTierSelection()
        {
            _selectedTier = SaveManager.Instance.GetHighestUnlockedTier();
            RefreshUI();
        }

        private void RefreshBadge(bool visible) => _dailyBadge.SetActive(visible);

        private void RefreshUI()
        {
            if (_tierLabel != null) _tierLabel.SetText($"T {_selectedTier}");
            if (_progressLabel != null) _progressLabel.SetText($"W {SaveManager.Instance.GetHighestWave(_selectedTier)}");

            if (_prevButton != null) _prevButton.gameObject.SetActive(_selectedTier > 1);
            if (_nextButton != null) _nextButton.gameObject.SetActive(SaveManager.Instance.IsTierUnlocked(_selectedTier + 1));
        }

        public void OnPrevClicked()
        {
            if (_selectedTier <= 1) return;
            _selectedTier--;
            RefreshUI();
        }

        public void OnNextClicked()
        {
            if (!SaveManager.Instance.IsTierUnlocked(_selectedTier + 1)) return;
            _selectedTier++;
            RefreshUI();
        }

        public void OnStartGame() => GameManager.Instance.OpenGame(_selectedTier);
        public void OnLoadCard() => CardManager.Instance.OpenCollection();
        public void OnLoadInventory() => InventoryManager.Instance.OpenInventory();
        public void OnLoadCrafting() => CraftingManager.Instance.OpenCrafting();
        
        private void OnShowDaily() {
            UIManager.Instance.ShowPopup(_dailyRewardPanelPrefab);
        }

        private void OnActiveVIP()
        {
            const bool enabled = true;
            SaveManager.Instance.SetDaily(enabled);
            SaveManager.Instance.SetMaxSpeed(enabled);
            SaveManager.Instance.SetAutoCollect(enabled);
        }

    }
}