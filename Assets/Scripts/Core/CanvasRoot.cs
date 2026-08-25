using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    [RequireComponent(typeof(Canvas))]
    public class CanvasRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private Transform popupRoot;
        [SerializeField] private Transform ultimateRoot;
        [SerializeField] private Transform toastRoot;
        [SerializeField] private Transform dropRoot;

        private void Start()
        {
            UIManager.Instance.
                RegisterCanvas(canvas, popupRoot, ultimateRoot, toastRoot, dropRoot);
        }
    }
}