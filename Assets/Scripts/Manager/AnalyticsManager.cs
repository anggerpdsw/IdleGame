using System.Collections.Generic;
using IdleDefenseSurvival.Core.Interfaces;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Simple analytics manager for tracking game usage metrics.
    /// Can be replaced with a real service (Firebase, AppsFlyer, etc.) later.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour, IAnalyticsService
    {
        private static AnalyticsManager _instance;
        public static AnalyticsManager Instance => _instance;

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

        // Log generic events
        public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            // Stub: in a real implementation this would send data to an analytics backend
            Debug.Log($"[Analytics] Event: {eventName} {parameters?.ToString() ?? ""}");
        }

        public void SetUserId(string userId)
        {
            Debug.Log($"[Analytics] User set to {userId}");
        }

        public void SetProperty(string key, string value)
        {
            Debug.Log($"[Analytics] Set property {key} = {value}");
        }
    }
}
