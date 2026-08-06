// Updated EnemyData to include EXP reward
using System;
using UnityEngine;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Data definition for a single enemy type.
    /// Loaded from dataEnemy.json.
    /// </summary>
    [Serializable]
    public class EnemyData
    {
        [Tooltip("Unique identifier for this enemy type.")]
        public string id;

        public Role role;

        [Tooltip("Prefab name in Resources/Enemies/ folder.")]
        public string prefabName;

        [Tooltip("Distance from player at which enemy stops to attack.")]
        public float attackRange = 2f;

        [Tooltip("Jeda waktu antar serangan pertama dan selanjutnya.")]
        public float attackSpeed = 1f;

        [Tooltip("Damage dealt per attack.")]
        public float damage = 10f;

        [Tooltip("Total hit points.")]
        public float health = 100f;

        [Tooltip("Amount of damage reduced by defense. Count in EnemyAi→Initialize")]
        public float defense = 0f;

        [Tooltip("Movement speed in units per second.")]
        public float moveSpeed = 0.3f;

        [Tooltip("Spawn weight for random selection (higher = more common).")]
        public float spawnWeight = 1f;

        [Tooltip("Duration of knockback effect in seconds. Default stunt duration in EnemyAi→_knockbackDuration")]
        public float knockback = 0.2f;

        public float evasion;
        public Element element;

        // -------------------------------------------------------------------
        // New field: Permanent Account EXP reward given when this enemy dies.
        // -------------------------------------------------------------------
        [Tooltip("Permanent Account EXP granted on enemy death.")]
        public int exp = 0;

        [Tooltip("Drop table ID for loot generation. If empty, uses default drops.")]
        public string DropTableId;
    }

    /// <summary>
    /// Root container for the enemy database JSON.
    /// </summary>
    [Serializable]
    public class EnemyDatabase
    {
        public EnemyData[] enemies;
    }

    [Serializable]
    public class SlowEffect
    {
        public SlowSource Source;
        public SlowType Type;
        public float Percent;
    }

    [Serializable]
    public class DefenseBreakEffect
    {
        public DefenseBreakSource Source;
        public DefenseBreakType Type;
        public float Percent;
    }
}
