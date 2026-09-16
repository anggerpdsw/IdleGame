namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Determines when a behavior should attempt to execute.
    /// Returns true if trigger condition is met.
    /// </summary>
    public interface ITrigger
    {
        bool ShouldTrigger(IBehaviorContext context);
    }
}
