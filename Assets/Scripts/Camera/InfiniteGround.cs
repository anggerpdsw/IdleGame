using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Camera
{
    /// <summary>
    /// Generates a finite number of ground tiles around the camera
    /// and removes tiles that are too far away.
    ///
    /// The player can move infinitely in X/Y without exposing
    /// the default camera background.
    /// </summary>
    public class InfiniteGround : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera used to determine which tiles should exist.")]
        [SerializeField] private UnityEngine.Camera _camera;
        [Tooltip("Prefab containing the ground SpriteRenderer.")]
        [SerializeField] private GameObject _tilePrefab;
        [Tooltip("Parent for generated tiles.")]
        [SerializeField] private Transform _tileParent;

        [Header("Tile Settings")]
        [Tooltip("World-space size of one ground tile.")]
        [SerializeField] private Vector2 _tileWorldSize = new(10f, 10f);
        [Tooltip("Z position of generated ground tiles.")]
        [SerializeField] private float _tileZ = 0f;


        [Header("Generation")]
        [Tooltip("Extra tiles generated around the visible camera area. " +
            "Higher values prevent tiles popping into view.")]
        [SerializeField, Min(0)]
        private int _extraTiles = 2;

        [Tooltip("Maximum distance in tiles from the camera before a tile is removed.")]
        [SerializeField, Min(1)]
        private int _keepDistance = 6;

        [Header("Update Settings")]
        [Tooltip("How often the system checks whether the camera moved to another tile.")]
        [SerializeField, Min(0.01f)]
        private float _updateInterval = 0.05f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugGrid = false;

        // Active tiles indexed by their grid coordinate.
        private readonly Dictionary<Vector2Int, GameObject> _activeTiles = new();
        private Vector2Int _lastCameraTile;
        private float _updateTimer;
        private bool _initialized;

        private void Awake()
        {
            ValidateReferences();
        }

        private void Start()
        {
            if (!ValidateSettings()) return;
            GenerateInitialTiles();
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            _updateTimer += Time.deltaTime;
            if (_updateTimer < _updateInterval) return;
            _updateTimer = 0f;
            UpdateTiles();
        }

        // =========================================================
        // INITIALIZATION
        // =========================================================
        private void GenerateInitialTiles()
        {
            _lastCameraTile = WorldToGrid(_camera.transform.position);
            GenerateTilesAroundCamera();
            RemoveFarTiles();
        }

        // =========================================================
        // TILE UPDATE
        // =========================================================
        private void UpdateTiles()
        {
            Vector2Int currentCameraTile = WorldToGrid(_camera.transform.position);
            // Camera is still inside the same tile.
            if (currentCameraTile == _lastCameraTile) return;
            _lastCameraTile = currentCameraTile;
            GenerateTilesAroundCamera();
            RemoveFarTiles();
        }

        // =========================================================
        // GENERATION
        // =========================================================
        private void GenerateTilesAroundCamera()
        {
            Vector2Int cameraTile = WorldToGrid(_camera.transform.position);
            GetRequiredTileRange(
                out int minX,
                out int maxX,
                out int minY,
                out int maxY
            );

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int coordinate = new(x, y);
                    if (_activeTiles.ContainsKey(coordinate)) continue;
                    CreateTile(coordinate);
                }
            }
        }

        private void GetRequiredTileRange(
            out int minX,
            out int maxX,
            out int minY,
            out int maxY)
        {
            Vector2 cameraPosition = _camera.transform.position;

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            // Determine how many tiles the camera can currently see.
            int visibleTilesX = Mathf.CeilToInt(halfWidth / _tileWorldSize.x);
            int visibleTilesY = Mathf.CeilToInt(halfHeight / _tileWorldSize.y);
            Vector2Int cameraTile = WorldToGrid(cameraPosition);

            minX = cameraTile.x - visibleTilesX - _extraTiles;
            maxX = cameraTile.x + visibleTilesX + _extraTiles;
            minY = cameraTile.y - visibleTilesY - _extraTiles;
            maxY = cameraTile.y + visibleTilesY+ _extraTiles;
        }

        private void CreateTile(Vector2Int coordinate)
        {
            Vector3 position = GridToWorld(coordinate);
            GameObject tile = Instantiate(_tilePrefab, position, Quaternion.identity, _tileParent);
            tile.name = $"Ground_{coordinate.x}_{coordinate.y}";
            _activeTiles.Add(coordinate, tile);
        }

        // =========================================================
        // CLEANUP
        // =========================================================
        private void RemoveFarTiles()
        {
            Vector2Int cameraTile = WorldToGrid(_camera.transform.position);
            List<Vector2Int> tilesToRemove = new();
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _activeTiles)
            {
                Vector2Int tileCoordinate = pair.Key;
                int distanceX = Mathf.Abs(tileCoordinate.x - cameraTile.x);
                int distanceY = Mathf.Abs(tileCoordinate.y - cameraTile.y);
                int distance = Mathf.Max(distanceX, distanceY);
                if (distance > _keepDistance)
                    tilesToRemove.Add(tileCoordinate);
            }
            foreach (Vector2Int coordinate in tilesToRemove)
            {
                RemoveTile(coordinate);
            }
        }

        private void RemoveTile(Vector2Int coordinate)
        {
            if (!_activeTiles.TryGetValue(coordinate, out GameObject tile)) return;
            if (tile != null) Destroy(tile);
            _activeTiles.Remove(coordinate);
        }

        // =========================================================
        // GRID CONVERSION
        // =========================================================

        private Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.x / _tileWorldSize.x);
            int y = Mathf.FloorToInt(worldPosition.y / _tileWorldSize.y);
            return new Vector2Int(x, y);
        }

        private Vector3 GridToWorld(Vector2Int coordinate)
        {
            float x = (coordinate.x + 0.5f) * _tileWorldSize.x;
            float y = (coordinate.y + 0.5f) * _tileWorldSize.y;
            return new Vector3(x, y, _tileZ);
        }

        // =========================================================
        // VALIDATION
        // =========================================================
        private void ValidateReferences()
        {
            if (_camera == null) _camera = UnityEngine.Camera.main;
            if (_tileParent == null) _tileParent = transform;
        }

        private bool ValidateSettings()
        {
            if (_camera == null)
            {
                Debug.LogError("InfiniteGround: Camera reference is missing.", this);
                return false;
            }

            if (_tilePrefab == null)
            {
                Debug.LogError("InfiniteGround: Tile Prefab is missing.", this);
                return false;
            }

            if (_tileWorldSize.x <= 0f || _tileWorldSize.y <= 0f)
            {
                Debug.LogError("InfiniteGround: Tile World Size must be greater than zero.", this);
                return false;
            }

            if (!_camera.orthographic)
            {
                Debug.LogWarning("InfiniteGround works best with an Orthographic Camera.", this);
            }

            return true;
        }

        // =========================================================
        // DEBUG
        // =========================================================
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_showDebugGrid) return;
            if (_camera == null) return;
            if (_tileWorldSize.x <= 0f || _tileWorldSize.y <= 0f) return;

            Vector2Int cameraTile = WorldToGrid(_camera.transform.position);
            int range = _keepDistance + 1;
            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    Vector2Int coordinate = cameraTile + new Vector2Int(x, y);
                    Vector3 center = GridToWorld(coordinate);
                    Gizmos.DrawWireCube(center,
                        new Vector3(_tileWorldSize.x, _tileWorldSize.y, 0.1f)
                    );
                }
            }
        }
#endif
    }
}