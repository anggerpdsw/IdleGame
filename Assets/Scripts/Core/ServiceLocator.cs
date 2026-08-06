using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Core.Interfaces;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Simple static service locator that exposes the concrete manager instances as their interface types.
    /// This allows game code to depend on abstractions (interfaces) while still using the singleton managers.
    /// </summary>
    public static class ServiceLocator
    {
        public static ISaveService SaveService => SaveManager.Instance;
        public static IEconomyService EconomyService => EconomyManager.Instance;
        public static IAudioService AudioService => AudioManager.Instance;
        public static IAdsService AdsService => AdvertisingManager.Instance;
        public static IAnalyticsService AnalyticsService => (IAnalyticsService)AnalyticsManager.Instance;
        // GameManager does not have an interface; expose the concrete instance for direct use.
        public static GameManager Manager => GameManager.Instance;
    }
}
