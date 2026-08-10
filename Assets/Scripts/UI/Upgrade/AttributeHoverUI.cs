using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleDefenseSurvival.UI.Upgrade
{
    public class AttributeHoverUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private MainAttribute _attribute;
        [SerializeField] private AttributePanelUI _attributePanel;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_attributePanel == null) return;
            _attributePanel.ShowAttributeInfo(_attribute, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_attributePanel == null) return;
            _attributePanel.HideAttributeInfo();
        }
    }
}