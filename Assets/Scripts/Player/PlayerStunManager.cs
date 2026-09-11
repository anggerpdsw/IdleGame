using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Manages stun effects on the player.
    /// Uses source-ID tracking so multiple enemies can apply independent stuns.
    /// Any active stun disables player movement (joystick); when none remain,
    /// joystick re-enabled to its previous state.
    /// </summary>
    public sealed class PlayerStunManager : MonoBehaviour
    {
        private static PlayerStunManager _instance;
        public static PlayerStunManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerStunManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PlayerStunManager");
                        _instance = go.AddComponent<PlayerStunManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // sourceId → expiry time
        private readonly Dictionary<string, StunEffect> _activeStuns = new();

        private bool _wasJoystickEnabled = true;

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

        private void Update()
        {
            float now = Time.time;
            bool changed = false;

            var toRemove = new List<string>();
            foreach (var kvp in _activeStuns)
            {
                if (kvp.Value.ExpireTime <= now)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _activeStuns.Remove(key);
                changed = true;
            }

            if (changed) ApplyStunState();
        }

        /// <summary>
        /// Apply or refresh a stun from a given source.
        /// </summary>
        /// <param name="sourceId">Unique identifier (e.g. "EnemyEffect_Orc_12345_Stun")</param>
        /// <param name="duration">Seconds to stun</param>
        public void ApplyStun(string sourceId, float duration)
        {
            if (string.IsNullOrEmpty(sourceId) || duration <= 0f) return;

            float expire = Time.time + Mathf.Max(0.1f, duration);
            _activeStuns[sourceId] = new StunEffect { SourceId = sourceId, ExpireTime = expire };
            ApplyStunState();
        }

        /// <summary>
        /// Remove a specific stun source (rarely needed, but included for symmetry).
        /// </summary>
        public void RemoveStun(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (_activeStuns.Remove(sourceId)) ApplyStunState();
        }

        // Enable joystick only when no active stuns.
        private void ApplyStunState()
        {
            var player = Player.Instance;
            if (player == null) return;

            var joystick = player.GetComponentInChildren<Joystick>();
            if (joystick == null) return;

            bool anyStun = _activeStuns.Count > 0;

            if (anyStun)
            {
                if (joystick.enabled)
                {
                    _wasJoystickEnabled = true;
                    joystick.enabled = false;
                }
            }
            else
            {
                joystick.enabled = _wasJoystickEnabled;
            }
        }

        /// <summary>
        /// Whether the player is currently stunned.
        /// </summary>
        public bool IsStunned => _activeStuns.Count > 0;

        /// <summary>
        /// Number of active stun sources.
        /// </summary>
        public int ActiveStunCount => _activeStuns.Count;

        private struct StunEffect
        {
            public string SourceId;
            public float ExpireTime;
        }
    }
}