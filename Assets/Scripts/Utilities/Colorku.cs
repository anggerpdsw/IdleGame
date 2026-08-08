using UnityEngine;

namespace IdleDefenseSurvival
{
    public static class GameColors
    {
        // ===== Base palette =====
        public static readonly Color gold  = new Color32(255, 215, 0, 255);
        public static readonly Color green  = new Color32(22, 163, 74, 255);
        public static readonly Color yellow = new Color32(245, 158, 11, 255);
        public static readonly Color red    = new Color32(220, 38, 38, 255);
        public static readonly Color blue   = new Color32(37, 99, 235, 255);
        public static readonly Color white  = new Color32(248, 250, 252, 255);
        public static readonly Color empty = new Color32(80, 110, 170, 255);
        public static readonly Color orange = new Color32(234, 88, 12, 255);
        public static readonly Color orangered = new Color32(255, 69, 0, 255);
        public static readonly Color cyan = new Color32(6, 182, 212, 255);
        public static readonly Color darkgray = new Color32(30, 30, 30, 255);
        public static readonly Color gray = new Color32(107, 114, 128, 255);
        public static readonly Color pink = new Color32(255, 20, 147, 255);
        public static readonly Color purple = new Color32(147, 51, 234, 255);

        // ===== Item rarity =====
        public static readonly Color commonGray      = new Color32(179, 179, 179, 255);
        public static readonly Color uncommonGreen   = new Color32(51, 204, 51, 255);
        public static readonly Color rareBlue        = new Color32(51, 102, 255, 255);
        public static readonly Color epicPurple      = new Color32(179, 51, 255, 255);
        public static readonly Color legendaryOrange = new Color32(255, 153, 26, 255);
        public static readonly Color mythicPink      = new Color32(255, 51, 153, 255);
        public static readonly Color ancientPurple   = new Color32(128, 51, 255, 255);
        public static readonly Color divineGold      = new Color32(255, 230, 51, 255);

        // ===== Gem types =====
        public static readonly Color gemRuby      = new Color32(230, 26, 26, 255);
        public static readonly Color gemSapphire  = new Color32(26, 77, 230, 255);
        public static readonly Color gemEmerald   = new Color32(26, 204, 77, 255);
        public static readonly Color gemTopaz     = new Color32(230, 179, 26, 255);
        public static readonly Color gemAmethyst  = new Color32(179, 26, 230, 255);
        public static readonly Color gemDiamond   = new Color32(230, 230, 255, 255);
        public static readonly Color gemOnyx      = new Color32(51, 26, 77, 255);
        public static readonly Color gemPearl     = new Color32(255, 230, 230, 255);
        public static readonly Color gemOpal      = new Color32(204, 102, 204, 255);
        public static readonly Color gemPrismatic = new Color32(255, 128, 255, 255);

        // ===== Secondary stat colors =====
        public static readonly Color statLifeSteal          = new Color32(204, 51, 102, 255);
        public static readonly Color statBossDamage         = new Color32(255, 26, 26, 255);
        public static readonly Color statEliteDamage        = new Color32(255, 77, 51, 255);
        public static readonly Color statDamagePerRange     = new Color32(255, 102, 51, 255);
        public static readonly Color statMoveSpeed           = new Color32(255, 230, 51, 255);
        public static readonly Color statCooldownReduction  = new Color32(230, 179, 51, 255);
        public static readonly Color statGoldGain           = new Color32(255, 217, 0, 255);
        public static readonly Color statDropRate           = new Color32(230, 153, 26, 255);
        public static readonly Color statInterestWave       = new Color32(128, 204, 128, 255);
        public static readonly Color statHitRate            = new Color32(102, 230, 255, 255);
        public static readonly Color statMetal              = new Color32(179, 191, 204, 255);
        public static readonly Color statWood               = new Color32(102, 217, 115, 255);
        public static readonly Color statFire               = new Color32(255, 115, 51, 255);
        public static readonly Color statWater              = new Color32(77, 153, 255, 255);
        public static readonly Color statEarth              = new Color32(184, 128, 77, 255);
        public static readonly Color statWind               = new Color32(102, 242, 242, 255);
        public static readonly Color statRange              = new Color32(153, 77, 255, 255);
        public static readonly Color statBounceChance       = new Color32(128, 204, 255, 255);
        public static readonly Color statBounceCount        = new Color32(102, 179, 255, 255);
        public static readonly Color statMultiShootChance   = new Color32(204, 77, 255, 255);
        public static readonly Color statMultiShootCount    = new Color32(179, 51, 255, 255);
        public static readonly Color statKnockbackChance    = new Color32(230, 102, 153, 255);
        public static readonly Color statStunChance         = new Color32(204, 77, 128, 255);
        public static readonly Color statStunDuration       = new Color32(179, 51, 102, 255);

        // ===== Daily reward =====
        public static readonly Color dailyClaimableGreen = new Color32(56, 125, 56, 255);
        public static readonly Color dailyClaimedGreen   = new Color32(66, 97, 61, 255);
        public static readonly Color dailyDefault        = new Color32(41, 41, 51, 255);
        public static readonly Color waveInterYellow     = new Color32(255, 204, 51, 255);

        // ===== Debug / Gizmos (alpha via WithAlpha) =====
        public static readonly Color debugBlueGizmo      = new Color32(0, 128, 255, 255);
        public static readonly Color debugYellowGizmo    = new Color32(255, 255, 0, 255);
        public static readonly Color debugCyanGizmo      = new Color32(0, 255, 255, 255);
        public static readonly Color debugAtkRangeCyan   = new Color32(26, 242, 255, 255);
        public static readonly Color debugOrangeGizmo    = new Color32(255, 128, 0, 255);
        public static readonly Color debugDarkCyan       = new Color32(0, 179, 179, 255);
        public static readonly Color debugLightningGold  = new Color32(255, 204, 0, 255);
        public static readonly Color debugLightningPurple = new Color32(128, 0, 255, 255);
        public static readonly Color debugDarkPurple     = new Color32(51, 0, 128, 255);

        /// <summary>Returns a copy of the color with the given alpha.</summary>
        public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);
    }

}
