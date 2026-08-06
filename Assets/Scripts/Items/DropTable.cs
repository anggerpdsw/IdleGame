using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using System.Linq;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Drop table manager for runtime access.
    /// </summary>
    public sealed class DropTableManager : MonoBehaviour
    {
        #region Singleton
        private static DropTableManager _instance;
        public static DropTableManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        #endregion

        #region Fields
        private readonly Dictionary<string, DropTableData> _tables = new();
        #endregion

        #region Initialization
        private void Initialize()
        {
            LoadTablesFromResources();
        }

        private void LoadTablesFromResources()
        {
            var jsonAsset = Resources.Load<TextAsset>("Data/dataDropTables");
            if (jsonAsset != null)
            {
                try
                {
                    var container = JsonConvert.DeserializeObject<DropTableContainer>(jsonAsset.text);
                    if (container?.Tables != null)
                    {
                        foreach (var table in container.Tables)
                        {
                            RegisterTable(table);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DropTableManager] Failed to load drop tables: {e.Message}");
                }
            }
        }
        #endregion

        #region Public API
        public void RegisterTable(DropTableData table)
        {
            if (table == null || string.IsNullOrEmpty(table.TableId)) return;
            _tables[table.TableId] = table;
        }

        public void UnregisterTable(string tableId) => _tables.Remove(tableId);

        public DropTableData GetTable(string tableId) => _tables.TryGetValue(tableId, out var table) ? table : null;

        public bool TryGetTable(string tableId, out DropTableData table) => _tables.TryGetValue(tableId, out table);

        public InventoryItem[] RollTable(string tableId, int tier, int wave, float luckBonus = 0f, int maxItems = 5)
        {
            if (_tables.TryGetValue(tableId, out var table))
            {
                return table.Roll(tier, wave, luckBonus, maxItems);
            }
            return Array.Empty<InventoryItem>();
        }

        public IReadOnlyList<DropTableData> GetAllTables() => _tables.Values.ToList();
        #endregion
    }

    // JSON container
    [Serializable]
    public class DropTableContainer
    {
        public List<DropTableData> Tables;
    }
}