using UnityEngine;

namespace IdleDefenseSurvival.Core
{
    /// <summary>
    /// Guarantees that a BootstrapController exists even if the developer forgets to place a Bootstrap scene.
    /// This runs right after the first scene loads.
    /// </summary>
    public static class BootstrapInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExists()
        {
            if (Object.FindFirstObjectByType<BootstrapController>() != null) return;

            var go = new GameObject("Bootstrap");
            go.AddComponent<BootstrapController>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
