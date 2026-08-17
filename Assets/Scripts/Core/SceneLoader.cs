using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Global scene loader.
    /// Handles additive scene loading/unloading.
    /// Lives under Bootstrap.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public static event Action OnGameSceneLoaded;
        private bool _isLoading;

        private string _isMainMenu = SceneState.MainMenu.ToString();
        private string _isGame = SceneState.Game.ToString();
        private string _isCardCollection = SceneState.CardCollection.ToString();
        private string _isInventory = SceneState.Inventory.ToString();
        private string _isCrafting = SceneState.Crafting.ToString();

        private void Awake()
        {
            // If an instance already exists, this is a duplicate – destroy it.
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Ensure sleep timeout is set after all Awake calls
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        /// <summary>
        /// Switch from one scene to another.
        /// </summary>
        public void SwitchScene(string unloadScene, string loadScene)
        {
            if (_isLoading)
            {
                Debug.LogWarning(
                    $"[SceneLoader] Scene transition already running. " +
                    $"Ignored: {unloadScene} -> {loadScene}"
                );

                return;
            }

            if (string.IsNullOrEmpty(loadScene))
            {
                Debug.LogError("[SceneLoader] loadScene is empty!");
                return;
            }

            StartCoroutine(SwitchSceneRoutine(unloadScene, loadScene));
        }

        private IEnumerator SwitchSceneRoutine(string unloadScene, string loadScene)
        {
            _isLoading = true;

            try
            {
                // Reset global state ketika kembali ke MainMenu
                if (loadScene == _isMainMenu) ResetGlobalState();

                // =========================
                // LOAD TARGET SCENE
                // =========================
                if (!IsSceneLoaded(loadScene))
                {
                    Debug.Log($"[SceneLoader] Loading scene: {loadScene}");
                    AsyncOperation loadOp =
                        SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);

                    if (loadOp == null)
                    {
                        Debug.LogError(
                            $"[SceneLoader] Failed to create " +
                            $"LoadSceneAsync for: {loadScene}"
                        );
                        yield break;
                    }

                    while (!loadOp.isDone) yield return null;
                }
                else
                {
                    Debug.Log($"[SceneLoader] Scene already loaded: {loadScene}");
                }

                // =========================
                // SET ACTIVE SCENE
                // =========================
                Scene loadedScene = SceneManager.GetSceneByName(loadScene);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                {
                    Debug.LogError(
                        $"[SceneLoader] Target scene is invalid " +
                        $"or not loaded: {loadScene}"
                    );
                    yield break;
                }

                SceneManager.SetActiveScene(loadedScene);
                Debug.Log($"[SceneLoader] Active scene: {loadScene}");

                // =========================
                // UNLOAD PREVIOUS SCENE
                // =========================
                if (!string.IsNullOrEmpty(unloadScene) &&
                    unloadScene != loadScene &&
                    IsSceneLoaded(unloadScene))
                {
                    Debug.Log($"[SceneLoader] Unloading scene: {unloadScene}");
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(unloadScene);
                    if (unloadOp != null)
                        while (!unloadOp.isDone)
                            yield return null;
                }

                // =========================
                // GAME CALLBACK
                // =========================
                if (loadScene == _isGame) OnGameSceneLoaded?.Invoke();
            }
            finally
            {
                // Sangat penting:
                // jangan sampai terkunci true selamanya
                _isLoading = false;
            }
        }

        public void LoadGame() => SwitchScene(_isMainMenu, _isGame);
        public void LoadCardCollection() => SwitchScene(_isMainMenu, _isCardCollection);
        public void LoadInventory() => SwitchScene(_isMainMenu, _isInventory);
        public void LoadCrafting() => SwitchScene(_isMainMenu, _isCrafting);
        public void ReturnToMainMenuFromGame() => SwitchScene(_isGame, _isMainMenu);
        public void ReturnToMainMenuFromCardCollection() => SwitchScene(_isCardCollection, _isMainMenu);
        public void ReturnToMainMenuFromInventory() => SwitchScene(_isInventory, _isMainMenu);
        public void ReturnToMainMenuFromCrafting() => SwitchScene(_isCrafting, _isMainMenu);

        private bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public void ResetGlobalState()
        {
            Time.timeScale = 1f;
        }

        private void OnEnable() => Screen.sleepTimeout = SleepTimeout.NeverSleep;
        private void OnApplicationQuit()  => Screen.sleepTimeout = SleepTimeout.SystemSetting;
        private void OnDisable() => Screen.sleepTimeout = SleepTimeout.SystemSetting;
        private void OnDestroy() => Screen.sleepTimeout = SleepTimeout.SystemSetting;

    }
}