using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Core.Interfaces;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Simple global audio manager. Handles music and SFX volume and provides helper methods.
    /// It lives in the Bootstrap scene (or is auto‑created by BootstrapInitializer).
    /// </summary>
    public class AudioManager : MonoBehaviour, IAudioService
    {
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        [Header("Configuration")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume   = 1f;

        private AudioSource _musicSource;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Create a child AudioSource for background music if none exists.
            if (transform.Find("MusicSource") == null)
            {
                var go = new GameObject("MusicSource");
                go.transform.SetParent(transform);
                _musicSource = go.AddComponent<AudioSource>();
                _musicSource.loop = true;
                ApplyVolumeSettings();
            }
            else
            {
                _musicSource = transform.Find("MusicSource").GetComponent<AudioSource>();
            }
        }

        private void ApplyVolumeSettings()
        {
            AudioListener.volume = masterVolume;
            if (_musicSource != null) _musicSource.volume = musicVolume;
        }

        /// <summary>
        /// Play a music clip on the dedicated music source.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (_musicSource == null) return;
            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.Play();
        }

        /// <summary>
        /// Play a one‑shot sound effect at the given position.
        /// </summary>
        public void PlaySfx(AudioClip clip, Vector3 worldPos, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, worldPos, sfxVolume * volumeScale);
        }

        /// <summary>
        /// Update volume settings at runtime (e.g., from Settings UI).
        /// </summary>
        public void SetVolumes(float master, float music, float sfx)
        {
            masterVolume = Mathf.Clamp01(master);
            musicVolume = Mathf.Clamp01(music);
            sfxVolume   = Mathf.Clamp01(sfx);
            ApplyVolumeSettings();
        }
    }
}
