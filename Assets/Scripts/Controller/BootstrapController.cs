using System.Collections;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Entry point that lives for the entire lifetime of the application.
    /// It creates the required global managers (if they are not already present) and
    /// loads the first UI scene (MainMenu) additively.
    /// Place this component on a GameObject in a dedicated "Bootstrap" scene.
    /// </summary>
    public class BootstrapController : MonoBehaviour
    {
        public static bool IsInitialized { get; private set; }
        private const string MainMenuSceneName = "MainMenu";
        private void Awake()
        {
            // Ensure this GameObject persists across scene loads.
            DontDestroyOnLoad(gameObject);

            // Guarantee that each global manager exists exactly once.
            EnsureSingleton<SceneLoader>();
            EnsureSingleton<UIManager>();
            EnsureSingleton<Economy.EconomyManager>();
            EnsureSingleton<IdleRewardManager>();
            EnsureSingleton<RewardManager>();
            EnsureSingleton<InventoryManager>();
            EnsureSingleton<DailyRewardManager>();
            EnsureSingleton<AccountManager>();
            EnsureSingleton<ModifierManager>();
            EnsureSingleton<PlayerStatsManager>();
            EnsureSingleton<BaseStatLoader>();
            EnsureSingleton<EnemyStatisticsManager>();
            EnsureSingleton<AudioManager>();
            EnsureSingleton<AdvertisingManager>();
            EnsureSingleton<AnalyticsManager>();
            EnsureSingleton<WaveManager>();
            EnsureSingleton<CardDatabase>();
            EnsureSingleton<CardInventory>();
            EnsureSingleton<CardEquipmentService>();
            EnsureSingleton<CardManager>();
            // Inventory/Equipment/Craft services (new items system)
            EnsureSingleton<ItemDatabase>();
            ItemDatabase.Instance?.Initialize(); // Load item JSONs synchronously before any scene uses them
            EnsureSingleton<InventoryService>();
            EnsureSingleton<DropBagManager>();
            EnsureSingleton<EquipmentService>();
            EnsureSingleton<SaveManager>();
            // Attribute stat modifiers need SaveManager (AccountData) to exist; it is created above.
            // If the save has not loaded yet, AttributeModifierManager re-applies on OnSaveLoaded.
            EnsureSingleton<AttributeModifierManager>();
            EnsureSingleton<GameManager>();
            EnsureSingleton<CraftingManager>();

            if (SaveManager.Instance?.IsSaveLoaded == true) {
                // Dev: fill inventory once when the save is truly fresh (no save file yet)
                InventorySampleSeeder.SeedIfEmpty();
            } else {
                SaveManager.OnSaveLoaded += OnSaveLoaded_SeedIfEmpty;
            }
            // Load save data AFTER all managers exist (SaveManager.Start() will handle loading via coroutine)
            IsInitialized = true;
            StartCoroutine(LoadMainMenu());
        }

        private void OnSaveLoaded_SeedIfEmpty()
        {
            SaveManager.OnSaveLoaded -= OnSaveLoaded_SeedIfEmpty;
            InventorySampleSeeder.SeedIfEmpty();
        }

        private IEnumerator LoadMainMenu()
        {
            if (IsSceneLoaded(MainMenuSceneName)) yield break;
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Additive);
            Scene scene = SceneManager.GetSceneByName(MainMenuSceneName);
            if (scene.IsValid()) SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// Helper that creates a GameObject with the requested component when the
        /// singleton instance is null. The created GameObject is also marked as
        /// DontDestroyOnLoad so the manager survives the whole session.
        /// </summary>
        private void EnsureSingleton<T>() where T : MonoBehaviour
        {
            var prop = typeof(T).GetProperty("Instance");
            if (prop == null) return; // No static Instance – nothing to enforce.
            var instance = prop.GetValue(null);
            if (instance == null)
            {
                var go = new GameObject(typeof(T).Name);
                go.AddComponent<T>();
                DontDestroyOnLoad(go);
            }
        }

        private bool IsSceneLoaded(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == name) return true;
            }
            return false;
        }
    }
}
