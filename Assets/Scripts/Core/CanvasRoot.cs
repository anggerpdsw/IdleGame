using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    [RequireComponent(typeof(Canvas))]
    public class CanvasRoot : MonoBehaviour
    {
        [SerializeField] private Transform popupRoot;
        [SerializeField] private Transform overlayRoot;
        [SerializeField] private Transform toastRoot;
        [SerializeField] private Transform dropRoot;

        private void Start()
        {
            UIManager.Instance.
                RegisterCanvas(GetComponent<Canvas>(), popupRoot, overlayRoot, toastRoot, dropRoot);
        }
    }
}