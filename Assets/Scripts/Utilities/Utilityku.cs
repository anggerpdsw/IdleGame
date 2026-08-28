using System.Collections.Generic;
using IdleDefenseSurvival;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using UnityEngine;

public static class Utilityku
{
    public static bool Chance(float chancePercent) => Random.Range(0f, 100f) < chancePercent;
    public static float FinalDamage(float damage, float defense, float armorPenetration = 0f)
    {
        float effectiveDefense = defense - armorPenetration;
        return damage * (100f / (100f + effectiveDefense));
    }

    public static int FinalDefense(Role role, float health)
    {
        int defense = Mathf.RoundToInt(health * 0.013f); // Beast
        switch(role)
        {
            case Role.Fighter:
                defense = Mathf.RoundToInt(defense * 1.1f);
                break;
            case Role.Tank:
                defense = Mathf.RoundToInt(defense * 1.35f);
                break;
            case Role.Golem:
                defense = Mathf.RoundToInt(defense * 1.15f);
                break;
            case Role.Caster:
                defense = Mathf.RoundToInt(defense * 0.75f);
                break;
            case Role.Ranger:
                defense = Mathf.RoundToInt(defense * 0.7f);
                break;
            case Role.Agile:
                defense = Mathf.RoundToInt(defense * 0.8f);
                break;
            case Role.BOSS:
                defense = Mathf.RoundToInt(defense * 1.55f);
                break;
        }
        return defense;
    }

    private static readonly (long Value, string Suffix)[] NumberUnits =
    {
        (1_000_000_000_000_000_000L, "F"),
        (1_000_000_000_000_000L, "E"), (1_000_000_000_000L, "D"),
        (1_000_000_000L, "C"), (1_000_000L, "B"), (1_000L, "A"),
    };
    public static string FormatNumber(long amount)
    {
        foreach (var (value, suffix) in NumberUnits)
        {
            if (amount < value) continue;
            double formatted = amount / (double)value;
            return $"{formatted:0.##}{suffix}";
        }
        return amount.ToString("N0");
    }
    public static string FormatDuration(System.TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        if (time.TotalMinutes >= 1)
            return $"{(int)time.TotalMinutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }
    
    public static long WaveBonusInterest(CurrencyType type, long earned, float percent, int CurrentTier)
    {
        long bonus = Mathf.RoundToInt(earned * (percent / 100f));
        float baseMax = 100f;
        float tierIncrease = 25f;
        switch(type)
        {
            case CurrencyType.Gold:
                baseMax = 1000f;
                tierIncrease = 250f;
                break;
            case CurrencyType.Meat:
                baseMax = 100f;
                tierIncrease = 25f;
                break;
        }
        baseMax =+ baseMax * (CurrentTier - 1) * tierIncrease;
        return (long)Mathf.Min(bonus, baseMax);
    }

    public static long WaveBonusVictory(long earned, int currentTier, int currentWave)
    {
        float tierMultiplier = 1f + (currentTier - 1) * 0.08f;
        float waveProgress = Mathf.Clamp01(currentWave / 350f);
        float percent = Mathf.Lerp(0.01f, 0.05f, waveProgress);
        // Bonus khusus saat clear semua wave
        if (currentWave >= 350) percent += 0.03f;
        long bonus = (long)(earned * percent * tierMultiplier);
        return Mathf.RoundToInt(bonus / 1000f) * 1000L;
    }

    public static float WaveMultiplier(float type, int CurrentWave, int _maxWave)
    {
        return Mathf.Pow(type,  Mathf.Min(CurrentWave, _maxWave) - 1);
    }
    public static float WaveDecayCalculate(float start, float end, int targetWave)
    {
        return Mathf.Pow(end / start, 1f / (targetWave - 1));
    }

    public static Vector3 WorldToScreen(Vector3 worldPosition)
        => UnityEngine.Camera.main.WorldToScreenPoint(worldPosition);

    public static void PlaySfx(AudioSource source, AudioClip clip, float volume = 1f)
    {
        if (clip == null || source == null) return;
        SettingsController settings = SettingsController.Instance;

        if (settings != null)
        {
            if (!settings.SfxMuted) return;
            volume *= settings.SfxVolume;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
    
    private static readonly Dictionary<(Element, Element), float> _elementTable = new()
    {
        // Counter (150%)
        {(Element.Metal,      Element.Wind),      1.5f},
        {(Element.Wood,       Element.Metal),     1.5f},
        {(Element.Fire,       Element.Wood),      1.5f},
        {(Element.Water,      Element.Fire),      1.5f},
        {(Element.Earth,      Element.Water),     1.5f},
        {(Element.Lightning,  Element.Earth),     1.5f},
        {(Element.Wind,       Element.Lightning), 1.5f},

        // Weak (50%)
        {(Element.Metal,      Element.Wood),      0.5f},
        {(Element.Wood,       Element.Fire),      0.5f},
        {(Element.Fire,       Element.Water),     0.5f},
        {(Element.Water,      Element.Earth),     0.5f},
        {(Element.Earth,      Element.Lightning), 0.5f},
        {(Element.Lightning,  Element.Wind),      0.5f},
        {(Element.Wind,       Element.Metal),     0.5f},
    };
    /// <summary>
    /// Layer 3: per-element damage bonus (percent stat from equipment/card/buff).
    /// Maps an Element to its percent SkillType. Returns 1x when no element.
    /// </summary>
    public static float ElementBonus(Element element)
    {
        if (element == Element.None) return 1f;
        if (PlayerStatsManager.Instance == null) return 1f;

        SkillType stat = element switch
        {
            Element.Metal     => SkillType.MetalDamageBonus,
            Element.Wood      => SkillType.WoodDamageBonus,
            Element.Fire      => SkillType.FireDamageBonus,
            Element.Water     => SkillType.WaterDamageBonus,
            Element.Earth     => SkillType.EarthDamageBonus,
            Element.Lightning => SkillType.LightningDamageBonus,
            Element.Wind      => SkillType.WindDamageBonus,
            _ => SkillType.None
        };
        if (stat == SkillType.None) return 1f;

        float bonus = PlayerStatsManager.Instance.GetStat(stat);
        // Percent stat: +18 Fire Damage → 1.18x
        return (100f + bonus) * 0.01f;
    }

    public static float ElementMultiplier(Element attacker, Element defender)
    {
        // Non-elemental attack
        if (attacker == Element.None || defender == Element.None) return 1f;

        // Sama element = resist
        if (attacker == defender) return 0.75f;

        // Counter / Weak
        if (_elementTable.TryGetValue((attacker, defender), out float multiplier)) return multiplier;

        // Semua element lain
        return 0.75f;
    }
    private static readonly Element[] _elements =
    {
        Element.None, 
        Element.Metal, Element.Wood, 
        Element.Fire, Element.Water, 
        Element.Earth, Element.Lightning, 
        Element.Wind
    };
    public static Element RandomElement()
    {
        return _elements[Random.Range(0, _elements.Length)];
    }

    public static string ToItemId(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return System.Text.RegularExpressions.Regex
            .Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_")
            .Trim('_');
    }

}