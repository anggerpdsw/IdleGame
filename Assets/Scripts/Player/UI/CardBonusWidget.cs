using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Card.Behavior;

namespace IdleDefenseSurvival.Player
{
    public sealed class CardBonusWidget : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _text;

        public void Refresh(CardHUDData data)
        {
            if (_icon != null)
            {
                _icon.sprite = data.Icon;
                _icon.fillAmount = data.FillAmount;
            }
            if (_text != null)
                _text.text = data.Value;
        }
    }
}
