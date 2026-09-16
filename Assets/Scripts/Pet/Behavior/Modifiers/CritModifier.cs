namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Crit: Adds critical hit chance and damage bonus.
    /// </summary>
    public class CritModifier : IModifier
    {
        private readonly float _critChanceBonus;
        private readonly float _critDamageBonus;

        public CritModifier(float critChanceBonus, float critDamageBonus)
        {
            _critChanceBonus = critChanceBonus;
            _critDamageBonus = critDamageBonus;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.CriticalChanceBonus += _critChanceBonus;
            data.CriticalDamageBonus += _critDamageBonus;
        }
    }
}
