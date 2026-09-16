namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Chain: Enables chain/bounce to additional targets.
    /// Action must implement chain logic (not all actions support it).
    /// </summary>
    public class ChainModifier : IModifier
    {
        private readonly int _chainCount;
        private readonly float _damageReduction;

        public ChainModifier(int chainCount, float damageReduction = 0.5f)
        {
            _chainCount = chainCount;
            _damageReduction = damageReduction;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.EnableChain = true;
            data.ChainCount = _chainCount;
            data.ChainDamageReduction = _damageReduction;
        }
    }
}
