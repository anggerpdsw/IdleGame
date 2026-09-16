using UnityEngine;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Context passed to all behavior components for evaluation and execution.
    /// Provides access to pet state, player state, battle state without direct coupling.
    /// Read-only view of battle state - behavior components should not mutate context directly.
    /// </summary>
    public interface IBehaviorContext
    {
        // Pet state
        PetRuntime Pet { get; }
        Transform PetTransform { get; }
        Vector3 PetPosition { get; }

        // Player state
        Player.Player Player { get; }
        Transform PlayerTransform { get; }
        Vector3 PlayerPosition { get; }
        float PlayerHealthPercent { get; }

        // Battle state
        float BattleTime { get; }
        int AliveEnemyCount { get; }
        int EliteCount { get; }
        int BossCount { get; }
        bool IsEmergencyMode { get; }

        // Target state
        Transform CurrentTarget { get; }
        bool HasValidTarget { get; }
    }
}
