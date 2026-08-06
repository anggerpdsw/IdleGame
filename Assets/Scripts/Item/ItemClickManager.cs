using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleDefenseSurvival.Item
{
    /// <summary>
    /// Centralized click handler for currency items using the new Input System.
    /// More efficient than per-item click checks in Items.Update().
    /// Attach to the Main Camera.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class ItemClickManager : MonoBehaviour
    {
        private bool _debug;
        [Header("Layer Filter")]
        [Tooltip("Only items on the 'Items' layer will be clickable. Default set to Items layer.")]
        [SerializeField] private LayerMask _itemLayerMask;

        private UnityEngine.Camera _mainCamera;
        private InputAction _clickAction;

        private void Awake()
        {
            // Get camera from this GameObject, fallback to main camera
            _mainCamera = GetComponent<UnityEngine.Camera>();
            if (_mainCamera == null)
                _mainCamera = UnityEngine.Camera.main;

            // Set default layer mask to "Items" layer (layer 6)
            if (_itemLayerMask == 0)
            {
                _itemLayerMask = LayerMask.GetMask("Items");
            }

            // Create InputAction for left mouse button / click Android
            _clickAction = new InputAction("ItemClick", InputActionType.Button);
            _clickAction.AddBinding("<Pointer>/press");
        }

        // Fallback for direct click detection in Update if Input System event fails
        private void Update()
        {
            if (_clickAction != null && _clickAction.enabled && _clickAction.WasPressedThisFrame())
            {
                // Only call if InputAction event might not have fired, or for testing
                // This is generally not needed if OnClickPerformed fires correctly
                // Debug.Log("[ItemClickManager] Fallback Update click detected.");
                // HandleClick();
            }
        }

        private void HandleClick()
        {
            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector2 worldPos = _mainCamera.ScreenToWorldPoint(screenPos);

            // Check ALL colliders at click point (no layer filter)
            if (_debug) {
                Collider2D[] allColliders = Physics2D.OverlapPointAll(worldPos);
                foreach (var col in allColliders)
                {
                    Debug.Log($"  - {col.gameObject.name} on layer {LayerMask.LayerToName(col.gameObject.layer)}");
                }
            }

            // Use Physics2D.OverlapPointAll to get all colliders at the click point
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos, _itemLayerMask);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<CurrencyPickup>(out var item))
                {
                    item.StartCollection();
                    return; // Only collect one item per click
                }
            }
        }

        private void OnEnable()
        {
            _clickAction.performed += OnClickPerformed;
            _clickAction.Enable();
        }

        private void OnDisable()
        {
            _clickAction.performed -= OnClickPerformed;
            _clickAction.Disable();
        }

        private void OnDestroy()
        {
            _clickAction?.Dispose();
        }

        private void OnClickPerformed(InputAction.CallbackContext _)
        {
            HandleClick();
        }

    }
}
