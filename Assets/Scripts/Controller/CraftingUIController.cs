using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Items;

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
        [SerializeField] private GameObject _recipeEntryTemplate;

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
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _feedbackText;

        private string _selectedRecipeId;
        private int _quantity = 1;
        private string _currentJobId;
        private readonly List<GameObject> _entries = new();
        private readonly List<GameObject> _materialRows = new();

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
            if (_recipeEntryTemplate != null) _recipeEntryTemplate.SetActive(false);
            if (_materialRowTemplate != null) _materialRowTemplate.SetActive(false);
            if (_feedbackText != null) _feedbackText.text = "";
        }

        private void OnEnable()
        {
            var svc = CraftingManager.Instance;
            if (svc == null)
            {
                Debug.LogError("[CraftingUIController] CraftingManager.Instance is null — scene cannot operate.");
                return;
            }
            svc.OnCraftStarted += OnCraftStarted;
            svc.OnCraftProgress += OnCraftProgress;
            svc.OnCraftCompleted += OnCraftCompleted;
            svc.OnCraftResult += OnCraftResult;
            svc.OnCraftFailed += OnCraftFailed;
            svc.OnCraftCancelled += OnCraftCancelled;

            if (_plusButton != null) _plusButton.onClick.AddListener(OnPlusClicked);
            if (_minusButton != null) _minusButton.onClick.AddListener(OnMinusClicked);
            if (_craftButton != null) _craftButton.onClick.AddListener(OnCraftClicked);

            PopulateRecipeList();
        }

        private void OnDisable()
        {
            var svc = CraftingManager.Instance;
            if (svc == null) return;
            svc.OnCraftStarted -= OnCraftStarted;
            svc.OnCraftProgress -= OnCraftProgress;
            svc.OnCraftCompleted -= OnCraftCompleted;
            svc.OnCraftResult -= OnCraftResult;
            svc.OnCraftFailed -= OnCraftFailed;
            svc.OnCraftCancelled -= OnCraftCancelled;

            if (_plusButton != null) _plusButton.onClick.RemoveListener(OnPlusClicked);
            if (_minusButton != null) _minusButton.onClick.RemoveListener(OnMinusClicked);
            if (_craftButton != null) _craftButton.onClick.RemoveListener(OnCraftClicked);

            ClearRecipeEntries();
            ClearMaterialRows();
        }

        private void ValidateReferences()
        {
            if (_recipeContent == null) Debug.LogError("[CraftingUIController] Missing required reference: _recipeContent");
            if (_recipeEntryTemplate == null) Debug.LogError("[CraftingUIController] Missing required reference: _recipeEntryTemplate");
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
            if (_progressSlider == null) Debug.LogError("[CraftingUIController] Missing required reference: _progressSlider");
            if (_feedbackText == null) Debug.LogError("[CraftingUIController] Missing required reference: _feedbackText");
        }

        #endregion

        #region Recipe list

        private void PopulateRecipeList()
        {
            ClearRecipeEntries();
            var recipes = CraftingManager.Instance.GetKnownRecipes();
            if (recipes == null) return;

            foreach (var recipe in recipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.RecipeId)) continue;
                var entry = Instantiate(_recipeEntryTemplate, _recipeContent);
                entry.SetActive(true);
                if (_recipeEntryTemplate.GetComponent<CraftingRecipeEntry>() != null)
                {
                    // Template already carries the entry component; re-init the clone
                    entry.GetComponent<CraftingRecipeEntry>().Initialize(recipe.RecipeId, recipe.DisplayName, ResolveRecipeIcon(recipe), this);
                }
                else
                {
                    var component = entry.AddComponent<CraftingRecipeEntry>();
                    component.Initialize(recipe.RecipeId, recipe.DisplayName, ResolveRecipeIcon(recipe), this);
                }
                _entries.Add(entry);
            }
        }

        private Sprite ResolveRecipeIcon(CraftRecipeData recipe)
        {
            if (recipe.GuaranteedResult == null || string.IsNullOrEmpty(recipe.GuaranteedResult.ItemId)) return null;
            if (ItemDatabase.Instance == null) return null;
            var itemData = ItemDatabase.Instance.GetItem(recipe.GuaranteedResult.ItemId);
            if (itemData == null || string.IsNullOrEmpty(itemData.IconKey)) return null;
            return ItemResources.GetItemSource(itemData.IconKey);
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
                var row = Instantiate(_materialRowTemplate, _materialList);
                row.SetActive(true);
                var text = row.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    int owned = InventoryService.Instance != null ? InventoryService.Instance.GetTotalQuantity(req.ItemId) : 0;
                    text.text = $"{req.ItemId}: {owned} / {req.Count}";
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

            bool busy = !string.IsNullOrEmpty(_currentJobId);
            bool hasRecipe = !string.IsNullOrEmpty(_selectedRecipeId);
            var svc = CraftingManager.Instance;
            if (svc == null || !hasRecipe)
            {
                _craftButton.interactable = false;
                return;
            }

            long gold = EconomyManager.Instance != null ? EconomyManager.Instance.GetCurrency(CurrencyType.Gold) : 0;
            long gem = EconomyManager.Instance != null ? EconomyManager.Instance.GetCurrency(CurrencyType.Gem) : 0;
            var cost = svc.GetRecipeCostPreview(_selectedRecipeId, _quantity);
            var reqs = svc.GetRecipeMaterialPreview(_selectedRecipeId, _quantity);
            int totalQuantity = 0;
            if (InventoryService.Instance != null)
                foreach (var r in reqs)
                    totalQuantity += InventoryService.Instance.GetTotalQuantity(r.ItemId);
            static int owned(string id) => InventoryService.Instance != null ? InventoryService.Instance.GetTotalQuantity(id) : 0;

            bool canAfford = CanAffordCurrency(cost, gold, gem) && CanAffordMaterials(reqs, owned);
            _craftButton.interactable = !busy && canAfford;
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

            var jobId = svc.StartCraft(_selectedRecipeId, _quantity);
            if (!string.IsNullOrEmpty(jobId))
            {
                _currentJobId = jobId;
                if (_craftButton != null) _craftButton.interactable = false;
                if (_progressSlider != null) _progressSlider.value = 0f;
                if (_feedbackText != null) _feedbackText.text = "Crafting...";
            }
            else
            {
                if (_feedbackText != null) _feedbackText.text = "Cannot craft: " + (CanCraftReason() ?? "unknown");
                RefreshControls();
            }
        }

        private string CanCraftReason()
        {
            var svc = CraftingManager.Instance;
            if (svc == null || string.IsNullOrEmpty(_selectedRecipeId)) return "no recipe";
            var v = svc.CanCraft(_selectedRecipeId, _quantity);
            return v.IsSuccess ? null : v.Reason;
        }

        #endregion

        #region Job events

        /// <summary>
        /// NOTE: OnCraftCompleted carries (recipeId, success) — no jobId.
        /// This UI tracks a single job (_currentJobId). Progress/failed/cancelled are
        /// jobId-filtered; completion is accepted for the tracked job only.
        /// Multi-job UI requires OnCraftCompleted to include jobId.
        /// </summary>
        private void OnCraftCompleted(string recipeId, bool success)
        {
            if (string.IsNullOrEmpty(_currentJobId)) return;
            if (_feedbackText != null) _feedbackText.text = success ? "Craft Complete!" : "Craft Failed!";
            _currentJobId = null;
            RefreshControls();
        }

        private void OnCraftResult(string recipeId, InventoryItem[] results)
        {
            if (_feedbackText != null)
                _feedbackText.text = results != null && results.Length > 0 ? $"Got: {results[0].ItemId} (+{results.Length - 1} more)" : "Items added to inventory!";
        }

        private void OnCraftStarted(string jobId)
        {
            if (jobId == _currentJobId && _progressSlider != null) _progressSlider.value = 0f;
        }

        private void OnCraftProgress(string jobId, float progress)
        {
            if (jobId == _currentJobId && _progressSlider != null) _progressSlider.value = progress;
        }

        private void OnCraftFailed(string jobId, string reason)
        {
            if (jobId != _currentJobId) return;
            if (_feedbackText != null) _feedbackText.text = $"Failed: {reason}";
            _currentJobId = null;
            RefreshControls();
        }

        private void OnCraftCancelled(string jobId)
        {
            if (jobId != _currentJobId) return;
            if (_feedbackText != null) _feedbackText.text = "Craft Cancelled";
            _currentJobId = null;
            RefreshControls();
        }

        #endregion

        #region Cleanup

        private void ClearRecipeEntries()
        {
            foreach (var e in _entries) if (e != null) Destroy(e);
            _entries.Clear();
        }

        private void ClearMaterialRows()
        {
            foreach (var r in _materialRows) if (r != null) Destroy(r);
            _materialRows.Clear();
        }

        #endregion
    }
}