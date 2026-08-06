using UnityEngine;

namespace IdleDefenseSurvival.Core.Interfaces
{
    /// <summary>
    /// Audio abstraction used by UI and gameplay to play music and SFX.
    /// Allows swapping the implementation for a mock in unit tests.
    /// </summary>
    public interface IAudioService
    {
        void PlayMusic(AudioClip clip, bool loop = true);
        void PlaySfx(AudioClip clip, Vector3 worldPos, float volumeScale = 1f);
        void SetVolumes(float master, float music, float sfx);
    }
}