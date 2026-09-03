using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Decomposition;
using IdleDefenseSurvival.Items.Generation;
using Newtonsoft.Json;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Repository for craft recipes - loads and manages all available recipes.
    /// Handles recipe unlock state (known/unlocked/hidden).
    /// </summary>
    public sealed class CraftRecipeRepository
    {
        private readonly Dictionary<string, CraftRecipeData> _allRecipes = new();
        private readonly HashSet<string> _unlockedRecipeIds = new();
        private readonly HashSet<string> _knownRecipeIds = new();
        private bool _initialized = false;

        // ============ Events ============
        public event Action<string> OnRecipeUnlocked;      // recipeId
        public event Action<string> OnRecipeDiscovered;    // recipeId

        // ============ Properties ============
        public IReadOnlyDictionary<string, CraftRecipeData> AllRecipes => _allRecipes;
        public IReadOnlyCollection<string> UnlockedRecipeIds => _unlockedRecipeIds;
        public IReadOnlyCollection<string> KnownRecipeIds => _knownRecipeIds;

        // ============ Initialization ============
        public void Initialize()
        {
            if (_initialized) return;
            LoadRecipesFromDatabase();
            _initialized = true;
        }

        private void LoadRecipesFromDatabase()
        {
            // Load recipes from EquipmentData (existing system)
            var allEquipment = ItemDatabase.Instance.AllEquipment.Values ?? Enumerable.Empty<EquipmentData>();
            foreach (var equip in allEquipment)
            {
                if (equip.CraftRecipe != null)
                {
                    var recipe = equip.CraftRecipe;
                    _allRecipes[recipe.RecipeId] = recipe;

                    // Auto-unlock if configured
                    if (recipe.AutoUnlock)
                    {
                        UnlockRecipe(recipe.RecipeId, notify: false);
                    }
                    // Mark as known if default unlocked
                    if (recipe.UnlockSource == UnlockSource.Default)
                    {
                        _knownRecipeIds.Add(recipe.RecipeId);
                    }
                }
            }

            // Load dedicated recipe JSON files (one per equipment slot)
            LoadRecipesFromJson();
        }

        private void LoadRecipesFromJson()
        {
            string[] recipeFiles = {
                "Data/Crafting/Equipment/dataRecipeHat",
                "Data/Crafting/Equipment/dataRecipeGloves",
                "Data/Crafting/Equipment/dataRecipeCape",
                "Data/Crafting/Equipment/dataRecipeArmor",
                "Data/Crafting/Equipment/dataRecipeBelt",
                "Data/Crafting/Equipment/dataRecipePants",
                "Data/Crafting/Equipment/dataRecipePendant",
                "Data/Crafting/Equipment/dataRecipeRing",
                "Data/Crafting/Equipment/dataRecipeEarring",
                "Data/Crafting/Equipment/dataRecipeBracelet",
                "Data/Crafting/Equipment/dataRecipeShoes",
                "Data/Crafting/Potion/dataRecipeHealthPotion",
                "Data/Crafting/Potion/dataRecipeManaPotion"
            };

            int totalLoaded = 0;
            foreach (string path in recipeFiles)
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"[CraftRecipeRepository] Recipe file not found: {path}.json");
                    continue;
                }

                try
                {
                    var wrapper = JsonConvert.DeserializeObject<RecipeFile>(asset.text);
                    var recipes = wrapper?.Recipes;
                    if (recipes == null) continue;

                    foreach (var recipe in recipes)
                    {
                        if (string.IsNullOrEmpty(recipe.RecipeId)) continue;

                        // Inject decomposed requirements into Ingredients array
                        var decomposed = DecomposedRequirementResolver.Compute(recipe.Rarity);
                        if (decomposed.Count > 0)
                        {
                            var extra = new List<CraftIngredient>();
                            foreach (var d in decomposed)
                            {
                                extra.Add(new CraftIngredient
                                {
                                    ItemId = d.ItemId,
                                    Count = d.Quantity,
                                    Consumed = true,
                                    MinQuality = 0,
                                    MinLevel = 0
                                });
                            }
                            // Merge: original ingredients + decomposed
                            recipe.Ingredients = recipe.Ingredients?.Concat(extra).ToArray() ?? extra.ToArray();
                        }

                        // Set category for potion recipes
                        if (recipe.PotionType > 0)
                            recipe.Category = ItemCategory.Consumable;

                        _allRecipes[recipe.RecipeId] = recipe;
                        totalLoaded++;

                        if (recipe.AutoUnlock)
                            UnlockRecipe(recipe.RecipeId, notify: false);
                        if (recipe.UnlockSource == UnlockSource.Default)
                            _knownRecipeIds.Add(recipe.RecipeId);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CraftRecipeRepository] Failed to parse {path}.json: {e.Message}");
                }
            }

            Debug.Log($"[CraftRecipeRepository] Loaded {_allRecipes.Count} recipes, {_unlockedRecipeIds.Count} unlocked, {_knownRecipeIds.Count} known");
        }

        // ============ Public API ============
        public bool TryGetRecipe(string recipeId, out CraftRecipeData recipe)
        {
            return _allRecipes.TryGetValue(recipeId, out recipe);
        }

        public CraftRecipeData GetRecipe(string recipeId)
        {
            _allRecipes.TryGetValue(recipeId, out var recipe);
            return recipe;
        }

        public IReadOnlyList<CraftRecipeData> GetAllRecipes() => _allRecipes.Values.ToList();

        public IReadOnlyList<CraftRecipeData> GetUnlockedRecipes()
        {
            return _unlockedRecipeIds
                .Select(id => _allRecipes.TryGetValue(id, out var r) ? r : null)
                .Where(r => r != null)
                .ToList();
        }

        public IReadOnlyList<CraftRecipeData> GetKnownRecipes()
        {
            return _knownRecipeIds
                .Select(id => _allRecipes.TryGetValue(id, out var r) ? r : null)
                .Where(r => r != null)
                .ToList();
        }

        public IReadOnlyList<CraftRecipeData> GetRecipesForItem(string itemId)
        {
            // Deterministic equipment: recipe produces equipment of its EquipmentType
            // All recipes for a slot produce items of that slot type
            return _allRecipes.Values
                .Where(r => r != null && r.EquipmentType != EquipmentType.None)
                .ToList();
        }

        public IReadOnlyList<CraftRecipeData> GetRecipesByCategory(ItemCategory category)
        {
            return _allRecipes.Values.Where(r => r.Category == category).ToList();
        }

        // ============ Unlock System ============
        public bool IsUnlocked(string recipeId) => _unlockedRecipeIds.Contains(recipeId);
        public bool IsKnown(string recipeId) => _knownRecipeIds.Contains(recipeId) || _unlockedRecipeIds.Contains(recipeId);

        public bool UnlockRecipe(string recipeId, bool notify = true)
        {
            if (!_allRecipes.ContainsKey(recipeId)) return false;
            if (_unlockedRecipeIds.Contains(recipeId)) return true;

            _unlockedRecipeIds.Add(recipeId);
            _knownRecipeIds.Add(recipeId);

            if (notify)
            {
                OnRecipeUnlocked?.Invoke(recipeId);
            }
            return true;
        }

        public bool DiscoverRecipe(string recipeId)
        {
            if (!_allRecipes.ContainsKey(recipeId)) return false;
            if (_knownRecipeIds.Contains(recipeId)) return true;

            _knownRecipeIds.Add(recipeId);
            OnRecipeDiscovered?.Invoke(recipeId);
            return true;
        }

        public void UnlockRecipesByCraftingLevel(int craftingLevel)
        {
            foreach (var recipe in _allRecipes.Values)
            {
                if (recipe.UnlockSource == UnlockSource.BlacksmithLevel
                    && recipe.RequiredBlacksmithLevel <= craftingLevel
                    && !_unlockedRecipeIds.Contains(recipe.RecipeId))
                {
                    UnlockRecipe(recipe.RecipeId, notify: true);
                }
            }
        }

        // ponytail: Tier-based unlock removed — RequiredTier no longer exists on CraftRecipeData
        // public void UnlockRecipesByTier(int tier) { ... }

        // ============ Persistence ============
        public CraftRecipeRepositorySaveData GetSaveData()
        {
            return new CraftRecipeRepositorySaveData
            {
                UnlockedRecipeIds = _unlockedRecipeIds.ToList(),
                KnownRecipeIds = _knownRecipeIds.ToList()
            };
        }

        public void LoadFromSaveData(CraftRecipeRepositorySaveData data)
        {
            if (data == null) return;

            _unlockedRecipeIds.Clear();
            if (data.UnlockedRecipeIds != null)
            {
                foreach (var id in data.UnlockedRecipeIds)
                {
                    if (_allRecipes.ContainsKey(id))
                        _unlockedRecipeIds.Add(id);
                }
            }

            _knownRecipeIds.Clear();
            if (data.KnownRecipeIds != null)
            {
                foreach (var id in data.KnownRecipeIds)
                {
                    if (_allRecipes.ContainsKey(id))
                        _knownRecipeIds.Add(id);
                }
            }
        }
    }

}