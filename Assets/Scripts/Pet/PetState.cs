namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Pet behavior states for state machine transitions.
    /// </summary>
    public enum PetState
    {
        /// <summary>No target, idle animation</summary>
        Idle,

        /// <summary>Following/orbiting player</summary>
        Follow,

        /// <summary>Searching for valid target within range</summary>
        SearchTarget,

        /// <summary>Attacking current target</summary>
        Attack,

        /// <summary>Casting active skill</summary>
        CastSkill,

        /// <summary>Emergency mode - player HP critical</summary>
        Emergency,

        /// <summary>Pet HP depleted (if applicable)</summary>
        Dead,

        /// <summary>Returning to player orbit position</summary>
        Returning
    }
}
