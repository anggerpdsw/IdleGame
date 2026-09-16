namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Gate condition that must be satisfied for behavior execution.
    /// Evaluated after trigger, before action.
    /// Composable via AND/OR/NOT wrappers.
    /// </summary>
    public interface ICondition
    {
        bool IsMet(IBehaviorContext context);
    }
}
