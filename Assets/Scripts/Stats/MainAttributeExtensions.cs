namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Display helpers for the four core attributes (CON/STR/INT/DEX).
    /// </summary>
    public static class MainAttributeExtensions
    {
        public static string GetMainDisplayName(this MainAttribute attr) => attr switch
        {
            MainAttribute.Constitution => "Constitution",
            MainAttribute.Strength => "Strength",
            MainAttribute.Intelligence => "Intelligence",
            MainAttribute.Dexterity => "Dexterity",
            _ => "Unknown"
        };

        public static string GetMainShortName(this MainAttribute attr) => attr switch
        {
            MainAttribute.Constitution => "CON",
            MainAttribute.Strength => "STR",
            MainAttribute.Intelligence => "INT",
            MainAttribute.Dexterity => "DEX",
            _ => "?"
        };

    }
}