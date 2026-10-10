namespace IdleDefenseSurvival.Events.Domain
{
    /// <summary>
    /// Event lifecycle state machine.
    /// Prevents concurrent events, double-rewards, UI-driven bypass.
    /// </summary>
    public enum EventState
    {
        /// <summary>No active event.</summary>
        Idle,

        /// <summary>Event selected, waiting for start condition.</summary>
        Scheduled,

        /// <summary>Event running, threat accumulating.</summary>
        Active,

        /// <summary>Waiting for player choice (rift interaction).</summary>
        AwaitingChoice,

        /// <summary>Processing event result (one-time reward grant).</summary>
        Resolving,

        /// <summary>Event successfully completed.</summary>
        Completed,

        /// <summary>Event failed or expired.</summary>
        Failed,

        /// <summary>Cooldown period before next event can start.</summary>
        Cooldown
    }
}
