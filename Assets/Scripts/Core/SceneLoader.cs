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
            if (_isLoading) return;
            var routine = SwitchSceneRoutine(unloadScene, loadScene);
            StartCoroutine(routine);
        }

        private IEnumerator SwitchSceneRoutine(string unloadScene, string loadScene)
        {
            _isLoading = true;

            // Kembalikan ke normal ketika keluar dari Game Scene
            if (loadScene  == _isMainMenu) ResetGlobalState();
            
            // Load target scene first
            if (!IsSceneLoaded(loadScene))
            {
                AsyncOperation loadOp = SceneManager.LoadSceneAsync(loadScene, LoadSceneMode.Additive);
                while (!loadOp.isDone) yield return null;
            }

            // Set loaded scene active
            Scene loadedScene = SceneManager.GetSceneByName(loadScene);
            if (loadedScene.IsValid()) SceneManager.SetActiveScene(loadedScene);

            // Unload previous scene
            if (!string.IsNullOrEmpty(unloadScene) && IsSceneLoaded(unloadScene))
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(unloadScene);
                if (unloadOp != null) while (!unloadOp.isDone) yield return null;
            }

            // Scene Game sudah selesai dimuat
            if (loadScene == _isGame) OnGameSceneLoaded?.Invoke();

            _isLoading = false;
        }

        public void LoadGame() => SwitchScene(_isMainMenu, _isGame);
        public void LoadCardCollection() => SwitchScene(_isMainMenu, _isCardCollection);
        public void LoadInventory() => SwitchScene(_isMainMenu, _isInventory);
        public void ReturnToMainMenuFromGame() => SwitchScene(_isGame, _isMainMenu);
        public void ReturnToMainMenuFromCardCollection() => SwitchScene(_isCardCollection, _isMainMenu);
        public void ReturnToMainMenuFromInventory() => SwitchScene(_isInventory, _isMainMenu);

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