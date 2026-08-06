using UnityEngine;

namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Analytics abstraction. UI and gameplay can log events without directly referencing a concrete analytics SDK.
    /// </summary>
    public interface IAnalyticsService
    {
        void LogEvent(string eventName, System.Collections.Generic.Dictionary<string, object> parameters = null);
        void SetUserId(string userId);
        void SetProperty(string key, string value);
    }
}