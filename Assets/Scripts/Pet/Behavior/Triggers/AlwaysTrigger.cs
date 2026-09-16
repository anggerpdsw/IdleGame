namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Always returns true - behavior executes every evaluation if conditions met.
    /// </summary>
    public class AlwaysTrigger : ITrigger
    {
        public bool ShouldTrigger(IBehaviorContext context) => true;
    }
}
