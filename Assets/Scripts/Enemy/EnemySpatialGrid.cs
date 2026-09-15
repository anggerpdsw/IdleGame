using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Enemy
{
    /// <summary>
    /// O(1) spatial grid for enemy separation.
    /// Static utility - no state per enemy, shared dictionary across all enemies.
    /// Cell size: 2 world units. Lookup: 3x3 neighbor cells.
    /// Hash: Cantor pairing for negative coordinate support.
    /// </summary>
    public static class EnemySpatialGrid
    {
        public const int CellSize = 2; // world units per cell - MUST NOT CHANGE
        private static readonly Dictionary<int, List<EnemyAi>> Grid = new();

        /// <summary>
        /// Cantor pairing hash for unique 2D grid cell ID.
        /// Supports negative coordinates.
        /// Identical to original GetGridHash implementation.
        /// </summary>
        public static int GetHash(Vector2Int cell)
        {
            int x = cell.x >= 0 ? cell.x * 2 : -cell.x * 2 - 1;
            int y = cell.y >= 0 ? cell.y * 2 : -cell.y * 2 - 1;
            return (x + y) * (x + y + 1) / 2 + y;
        }

        /// <summary>
        /// Register enemy to grid cell. Called from EnemyAi.RegisterWithGrid.
        /// </summary>
        public static void Register(EnemyAi enemy, Vector2Int cell)
        {
            int hash = GetHash(cell);
            if (!Grid.TryGetValue(hash, out var list))
            {
                list = new List<EnemyAi>();
                Grid[hash] = list;
            }
            if (!list.Contains(enemy))
                list.Add(enemy);
        }

        /// <summary>
        /// Unregister enemy from grid cell. Called from EnemyAi.UnregisterFromGrid.
        /// </summary>
        public static void Unregister(EnemyAi enemy, Vector2Int cell)
        {
            int hash = GetHash(cell);
            if (Grid.TryGetValue(hash, out var list))
            {
                list.Remove(enemy);
                if (list.Count == 0) Grid.Remove(hash);
            }
        }

        /// <summary>
        /// Get all enemies in 3x3 neighbor cells around given cell.
        /// Returns list for iteration - caller must filter self.
        /// Zero allocation when no neighbors in range.
        /// </summary>
        public static List<EnemyAi> GetNeighborsInCells(Vector2Int cell)
        {
            var result = new List<EnemyAi>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int neighborCell = new(cell.x + dx, cell.y + dy);
                    int hash = GetHash(neighborCell);
                    if (Grid.TryGetValue(hash, out var list))
                        result.AddRange(list);
                }
            }
            return result;
        }

        /// <summary>
        /// Debug: get total enemy count across all grid cells.
        /// </summary>
        public static int GetTotalEnemyCount()
        {
            int total = 0;
            foreach (var list in Grid.Values)
                total += list.Count;
            return total;
        }

        /// <summary>
        /// Debug: get active cell count.
        /// </summary>
        public static int GetActiveCellCount() => Grid.Count;
    }
}
