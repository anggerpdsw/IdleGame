using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Loads player skill base values from dataPlayer.json into PlayerStatsManager.
    /// Skills have no levels — they are static values later influenced by
    /// main stats (Constitution/Strength/Intelligence/Dexterity) via ModifierManager.
    /// Kept as a manager for scene/bootstrap compatibility.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static UpgradeManager _instance;
        public static UpgradeManager Instance => _instance;

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

            LoadBaseStats();
        }

        /// <summary>
        /// Reload base skill values from dataPlayer.json and push them into PlayerStatsManager.
        /// Called on init and after save load.
        /// </summary>
        public void LoadBaseStats()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null)
            {
                Debug.LogError("[UpgradeManager] dataPlayer.json not found!");
                return;
            }

            PlayerData playerData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
            Player.StatLoader.LoadBaseStats(playerData);
        }

        /// <summary>
        /// Refresh player stats in game (applies any modifiers).
        /// </summary>
        public void RefreshPlayerStats()
        {
            Player.Player.Instance?.ReloadStats();
        }
    }
}
