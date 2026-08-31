using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Floating UGUI joystick for player movement input.
    /// Works with touch and mouse. No Input System package dependency.
    /// </summary>
    public class Joystick : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _background;
        [SerializeField] private GameObject _handle;
        [SerializeField] public Vector2 joyStickVec;
        [SerializeField] private Vector2 _joyStickTouchPos;
        [SerializeField] private Vector2 _joyStickOriginalPos;
        [SerializeField] private float _joyStickRadius;
        
        private void Awake()
        {
            _joyStickOriginalPos = _background.transform.position;
            _joyStickRadius = _background.GetComponent<RectTransform>().sizeDelta.y / 4;
            _background.SetActive(false);
            _handle.SetActive(false);
        }

        public void PointerDown(BaseEventData baseEventData)
        {
            _background.SetActive(true);
            _handle.SetActive(true);

            PointerEventData pointerEventData = baseEventData as PointerEventData;
            Vector2 pos = pointerEventData.position;
            _handle.transform.position = pos;
            _background.transform.position = pos;
            _joyStickTouchPos = pos;
        }
        
        public void Drag(BaseEventData baseEventData)
        {
            PointerEventData pointerEventData = baseEventData as PointerEventData;
            Vector2 dragPos = pointerEventData.position;
            Vector2 vecFromTouch = dragPos - _joyStickTouchPos;
            float joystickDist = vecFromTouch.magnitude;

            joyStickVec = vecFromTouch.normalized;

            if (joystickDist < _joyStickRadius)
            {
                _handle.transform.position = _joyStickTouchPos + vecFromTouch;
            }
            else
            {
                _joyStickTouchPos = dragPos - joyStickVec * _joyStickRadius;
                _handle.transform.position = dragPos;
                _background.transform.position = _joyStickTouchPos;
            }
        }

        public void PointerUp()
        {
            joyStickVec = Vector2.zero;
            _handle.transform.position = _joyStickOriginalPos;
            _background.transform.position = _joyStickOriginalPos;
            _handle.SetActive(false);
            _background.SetActive(false);
        }
    }

}