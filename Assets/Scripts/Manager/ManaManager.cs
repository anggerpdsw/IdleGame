using IdleDefenseSurvival.Core;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Simple game configuration manager. Holds constants and tunable values
    /// that are shared between scenes. Can be persisted in SaveData if needed.
    /// </summary>
    public class ManaManager : MonoBehaviour
    {
        private static ManaManager _instance;
        public static ManaManager Instance => _instance;

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

        public float CurrentMana { get; private set; }
        public float MaxMana { get; private set; }

        public bool HasEnough(float amount)
        {
            return CurrentMana >= amount;
        }

        public bool TryConsume(float amount)
        {
            if (!HasEnough(amount))
                return false;

            CurrentMana -= amount;
            return true;
        }
    }
}
