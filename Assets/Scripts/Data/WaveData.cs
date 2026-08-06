using System;

namespace IdleDefenseSurvival.Data
{
    [Serializable] public class WaveData
    {
        public WaveSection waves;
        public DifficultySection difficulty;
        public SpawningSection spawning;
    }

    [Serializable] public class WaveSection
    {
        public float waveDuration;
        public float interWaveDuration;
        public int maxWave;
    }

    [Serializable] public class DifficultySection
    {
        public float tierMultiplier;
        public float healthMultiplier;
        public float speedMultiplier;
        public float damageMultiplier;
    }

    [Serializable] public class SpawningSection
    {
        public float baseSpawnInterval;
        public float minSpawnInterval;
        public int minWaveSpawnDecay;
        public float maxSpawnBuffer;
    }

    // -------------------------------------------------------------------
    // UI Data Structure
    // -------------------------------------------------------------------
    [Serializable] public struct WaveInfo
    {
        public int TierNumber;
        public int WaveNumber;
        public WaveState State;
        public float TimeRemaining;
        public float WaveDuration;
        public float InterWaveDuration;
        public float HealthMult;
        public float SpeedMult;
        public float DamageMult;
        public float SpawnInterval;
    }

}
