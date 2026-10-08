using UnityEngine;

namespace IdleDefenseSurvival.Card.Behavior
{
    public readonly struct CardHUDData
    {
        public readonly Sprite Icon;
        public readonly string Value;
        public readonly float FillAmount; // 0-1 for radial cooldown

        public CardHUDData(Sprite icon, string value, float fillAmount = 1f)
        {
            Icon = icon;
            Value = value;
            FillAmount = fillAmount;
        }
    }

    public interface ICardHUDProvider
    {
        CardEffectType EffectType { get; }
        CardHUDData GetHUDData();
    }
}
