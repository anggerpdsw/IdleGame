using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Text;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.UI.Tooltip;
using System.Collections;

namespace IdleDefenseSurvival.UI.Upgrade
{
    /// <summary>
    /// Attribute allocation panel. Shows the four main attributes and lets the
    /// player spend unspent stat points (earned 5 per level-up) on any attribute.
    /// Each attribute scanner is auto-filled from the four slots below.
    /// Hovering a row shows a tooltip listing which stats that attribute boosts
    /// and by how much, using the TooltipUI singleton.
    ///
    /// Wire in the scene:
    ///  - 4 rows, each: a Button "+", a TextMeshProUGUI name, a TextMeshProUGUI value.
    ///  - Assign the row references in order Con, Str, Int, Dex.
    /// </summary>
    public class AttributePanelUI : MonoBehaviour
    {
        [Header("Stat order: Con, Str, Int, Dex")]
        [SerializeField] private AttributeRow[] _rows;

        [Header("Points remaining")]
        [SerializeField] private TextMeshProUGUI _unspentStatPoints;

        [System.Serializable]
        private class AttributeRow
        {
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI valueText;
            public Button plusButton;
        }

        private readonly MainAttribute[] _order =
            { MainAttribute.Constitution, MainAttribute.Strength, MainAttribute.Intelligence, MainAttribute.Dexterity };

        private Coroutine _bindRoutine;

        private void OnEnable()
        {
            _bindRoutine = StartCoroutine(BindAccount());
        }

        private void OnDisable()
        {
            if (_bindRoutine != null)
            {
                StopCoroutine(_bindRoutine);
                _bindRoutine = null;
            }
            UnbindAccount();
        }

        private IEnumerator BindAccount()
        {
            // Wait until AccountManager has been created.
            while (AccountManager.Instance == null)
                yield return null;
            var account = AccountManager.Instance;
            account.OnDataLoaded += Refresh;
            account.OnAttributeChanged += Refresh;
            account.OnLevelUp += OnLevelUp;
            // Important:
            // Refresh immediately in case SaveManager already finished loading
            // before this panel became enabled.
            Refresh();
            _bindRoutine = null;
        }

        private void UnbindAccount()
        {
            var account = AccountManager.Instance;
            if (account == null) return;
            account.OnDataLoaded -= Refresh;
            account.OnAttributeChanged -= Refresh;
            account.OnLevelUp -= OnLevelUp;
        }

        private void OnLevelUp(int level) => Refresh();

        /// <summary>Refresh all rows + remaining points from AccountManager.</summary>
        public void Refresh()
        {
            var account = AccountManager.Instance;
            if (account == null) return;

            if (_unspentStatPoints != null) _unspentStatPoints.text = $"Points: {account.UnspentStatPoints}";

            if (_rows == null) return;
            for (int i = 0; i < _rows.Length && i < _order.Length; i++)
            {
                var row = _rows[i];
                if (row == null) continue;

                var attr = _order[i];
                if (row.nameText != null)
                {
                    row.nameText.text = attr.GetShortName();
                }
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

        /// <summary>Shows a hover tooltip listing what this attribute boosts per point.</summary>
        public void ShowAttributeInfo(MainAttribute attr, Vector3 screenPosition)
        {
            var tooltip = TooltipUI.Instance;
            if (tooltip == null) return;

            var account = AccountManager.Instance;
            if (account == null) return;

            int attributeValue = account.GetAttributeValue(attr);
            var bonuses = AttributeService.GetBonuses(attr);

            var sb = new StringBuilder();
            // Header
            sb.AppendLine($"<b><color=#FFD700>{attr.GetDisplayName()}</color></b>");
            // Total effect
            sb.AppendLine("<b>Current Effects:</b>");
            foreach (var bonus in bonuses) 
            {
                float totalFlat = bonus.Flat * attributeValue;
                float totalPercent = bonus.Percent * attributeValue;
                AttributeBonusData totalBonus = new(bonus.Stat, totalFlat, totalPercent);
                sb.AppendLine($"• {bonus.Stat.GetDisplayName()} {valueStat(totalBonus)}");
            }
            // Per point
            sb.AppendLine();
            sb.AppendLine("<b>Per Point:</b>");
            foreach (var bonus in bonuses)
                sb.AppendLine($"• {bonus.Stat.GetDisplayName()} {valueStat(bonus)}");
            var mouse = Pointer.current != null
                ? (Vector3)Pointer.current.position.ReadValue()
                : screenPosition;
            tooltip.ShowText(sb.ToString(), mouse);
        }
        private string valueStat(AttributeBonusData bonus)
        {
            return Mathf.Abs(bonus.Percent) > 0.000001f
                    ? $"+{bonus.Percent:P2}"
                    : $"+{bonus.Flat:0.####}";
        }
        public void HideAttributeInfo() => TooltipUI.Instance?.Hide();
    }
}