namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Composite condition: Negates subcondition (NOT logic).
    /// </summary>
    public class NotCondition : ICondition
    {
        private readonly ICondition _subcondition;

        public NotCondition(ICondition subcondition)
        {
            _subcondition = subcondition;
        }

        public bool IsMet(IBehaviorContext context)
        {
            return _subcondition == null || !_subcondition.IsMet(context);
        }
    }
}
