using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class GameStateData
    {
        public DateTime lastSaveTime = DateTime.Now;
        public float totalPlayTime = 0f;
        public int dailyGemsEarned = 0;
        public string dailyResetDate = "";
        public Dictionary<string, Dictionary<string, Dictionary<string, long>>> totalEnemiesKilled = new();
    }
}
