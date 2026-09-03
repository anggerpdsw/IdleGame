using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.UI.Tooltip;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// UI orchestration for the crafting scene. Owns presentation state only:
    /// recipe list, selection, quantity, affordability presentation, craft submission,
    /// and job event display. All business logic lives in CraftService.
    /// </summary>
    public class CraftingUIController : MonoBehaviour
    {
        [Header("Recipe List")]
        [SerializeField] private RectTransform _recipeContent;
        [SerializeField] private CraftingRecipeEntry _recipeEntryPrefabs;

        [Header("Category Tabs")]
        [SerializeField] private Button _equipmentTabButton;
        [SerializeField] private Button _potionTabButton;
        [SerializeField] private Image _equipmentTabSelection;
        [SerializeField] private Image _potionTabSelection;

        [Header("Equipment Sub-Filters")]
        [SerializeField] private Button _categoryAllEquipmentButton;
        [SerializeField] private Button[] _categoryEquipmentButtons;
        [SerializeField] private Image _categoryAllEquipmentSelection;
        [SerializeField] private Image[] _categoryEquipmentSelections;
        [SerializeField] private GameObject _equipmentFilterPanel;

        [Header("Potion Sub-Filters")]
        [SerializeField] private Button _categoryAllPotionButton;
        [SerializeField] private Button[] _categoryPotionButtons;
        [SerializeField] private Image _categoryAllPotionSelection;
        [SerializeField] private Image[] _categoryPotionSelections;
        [SerializeField] private GameObject _potionFilterPanel;

        [Header("Detail Panel")]
        [SerializeField] private Image _resultIcon;
        [SerializeField] private TextMeshProUGUI _resultName;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _rarityText;

        [Header("Materials")]
        [SerializeField] private RectTransform _materialList;
        [SerializeField] private GameObject _materialRowTemplate;

        [Header("Controls & Economy")]
        [SerializeField] private TextMeshProUGUI _goldCostText;
        [SerializeField] private TextMeshProUGUI _gemCostText;
        [SerializeField] private TextMeshProUGUI _quantityText;
        [SerializeField] private Button _plusButton;
        [SerializeField] private Button _minusButton;
        [SerializeField] private Button _craftButton;

        [Header("Job List")]
        [SerializeField] private RectTransform _jobList;
        [SerializeField] private JobEntryUI _jobEntryPrefab;

        private enum CategoryTab { Equipment, Potion }

        private CategoryTab _currentTab = CategoryTab.Equipment;
        private string _selectedRecipeId;
        private int _quantity = 1;
        private EquipmentType _currentCategoryEquipmentFilter = EquipmentType.None;
        private PotionType _currentCategoryPotionFilter = PotionType.None;
        private readonly List<CraftingRecipeEntry> _entries = new();
        private readonly List<GameObject> _materialRows = new();
        private readonly List<JobEntryUI> _jobEntries = new();

        #region Pure decision logic (EditMode-testable)

        /// <summary>Quantity floor is 1. No hardcoded cap — backend CanCraft is authoritative.</summary>
        public static int ClampQuantity(int q) => Math.Max(1, q);

        /// <summary>Currency affordability presentation check. Null preview = not affordable.</summary>
        public static bool CanAffordCurrency(CurrencySnapshot? cost, long gold, long gem)
        {
            if (!cost.HasValue) return false;
            return gold >= cost.Value.GoldSnapshot && gem >= cost.Value.GemSnapshot;
        }

        /// <summary>Material affordability presentation check. Pure — no state access.</summary>
        public static bool CanAffordMaterials(IngredientCost[] reqs, Func<string, int> getOwned)
        {
            if (reqs == null) return true;
            foreach (var r in reqs)
                if (getOwned(r.ItemId) < r.Count) return false;
            return true;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            ValidateReferences();
            if (_recipeEntryPrefabs != null) _recipeEntryPrefabs.gameObject.SetActive(false);
            if (_materialRowTemplate != null) _materialRowTemplate.SetActive(false);
        }

        private void Start()
        {
            Bind();
        }

        private void Bind()
        {
            var svc = CraftingManager.Instance;
            if (svc == null)
            {
                Debug.LogError("[CraftingUIController] CraftingManager.Instance is null — scene cannot operate.");
                return;
            }
            svc.OnJobStarted      += OnJobStartedForList;
            svc.OnJobProgress     += OnJobProgressForList;
            svc.OnJobReadyToClaim += OnJobReadyToClaimForList;
            svc.OnJobClaimed      += OnJobClaimedForList;

            if (_plusButton != null) _plusButton.onClick.AddListener(OnPlusClicked);
            if (_minusButton != null) _minusButton.onClick.AddListener(OnMinusClicked);
            if (_craftButton != null) _craftButton.onClick.AddListener(OnCraftClicked);

            BindCategoryTabs();
            BindCategoryButtons();
            PopulateRecipeList();
            PopulateJobList();
            UpdateTabSelection();
            UpdateCategorySelection();
        }

        private void BindCategoryTabs()
        {
            if (_equipmentTabButton != null)
                _equipmentTabButton.onClick.AddListener(() => OnTabChanged(CategoryTab.Equipment));
            if (_potionTabButton != null)
                _potionTabButton.onClick.AddListener(() => OnTabChanged(CategoryTab.Potion));
        }

        private void BindCategoryButtons()
        {
            // Equipment All
            if (_categoryAllEquipmentButton != null)
                _categoryAllEquipmentButton.onClick.AddListener(
                    () => OnCategoryEquipmentFilterChanged(EquipmentType.None)
                );

            // Equipment sub-filters
            if (_categoryEquipmentButtons != null)
            {
                for (int i = 0; i < _categoryEquipmentButtons.Length; i++)
                {
                    var button = _categoryEquipmentButtons[i];
                    if (button == null) continue;
                    EquipmentType category = (EquipmentType)(i + 1);
                    button.onClick.AddListener(() => OnCategoryEquipmentFilterChanged(category));
                }
            }

            // Potion All
            if (_categoryAllPotionButton != null)
                _categoryAllPotionButton.onClick.AddListener(
                    () => OnCategoryPotionFilterChanged(PotionType.None)
                );

            // Potion sub-filters
            if (_categoryPotionButtons != null)
            {
                for (int i = 0; i < _categoryPotionButtons.Length; i++)
                {
                    var button = _categoryPotionButtons[i];
                    if (button == null) continue;
                    PotionType category = (PotionType)(i + 1);
                    button.onClick.AddListener(() => OnCategoryPotionFilterChanged(category));
                }
            }
        }

        private void OnTabChanged(CategoryTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;

            // Reset sub-filters when switching tabs
            _currentCategoryEquipmentFilter = EquipmentType.None;
            _currentCategoryPotionFilter = PotionType.None;

            UpdateTabSelection();
            UpdateCategorySelection();
            PopulateRecipeList();
        }

        private void OnCategoryEquipmentFilterChanged(EquipmentType category)
        {
            _currentCategoryEquipmentFilter = category;
            UpdateCategorySelection();
            PopulateRecipeList();
        }

        private void OnCategoryPotionFilterChanged(PotionType category)
        {
            _currentCategoryPotionFilter = category;
            UpdateCategorySelection();
            PopulateRecipeList();
        }

        private void UpdateTabSelection()
        {
            if (_equipmentTabSelection != null)
                _equipmentTabSelection.gameObject.SetActive(_currentTab == CategoryTab.Equipment);
            if (_potionTabSelection != null)
                _potionTabSelection.gameObject.SetActive(_currentTab == CategoryTab.Potion);

            // Show/hide filter panels
            if (_equipmentFilterPanel != null)
                _equipmentFilterPanel.SetActive(_currentTab == CategoryTab.Equipment);
            if (_potionFilterPanel != null)
                _potionFilterPanel.SetActive(_currentTab == CategoryTab.Potion);
        }

        private void UpdateCategorySelection()
        {
            // Equipment sub-filter selection
            if (_categoryAllEquipmentSelection != null)
            {
                _categoryAllEquipmentSelection.gameObject.SetActive(
                    _currentCategoryEquipmentFilter == EquipmentType.None
                );
            }
            if (_categoryEquipmentSelections != null)
            {
                for (int i = 0; i < _categoryEquipmentSelections.Length; i++)
                {
                    var selection = _categoryEquipmentSelections[i];
                    if (selection == null) continue;
                    EquipmentType category = (EquipmentType)(i + 1);
                    selection.gameObject.SetActive(_currentCategoryEquipmentFilter == category);
                }
            }

            // Potion sub-filter selection
            if (_categoryAllPotionSelection != null)
            {
                _categoryAllPotionSelection.gameObject.SetActive(
                    _currentCategoryPotionFilter == PotionType.None
                );
            }
            if (_categoryPotionSelections != null)
            {
                for (int i = 0; i < _categoryPotionSelections.Length; i++)
                {
                    var selection = _categoryPotionSelections[i];
                    if (selection == null) continue;
                    PotionType category = (PotionType)(i + 1);
                    selection.gameObject.SetActive(_currentCategoryPotionFilter == category);
                }
            }
        }

        private void OnDisable()
        {
            var svc = CraftingManager.Instance;
            if (svc == null) return;
            svc.OnJobStarted -= OnJobStartedForList;
            svc.OnJobProgress -= OnJobProgressForList;
            svc.OnJobReadyToClaim -= OnJobReadyToClaimForList;
            svc.OnJobClaimed -= OnJobClaimedForList;

            if (_plusButton != null) _plusButton.onClick.RemoveListener(OnPlusClicked);
            if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinusClicked);
            if (_craftButton != null) _craftButton.onClick.RemoveListener(OnCraftClicked);
            if (_equipmentTabButton != null) _equipmentTabButton.onClick.RemoveAllListeners();
            if (_potionTabButton != null) _potionTabButton.onClick.RemoveAllListeners();

            ClearRecipeEntries();
            ClearMaterialRows();
            ClearJobEntries();
        }

        private void ValidateReferences()
        {
            if (_recipeContent == null) Debug.LogError("[CraftingUIController] Missing required reference: _recipeContent");
            if (_recipeEntryPrefabs == null) Debug.LogError("[CraftingUIController] Missing required reference: _recipeEntryPrefabs");
            if (_resultIcon == null) Debug.LogError("[CraftingUIController] Missing required reference: _resultIcon");
            if (_resultName == null) Debug.LogError("[CraftingUIController] Missing required reference: _resultName");
            if (_descriptionText == null) Debug.LogError("[CraftingUIController] Missing required reference: _descriptionText");
            if (_rarityText == null) Debug.LogError("[CraftingUIController] Missing required reference: _rarityText");
            if (_materialList == null) Debug.LogError("[CraftingUIController] Missing required reference: _materialList");
            if (_materialRowTemplate == null) Debug.LogError("[CraftingUIController] Missing required reference: _materialRowTemplate");
            if (_goldCostText == null) Debug.LogError("[CraftingUIController] Missing required reference: _goldCostText");
            if (_gemCostText == null) Debug.LogError("[CraftingUIController] Missing required reference: _gemCostText");
            if (_quantityText == null) Debug.LogError("[CraftingUIController] Missing required reference: _quantityText");
            if (_plusButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _plusButton");
            if (_minusButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _minusButton");
            if (_craftButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _craftButton");

            // Tab buttons
            if (_equipmentTabButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _equipmentTabButton");
            if (_potionTabButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _potionTabButton");
            if (_equipmentTabSelection == null) Debug.LogError("[CraftingUIController] Missing required reference: _equipmentTabSelection");
            if (_potionTabSelection == null) Debug.LogError("[CraftingUIController] Missing required reference: _potionTabSelection");

            // Equipment filters
            if (_categoryAllEquipmentButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryAllEquipmentButton");
            if (_categoryEquipmentButtons == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryEquipmentButtons");
            if (_categoryAllEquipmentSelection == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryAllEquipmentSelection");
            if (_categoryEquipmentSelections == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryEquipmentSelections");
            if (_equipmentFilterPanel == null) Debug.LogError("[CraftingUIController] Missing required reference: _equipmentFilterPanel");

            // Potion filters
            if (_categoryAllPotionButton == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryAllPotionButton");
            if (_categoryPotionButtons == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryPotionButtons");
            if (_categoryAllPotionSelection == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryAllPotionSelection");
            if (_categoryPotionSelections == null) Debug.LogError("[CraftingUIController] Missing required reference: _categoryPotionSelections");
            if (_potionFilterPanel == null) Debug.LogError("[CraftingUIController] Missing required reference: _potionFilterPanel");
        }

        #endregion

        #region Recipe list

        private void PopulateRecipeList()
        {
            ClearRecipeEntries();
            var recipes = CraftingManager.Instance.GetKnownRecipes();
            if (recipes == null) return;

            IEnumerable<CraftRecipeData> filteredRecipes;

            if (_currentTab == CategoryTab.Equipment)
            {
                // Show equipment recipes only, filtered by equipment sub-filter
                filteredRecipes = recipes.Where(r =>
                    r != null &&
                    !string.IsNullOrEmpty(r.RecipeId) &&
                    r.EquipmentType != EquipmentType.None &&
                    (_currentCategoryEquipmentFilter == EquipmentType.None || r.EquipmentType == _currentCategoryEquipmentFilter)
                );
            }
            else
            {
                // Show potion recipes only, filtered by potion sub-filter
                filteredRecipes = recipes.Where(r =>
                    r != null &&
                    !string.IsNullOrEmpty(r.RecipeId) &&
                    r.PotionType != PotionType.None &&
                    (_currentCategoryPotionFilter == PotionType.None || r.PotionType == _currentCategoryPotionFilter)
                );
            }

            // Sort by rarity (highest first: Divine=6, Mythic=5, Legendary=4, Epic=3, Rare=2, Common=1)
            var sortedRecipes = filteredRecipes.OrderByDescending(r => r.Rarity).ToList();
            foreach (var recipe in sortedRecipes)
            {
                var entry = Instantiate(_recipeEntryPrefabs, _recipeContent);
                entry.gameObject.SetActive(true);
                entry.Initialize(recipe.RecipeId, ResolveRecipeIcon(recipe), (Rarity)recipe.Rarity, this);
                _entries.Add(entry);
                UpdateEntryAffordability(entry, recipe);
            }
        }

        private void UpdateEntryAffordability(CraftingRecipeEntry entry, CraftRecipeData recipe)
        {
            if (entry == null || recipe == null) return;

            var reqs = CraftingManager.Instance.GetRecipeMaterialPreview(recipe.RecipeId, 1);
            if (reqs == null || reqs.Length == 0) return;

            bool canAffordMaterials = true;
            if (InventoryService.Instance != null)
            {
                foreach (var req in reqs)
                {
                    int owned = InventoryService.Instance.GetTotalQuantity(req.ItemId);
                    if (owned < req.Count)
                    {
                        canAffordMaterials = false;
                        break;
                    }
                }
            }
            else
            {
                canAffordMaterials = false;
            }

            entry.SetAffordable(canAffordMaterials, GameColors.empty, GameColors.white);
        }

        private Sprite ResolveRecipeIcon(CraftRecipeData recipe)
        {
            if (recipe == null) return null;

            // Potion recipes
            if (recipe.PotionType != PotionType.None)
            {
                string potionTypeName = recipe.PotionType switch
                {
                    PotionType.Health => "hp",
                    PotionType.Mana => "mp",
                    PotionType.Stamina => "sp",
                    PotionType.DebuffCleanse => "ap",
                    _ => "??"
                };
                return ItemResources.GetItemSource(
                    $"Potion/potion_r{recipe.Rarity}/{potionTypeName}"
                );
            }

            // Equipment recipes
            if (recipe.EquipmentType != EquipmentType.None)
            {
                string type = recipe.EquipmentType.ToString();
                string name = Utilityku.ToItemId(recipe.DisplayName);
                return ItemResources.GetItemSource($"Equipment/{type}/{name}");
            }

            return null;
        }

        public void OnRecipeSelected(string recipeId)
        {
            _selectedRecipeId = recipeId;
            _quantity = 1;
            RefreshDetail();
        }

        #endregion

        #region Detail

        private void RefreshDetail()
        {
            var svc = CraftingManager.Instance;
            if (svc == null || string.IsNullOrEmpty(_selectedRecipeId) || !svc.TryGetRecipe(_selectedRecipeId, out var recipe))
            {
                ClearDetail();
                return;
            }

            if (_resultName != null) _resultName.text = recipe.DisplayName;
            if (_descriptionText != null) _descriptionText.text = recipe.Description;
            if (_rarityText != null) _rarityText.text = ((Rarity)recipe.Rarity).ToString();

            var icon = ResolveRecipeIcon(recipe);
            if (_resultIcon != null) _resultIcon.sprite = icon;

            RebuildMaterials();
            RefreshCost();
            RefreshControls();
        }

        private void ClearDetail()
        {
            if (_resultIcon != null) _resultIcon.sprite = null;
            if (_resultName != null) _resultName.text = "";
            if (_descriptionText != null) _descriptionText.text = "";
            if (_rarityText != null) _rarityText.text = "";
            if (_goldCostText != null) _goldCostText.text = "0";
            if (_gemCostText != null) _gemCostText.text = "0";
            ClearMaterialRows();
        }

        #endregion

        #region Materials + cost

        private void RebuildMaterials()
        {
            ClearMaterialRows();
            var reqs = CraftingManager.Instance.GetRecipeMaterialPreview(_selectedRecipeId, _quantity);
            if (reqs == null || reqs.Length == 0) return;

            foreach (var req in reqs)
            {
                string _id = req.ItemId;
                var row = Instantiate(_materialRowTemplate, _materialList);
                row.SetActive(true);

                // Add tooltip component for hover info
                var tooltip = row.AddComponent<MaterialTooltip>();
                tooltip.ItemId = _id;

                var icon = row.GetComponentInChildren<Image>();
                if (icon != null)
                {   
                    Sprite showMaterial= ItemResources.GetItemSource($"Material/{_id}") ?? ItemResources.GetItemSource($"Herb/{_id}") ?? ItemResources.GetItemSource($"Potion/base/{_id}");
                    icon.sprite = showMaterial;
                }
                var text = row.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    int owned = InventoryService.Instance != null ? InventoryService.Instance.GetTotalQuantity(_id) : 0;
                    text.text = $"{owned} / {req.Count}";
                    text.color = owned >= req.Count ? GameColors.white : GameColors.red;
                }
                _materialRows.Add(row);
            }
        }

        private void RefreshCost()
        {
            var svc = CraftingManager.Instance;
            if (svc == null || string.IsNullOrEmpty(_selectedRecipeId))
            {
                if (_goldCostText != null) _goldCostText.text = "0";
                if (_gemCostText != null) _gemCostText.text = "0";
                return;
            }
            var cost = svc.GetRecipeCostPreview(_selectedRecipeId, _quantity);
            if (_goldCostText != null) _goldCostText.text = cost.HasValue ? cost.Value.GoldSnapshot.ToString() : "0";
            if (_gemCostText != null) _gemCostText.text = cost.HasValue ? cost.Value.GemSnapshot.ToString() : "0";
        }

        private void RefreshControls()
        {
            if (_quantityText != null) _quantityText.text = _quantity.ToString();
            if (_craftButton == null) return;

            var svc = CraftingManager.Instance;
            if (svc == null || string.IsNullOrEmpty(_selectedRecipeId))
            {
                _craftButton.interactable = false;
                return;
            }

            long gold = EconomyManager.Instance != null ? EconomyManager.Instance.GetCurrency(CurrencyType.Gold) : 0;
            long gem = EconomyManager.Instance != null ? EconomyManager.Instance.GetCurrency(CurrencyType.Gem) : 0;
            var cost = svc.GetRecipeCostPreview(_selectedRecipeId, _quantity);
            var reqs = svc.GetRecipeMaterialPreview(_selectedRecipeId, _quantity);
            static int owned(string id) => InventoryService.Instance != null ? InventoryService.Instance.GetTotalQuantity(id) : 0;

            bool canAfford = CanAffordCurrency(cost, gold, gem) && CanAffordMaterials(reqs, owned);

            // Check concurrent slot availability
            bool hasConcurrentSlot = svc.GetQueueService() != null && svc.GetQueueService().HasAvailableSlot;

            _craftButton.interactable = canAfford && hasConcurrentSlot;
        }

        #endregion

        #region Quantity

        private void OnPlusClicked()
        {
            _quantity = ClampQuantity(_quantity + 1);
            RefreshDetail();
        }

        private void OnMinusClicked()
        {
            _quantity = ClampQuantity(_quantity - 1);
            RefreshDetail();
        }

        #endregion

        #region Craft submission

        private void OnCraftClicked()
        {
            var svc = CraftingManager.Instance;
            if (svc == null || string.IsNullOrEmpty(_selectedRecipeId)) return;

            // Determine craft type from selected recipe
            CraftType craftType = CraftType.Equipment;
            var recipe = svc.TryGetRecipe(_selectedRecipeId, out var r) ? r : null;
            if (recipe != null && recipe.PotionType != PotionType.None)
                craftType = CraftType.Potion;

            var jobId = svc.StartCraft(craftType, _selectedRecipeId, _quantity);
            if (!string.IsNullOrEmpty(jobId))
            {
                PopulateJobList();
            }
            else
            {
                RefreshControls();
            }
            RebuildMaterials();
        }

        #endregion

        #region Job events

        private void OnJobStartedForList(string jobId)
        {
            RefreshJobEntry(jobId);
        }

        private void OnJobProgressForList(string jobId, float progress)
        {
            var entry = _jobEntries.FirstOrDefault(e => e.JobId == jobId);
            if (entry != null)
            {
                entry.SetProgress(progress);
            }
        }

        private void OnJobReadyToClaimForList(string jobId)
        {
            var entry = _jobEntries.FirstOrDefault(e => e.JobId == jobId);
            if (entry != null)
            {
                entry.SetProgress(1f);
                entry.SetStatus(CraftJobStatus.Complete);
                entry.SetClaimVisible(true);
            }
            else
            {
                RefreshJobEntry(jobId);
            }
        }

        private void OnJobClaimedForList(string jobId, InventoryItem[] items)
        {
            PopulateJobList();
            RebuildMaterials();
            RefreshCost();
            RefreshControls();
        }

        private void RefreshJobEntry(string jobId)
        {
            var svc = CraftingManager.Instance;
            if (svc == null) return;

            var job = svc.GetQueueService()?.GetJob(jobId);
            if (job == null) return;

            var entry = _jobEntries.FirstOrDefault(e => e.JobId == jobId);

            if (entry == null)
            {
                PopulateJobList();
                return;
            }

            entry.SetProgress(job.Progress);
            entry.SetStatus(job.Status);

            if (job.IsReadyToClaim)
            {
                entry.SetClaimVisible(true);
            }
        }

        #endregion

        #region Job List

        private void PopulateJobList()
        {
            ClearJobEntries();
            var svc = CraftingManager.Instance;
            if (svc == null) return;

            var allJobs = svc.GetAllJobs();
            var orderedJobs = allJobs
                .OrderByDescending(j => j.IsCrafting ? 0 : (j.IsReadyToClaim ? 1 : 2))
                .ThenBy(j => j.EndTimeUtc == 0 ? 0 : j.EndTimeUtc);

            foreach (var job in orderedJobs)
            {
                var entryObj = Instantiate(_jobEntryPrefab, _jobList);
                entryObj.gameObject.SetActive(true);
                if (!entryObj.TryGetComponent<JobEntryUI>(out var entry)) continue;

                Sprite icon = null;
                Rarity recipeRarity = Rarity.None;
                string recipeName = job.RecipeId;
                if (svc.TryGetRecipe(recipeName, out var recipe))
                {
                    icon = ResolveRecipeIcon(recipe);
                    recipeName = recipe.DisplayName;
                    recipeRarity = (Rarity)recipe.Rarity;
                }

                entry.Initialize(job.JobId, icon, recipeRarity, recipeName, job.Progress, job.Status, OnClaimJob);
                _jobEntries.Add(entry);
            }
        }

        private void OnClaimJob(string jobId)
        {
            var svc = CraftingManager.Instance;
            if (svc == null)
            {
                Debug.LogError("[CraftingUI] CraftingManager.Instance is null.");
                return;
            }

            Debug.Log($"[CraftingUIController] Claim requested | JobId={jobId}");
            svc.ClaimJob(jobId);
        }

        private void ClearJobEntries()
        {
            if (_jobList != null)
            {
                for (int i = _jobList.childCount - 1; i >= 0; i--)
                    Destroy(_jobList.GetChild(i).gameObject);
            }
            _jobEntries.Clear();
        }

        #endregion

        #region Cleanup
        private void ClearRecipeEntries() => ClearChildren(_recipeContent, _entries);
        private void ClearMaterialRows() => ClearChildren(_materialList, _materialRows);
        private void ClearChildren<T>(Transform container, List<T> items)
        {
            if (container != null)
                for (int i = container.childCount - 1; i >= 0; i--)
                    Destroy(container.GetChild(i).gameObject);
            items.Clear();
        }
        #endregion
    }
}