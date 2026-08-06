using IdleDefenseSurvival.Core;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Simple game configuration manager. Holds constants and tunable values
    /// that are shared between scenes. Can be persisted in SaveData if needed.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;
        private int _pendingTier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OpenGame(int tier)
        {    
            _pendingTier = tier;
            SceneLoader.Instance.LoadGame();
        }

        private void HandleGameLoaded() => WaveManager.Instance.InitializeRun(_pendingTier);

        private void OnEnable() => SceneLoader.OnGameSceneLoaded += HandleGameLoaded;

        private void OnDisable() => SceneLoader.OnGameSceneLoaded -= HandleGameLoaded;


    }
}
