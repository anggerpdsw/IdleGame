using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Global UI manager.
    /// Handles popup, overlay and toast roots.
    /// Lives in Bootstrap.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Current Scene UI")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _overlayRoot;
        [SerializeField] private Transform _toastRoot;
        [SerializeField] private Transform _dropRoot;

        private readonly Dictionary<GameObject, GameObject> _instances = new();

        public Canvas Canvas => _canvas;
        public Transform PopupRoot => _popupRoot;
        public Transform OverlayRoot => _overlayRoot;
        public Transform ToastRoot => _toastRoot;
        public Transform DropRoot => _dropRoot;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Called by CanvasRoot every scene.
        /// </summary>
        public void RegisterCanvas(
            Canvas canvas, Transform popupRoot, Transform overlayRoot, Transform toastRoot, Transform dropRoot)
        {
            _canvas = canvas;
            _popupRoot = popupRoot;
            _overlayRoot = overlayRoot;
            _toastRoot = toastRoot;
            _dropRoot = dropRoot;

            _instances.Clear();
        }

        /// <summary>
        /// Spawn singleton popup.
        /// </summary>
        public T ShowPopup<T>(T prefab) where T : MonoBehaviour
        {
            if (prefab == null)
            {
                Debug.LogError("Popup prefab is NULL.");
                return null;
            }

            if (_popupRoot == null)
            {
                Debug.LogError("PopupRoot not registered.");
                return null;
            }

            var key = prefab.gameObject;

            if (_instances.TryGetValue(key, out var existing))
            {
                if (existing != null)
                {
                    existing.SetActive(true);
                    return existing.GetComponent<T>();
                }

                _instances.Remove(key);
            }

            var popup = Instantiate(prefab, _popupRoot, false);
            _instances[key] = popup.gameObject;

            return popup;
        }

        public void HidePopup(MonoBehaviour popup)
        {
            if (popup != null) popup.gameObject.SetActive(false);
        }

        public void DestroyPopup(MonoBehaviour popup)
        {
            if (popup == null) return;

            foreach (var pair in _instances)
            {
                if (pair.Value == popup.gameObject)
                {
                    _instances.Remove(pair.Key);
                    break;
                }
            }

            Destroy(popup.gameObject);
        }

        public void CloseAllPopup()
        {
            foreach (GameObject popup in _instances.Values)
            {
                if (popup != null)
                    popup.SetActive(false);
            }
        }
    }
}