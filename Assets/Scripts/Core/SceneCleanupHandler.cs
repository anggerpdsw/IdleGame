using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Attach to Bootstrap's root GameObject.
    /// Ensures only one EventSystem and one AudioListener remain across additive scenes.
    /// Listens to scene load events to clean up duplicates after each load.
    /// </summary>
    public class SceneCleanupHandler : MonoBehaviour
    {
        private void Awake()
        {
            // Initial cleanup for the starting scene (e.g., MainMenu)
            Cleanup();
            // Subscribe to future scene load events
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            // Unsubscribe to avoid memory leaks
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Cleanup after each additive scene load to ensure only one instance remains
            Cleanup();
        }

        private void Cleanup()
        {
            // Keep the very first EventSystem; destroy any others that appear later.
            var allSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            for (int i = 1; i < allSystems.Length; i++)
            {
                Destroy(allSystems[i].gameObject);
            }

            // Keep the first AudioListener on the MainCamera (in Game scene).
            // Disable any extra listeners (e.g., from MainMenu Canvas).
            var allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 1; i < allListeners.Length; i++)
            {
                // If listener is on a camera we want to keep, just disable the component.
                allListeners[i].enabled = false;
            }
        }
    }
}
