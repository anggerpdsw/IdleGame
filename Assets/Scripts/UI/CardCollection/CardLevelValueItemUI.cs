using TMPro;
using UnityEngine;

public sealed class CardLevelValueItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _valueText;

    public void SetData(string level, string value, Color color)
    {
        _levelText.text = level;
        _valueText.text = value;

        _levelText.color = color;
        _valueText.color = color;
    }
}
