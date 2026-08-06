using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Attribute allocation panel. Shows the four main attributes and lets the
    /// player spend unspent stat points (earned 5 per level-up) on any attribute.
    /// Each attribute scanner is auto-filled from the four slots below.
    ///
    /// Wire in the scene:
    ///  - 4 rows, each: a Button "+", a TextMeshProUGUI name, a TextMeshProUGUI value.
    ///  - Assign the row references in order Constitution, Strength, Int, Dex.
    /// </summary>
    public class AttributePanelUI : MonoBehaviour
    {
        [Header("Stat rows (order: Constitution, Strength, Intelligence, Dexterity)")]
        [SerializeField] private AttributeRow[] _rows;

        [Header("Points remaining")]
        [SerializeField] private TextMeshProUGUI _pointsText;

        [System.Serializable]
        private class AttributeRow
        {
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI valueText;
            public Button plusButton;
        }

        private readonly MainAttribute[] _order =
            { MainAttribute.Constitution, MainAttribute.Strength, MainAttribute.Intelligence, MainAttribute.Dexterity };

        private void OnEnable()
        {
            AccountManager.Instance.OnAttributeChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (AccountManager.Instance != null)
                AccountManager.Instance.OnAttributeChanged -= Refresh;
        }

        /// <summary>Refresh all rows + remaining points from AccountManager.</summary>
        public void Refresh()
        {
            var account = AccountManager.Instance;
            if (account == null) return;

            if (_pointsText != null) _pointsText.text = $"Points: {account.UnspentStatPoints}";

            if (_rows == null) return;
            for (int i = 0; i < _rows.Length && i < _order.Length; i++)
            {
                var row = _rows[i];
                if (row == null) continue;

                var attr = _order[i];
                if (row.nameText != null) row.nameText.text = attr.ToString();
                if (row.valueText != null) row.valueText.text = account.GetAttributeValue(attr).ToString();

                if (row.plusButton != null)
                {
                    MainAttribute attrCopy = attr; // capture for closure
                    row.plusButton.onClick.RemoveAllListeners();
                    row.plusButton.onClick.AddListener(() => account.SpendPoint(attrCopy));
                    row.plusButton.interactable = account.UnspentStatPoints > 0;
                }
            }
        }
    }
}