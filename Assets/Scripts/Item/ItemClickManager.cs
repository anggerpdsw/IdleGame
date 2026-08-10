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
        [SerializeField] private bool _debug;
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
            Vector3 world3D = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, Mathf.Abs(_mainCamera.transform.position.z))
            );
            Vector2 worldPos = world3D;

            // Check ALL colliders at click point (no layer filter)
            if (_debug) 
            {
                Debug.Log(
                    $"[ItemClickManager] " +
                    $"Screen={screenPos} | " +
                    $"World={worldPos} | " +
                    $"Mask={_itemLayerMask.value}"
                );

                // TEST 1: tanpa layer mask
                Collider2D[] allHits = Physics2D.OverlapPointAll(worldPos);
                Debug.Log($"[ItemClickManager] ALL HITS = {allHits.Length}");

                foreach (var hit in allHits) 
                {
                    Debug.Log(
                        $"[ItemClickManager] ALL HIT: " +
                        $"{hit.name} | " +
                        $"Layer={LayerMask.LayerToName(hit.gameObject.layer)} | " +
                        $"LayerIndex={hit.gameObject.layer} | " +
                        $"Enabled={hit.enabled}"
                    );
                }
            }

            // Use Physics2D.OverlapPointAll to get all colliders at the click point
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPos, _itemLayerMask);
            // TEST 2: dengan layer mask
            if (_debug) Debug.Log($"[ItemClickManager] ITEM HITS = {hits.Length}");
            foreach (var hit in hits)
            {
                if (_debug) Debug.Log($"[ItemClickManager] ITEM HIT = {hit.name}"); 
                if (!hit.TryGetComponent<CurrencyPickup>(out var item)) 
                    item = hit.GetComponentInParent<CurrencyPickup>();
                if (item == null)
                {
                    if (_debug) Debug.LogWarning(
                        $"[ItemClickManager] Collider {hit.name} " +
                        $"tidak memiliki CurrencyPickup di parent."
                    );
                    continue;
                }

                if (_debug) Debug.Log($"[ItemClickManager] COLLECT: {item.name}");
                
                item.StartCollection();
                return;
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
