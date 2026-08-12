#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace IdleDefenseSurvival.Items.EditorTools
{
    /// <summary>
    /// Editor entry point for craft recipe validation.
    /// Menu: Tools → Crafting → Validate All Recipes.
    /// CLI: Unity.exe -executeMethod IdleDefenseSurvival.Items.EditorTools.CraftRecipeValidationMenu.ValidateAllRecipesCLI
    ///</summary>
    public static class CraftRecipeValidationMenu
    {
        private const string MenuPath = "Tools/Crafting/Validate All Recipes";
        private const string CliNamespace = "IdleDefenseSurvival.Items.EditorTools.CraftRecipeValidationMenu";

        [MenuItem(MenuPath)]
        public static void ValidateAllRecipesMenu()
        {
            var report = RunValidation();
            LogReport(report, "MENU");
        }

        /// <summary>
        /// CLI entry point. Called via -executeMethod. Returns ExitCode 0 on full pass, 1 on any failure.
        ///</summary>
        public static void ValidateAllRecipesCLI()
        {
            var report = RunValidation();
            LogReport(report, "CLI");

            EditorApplication.Exit(report.AllPass ? 0 : 1);
        }

        private static CraftRecipeValidationRunner.Report RunValidation()
        {
            // Bootstrap ItemDatabase
            var go = new GameObject("CraftRecipeValidation_RunnerHost");
            try
            {
                var db = go.AddComponent<ItemDatabase>();
                db.Initialize();

                // CLI bootstrap fix: Awake() is not guaranteed to fire under -executeMethod batchmode,
                // so the static _instance stays null. Force-assign it via reflection so that
                // CraftValidator's `ItemDatabase.Instance?.GetItem(...)` lookups resolve to this db.
                var instanceField = typeof(ItemDatabase).GetField("_instance",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                instanceField?.SetValue(null, db);

                Debug.Log(
                    $"[CraftValidation] " +
                    $"InstanceNull={ItemDatabase.Instance == null}, " +
                    $"IsLoaded={db.IsLoaded}, " +
                    $"ItemCount={db.ItemCount}, " +
                    $"AllItemsCount={db.AllItems?.Count ?? -1}"
                );

                // Load crafting config singleton
                var config = CraftingConfig.Load();

                // Load all 66 recipes from 11 JSON files
                var repo = new CraftRecipeRepository();
                repo.Initialize();

                // Validator with null runtime services — pure validation paths only
                var validator = new CraftValidator(repo, null, null, null);

                var runner = new CraftRecipeValidationRunner(repo, db, config, validator);
                return runner.RunAll();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void LogReport(CraftRecipeValidationRunner.Report report, string source)
        {
            string banner = $"=== Craft Recipe Validation [{source}] ===";
            Debug.Log(banner);
            Debug.Log($"Recipes: {report.TotalRecipes}");
            Debug.Log($"Items:           {(report.ItemsPass ? "PASS" : "FAIL")}");
            Debug.Log($"Design:          {(report.DesignPass ? "PASS" : "FAIL")}");
            Debug.Log($"Economy:         {(report.EconomyPass ? "PASS" : "FAIL")}");
            Debug.Log($"R6 Special:      {(report.R6SpecialPass ? "PASS" : "FAIL")}");
            Debug.Log($"Water Catalyst:  {(report.WaterCatalystPass ? "PASS" : "FAIL")}");
            Debug.Log($"Monotonic Cost:  {(report.MonotonicCostPass ? "PASS" : "FAIL")}");
            Debug.Log($"FAILED RECIPES: {report.FailedCount}");

            if (!report.AllPass && report.Failures != null)
            {
                Debug.LogError("--- FAILURE DETAILS ---");
                foreach (var failure in report.Failures)
                {
                    Debug.LogError(failure);
                }
            }

            Debug.Log($"=== END Craft Recipe Validation [{source}] ===");
        }
    }
}
#endif
