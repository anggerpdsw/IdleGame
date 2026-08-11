// v3 REBALANCE — final. Small pools, progression gates, ~0.2-0.25 items/kill.
// Rules: Assets/Resources/Data/Design/EnemyDrop_Design.md §3-4
// Pool: exp 1-9 → ≤2 entries | exp 10+ → ≤3 | BOSS → 4
// Gates: T2 (R2) exp>=7 | T3 (R3) exp>=12 | T4 (R4): elemental_essence/high_alloy_steel BOSS only,
//        dream_of_reminiscence only Devourer+Dark Sage. UNRESOLVED mats banned from drops.
// Rates per band (per-kill expected in parens):
//   exp 1-6: 22% × qty1 (0.22) | exp 7-9: 18%×1.3 + 7% (0.30) | exp 10-13: 16%×1.3 + 8% + 3% (0.32)
//   exp 14+: 15%×1.3 + 8% + 3% (0.30) | BOSS: 25%×2.5 + 12%×1.5 + 6% + 1% (0.92)
const fs = require('fs');
const path = 'Assets/Resources/Data/dataEnemy.json';
const db = JSON.parse(fs.readFileSync(path, 'utf8').replace(/^﻿/, ''));
const raw = fs.readFileSync(path, 'utf8');
const indent = raw.match(/\n( +)"id"/)?.[1] || '  ';

// MinTier = material ItemRarity (tier gate mirrors rarity tier)
const itemDb = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataItems.json', 'utf8').replace(/^﻿/, ''));
const rarityOf = {};
for (const i of itemDb.Items) rarityOf[i.Id] = i.ItemRarity || 1;

// Global rate multiplier — 0.0625 = quartered twice (balance passes, 2026-08-11)
const RATE_MULT = 0.0625;

// [item, chance, min, max] — chance = percent 0-100
const D = {
  // ============ BOSS (4 entries, 1% T4) ============
  "Infernal Lord": [["coal", 25, 2, 3], ["charcoal", 15, 1, 2], ["extruded_charcoal", 6, 1, 1], ["elemental_essence", 1, 1, 1]],
  "Stone Titan":  [["rock", 25, 2, 3], ["granite", 15, 1, 2], ["corundum_powder", 6, 1, 1], ["elemental_essence", 1, 1, 1]],
  "Red-Eye Tank": [["iron_dust", 25, 2, 3], ["pig_iron", 15, 1, 2], ["high_carbon_steel", 10, 1, 1], ["high_alloy_steel", 1, 1, 1]],

  // ============ Golem (exp 13-16, 3 entries, 15/8/3) ============
  "Magma Golem":  [["coal", 15, 1, 2], ["anthracite", 8, 1, 1], ["extruded_charcoal", 3, 1, 1]],
  "Devourer":     [["rock", 15, 1, 2], ["stone_dust", 8, 1, 2], ["dream_of_reminiscence", 2, 1, 1]],
  "Ice Golem":    [["cotton_thread", 15, 1, 2], ["silk_thread", 8, 1, 1], ["azureworm_silk", 3, 1, 1]],
  "Rock Golem":   [["rock", 16, 1, 2], ["granite", 8, 1, 1], ["corundum_powder", 4, 1, 1]],
  "Thorn Hulk":   [["disposed_logs", 15, 1, 2], ["fine_lumber", 8, 1, 1], ["high_grade_lumber", 4, 1, 1]],
  "Crystal Ogre": [["rock", 15, 1, 2], ["granite", 8, 1, 1], ["corundum_powder", 3, 1, 1]],

  // ============ Tank (exp 11-14) ============
  "Log Mimic":    [["disposed_logs", 16, 1, 2], ["fine_lumber", 8, 1, 1]],
  "Demon Pup":    [["coal", 16, 1, 2], ["anthracite", 8, 1, 1]],
  "Steel Wolf":   [["iron_dust", 16, 1, 2], ["high_carbon_steel", 8, 1, 1]],
  "Sand Worm":    [["rock", 18, 1, 2], ["sandstone", 8, 1, 1]],
  "Frost Beetle": [["cotton_thread", 16, 1, 2], ["silk_thread", 8, 1, 1]],
  "Jet Beetle":   [["cotton_thread", 16, 1, 2], ["thick_thread", 8, 1, 1]],
  "Purple Ooze":  [["organic_glue", 15, 1, 2], ["strong_glue", 8, 1, 1]],
  "Yeti":         [["cotton_thread", 15, 1, 2], ["silk_thread", 8, 1, 1]],
  "Mecha Crawler":[["iron_dust", 15, 1, 2], ["high_carbon_steel", 8, 1, 1]],
  "Lava Beast":   [["coal", 16, 1, 2], ["anthracite", 8, 1, 1]],
  "Boulderling":  [["rock", 18, 1, 2], ["sandstone", 8, 1, 1]],

  // ============ Caster (exp 10-12) ============
  "Forest Archer":[["disposed_logs", 16, 1, 2], ["fine_lumber", 8, 1, 1]],
  "Bone Archer":  [["iron_dust", 16, 1, 2], ["high_carbon_steel", 6, 1, 1]],
  "Arcane Warlock":[["rock", 16, 1, 2], ["stone_dust", 8, 1, 2]],
  "Ice Mage":     [["cotton_thread", 16, 1, 2], ["silk_thread", 8, 1, 1]],
  "Void Mage":    [["rock", 16, 1, 2], ["stone_dust", 8, 1, 1]],
  "Fire Shaman":  [["coal", 16, 1, 2], ["anthracite", 6, 1, 1]],
  "Dark Sage":    [["rock", 15, 1, 2], ["iron_dust", 8, 1, 1], ["dream_of_reminiscence", 1.5, 1, 1]],

  // ============ Ranger (exp 5-10) ============
  "Archer Elf":   [["disposed_logs", 18, 1, 2], ["rough_lumber", 8, 1, 1]],
  "Cave Bat":     [["cotton_thread", 18, 1, 2], ["rock", 6, 1, 1]],
  "Thorn Sprite": [["disposed_logs", 20, 1, 2], ["logs", 8, 1, 1]],
  "Buzz Drone":   [["cotton_thread", 16, 1, 2], ["thick_thread", 6, 1, 1]],

  // ============ Beast (exp 1-8) — T1 ONLY, ≤2 entries ============
  "Ice Wisp":     [["cotton_thread", 22, 1, 1]],
  "Fire Wisp":    [["coal", 22, 1, 1], ["coal_dust", 8, 1, 1]],
  "Bat Fiend":    [["cotton_thread", 20, 1, 2]],
  "Crimson Imp":  [["coal", 20, 1, 2]],
  "Void Puff":    [["rock", 22, 1, 1]],
  "Goblin Grunt": [["rock", 22, 1, 2], ["stone_dust", 7, 1, 1]],
  "Flame Core":   [["coal", 22, 1, 2], ["coal_dust", 8, 1, 1]],
  "Hornet":       [["cotton_thread", 22, 1, 2]],
  "Skeleton Raider":[["iron_dust", 22, 1, 2]],
  "Guardian Golem":[["iron_dust", 18, 1, 2], ["high_carbon_steel", 6, 1, 1]],

  // ============ Agile (exp 4-9) ============
  "Ice Sprite":   [["cotton_thread", 22, 1, 1]],
  "Goblin Scout": [["rock", 22, 1, 2], ["stone_dust", 7, 1, 1]],
  "Watcher":      [["iron_dust", 20, 1, 2]],
  "Shadow Stalker":[["rock", 18, 1, 2], ["iron_dust", 7, 1, 1]],
  "Rogue Imp":    [["rock", 20, 1, 2], ["iron_dust", 6, 1, 1]],
  "Ash Assassin": [["rock", 18, 1, 2], ["iron_dust", 8, 1, 1]],
  "Night Rogue":  [["rock", 20, 1, 2], ["iron_dust", 6, 1, 1]],
  "Shadow Ninja": [["rock", 18, 1, 2], ["iron_dust", 8, 1, 1]],

  // ============ Fighter (exp 5-12) ============
  "Thorn Golem":  [["disposed_logs", 18, 1, 2], ["rough_lumber", 8, 1, 1]],
  "Skeleton Guard":[["iron_dust", 18, 1, 2], ["high_carbon_steel", 5, 1, 1]],
  "Sentinel":     [["iron_dust", 20, 1, 2]],
  "Night Bat":    [["cotton_thread", 18, 1, 2], ["rock", 6, 1, 1]],
  "Green Slime":  [["organic_glue", 22, 1, 2]],
  "Embershard Golem":[["coal", 18, 1, 2], ["charcoal", 8, 1, 1]],
  "Aqua Slime":   [["organic_glue", 20, 1, 2], ["cotton_thread", 7, 1, 1]],
  "Dark Orb":     [["rock", 18, 1, 2], ["stone_dust", 7, 1, 1]],
  "Stone Rhino":  [["rock", 20, 1, 2], ["granite", 6, 1, 1]],
  "Void Eye":     [["rock", 18, 1, 2], ["iron_dust", 7, 1, 1]],
  "Swamp Brute":  [["disposed_logs", 16, 1, 2], ["logs", 8, 1, 1]],
  "Fire Beetle":  [["coal", 18, 1, 2], ["charcoal", 8, 1, 1]],
  "Cannon Bug":   [["cotton_thread", 18, 1, 2], ["thick_thread", 6, 1, 1]],
  "Moss Slime":   [["organic_glue", 18, 1, 2], ["concentrated_glue", 8, 1, 1]],
  "Stone Golem":  [["rock", 18, 1, 2], ["granite", 6, 1, 1]],
  "Burrower":     [["rock", 20, 1, 2], ["stone_dust", 7, 1, 1]],
  "Crystal Cluster":[["rock", 16, 1, 2], ["stone_dust", 8, 1, 1]],
  "Venom Lizard": [["organic_glue", 16, 1, 2], ["concentrated_glue", 5, 1, 1]],
  "Corrupted Drake":[["rock", 18, 1, 2], ["iron_dust", 7, 1, 1]],
  "Shield Kobold":[["iron_dust", 18, 1, 2], ["pig_iron", 8, 1, 1]],
  "Wyvern":       [["cotton_thread", 16, 1, 2], ["silk_thread", 6, 1, 1]],
  "Club Orc":     [["rock", 20, 1, 2], ["stone_dust", 7, 1, 1]],
  "Mushroom Beast":[["disposed_logs", 16, 1, 2], ["fine_lumber", 8, 1, 1]],
  "Goblin Ranger": [["iron_dust", 16, 1, 2], ["high_carbon_steel", 6, 1, 1]],
  "Bramble Chief":[["disposed_logs", 16, 1, 2], ["fine_lumber", 8, 1, 1]],
  "Orc Bruiser":  [["rock", 20, 1, 2], ["stone_dust", 7, 1, 1]],
  "Cyclops":      [["rock", 18, 1, 2], ["granite", 8, 1, 1]],
  "Water Spirit": [["cotton_thread", 16, 1, 2], ["silk_thread", 8, 1, 1]],
  "Skeleton Duelist":[["iron_dust", 18, 1, 2], ["pig_iron", 8, 1, 1]],
  "Frost Adept":  [["cotton_thread", 16, 1, 2], ["silk_thread", 8, 1, 1]],
  "Lizard Hunter":[["rock", 16, 1, 2], ["iron_dust", 7, 1, 1]],
  "Rock Mimic":   [["rock", 20, 1, 2], ["granite", 8, 1, 1]],
  "Ice Wolf":     [["cotton_thread", 16, 1, 2], ["silk_thread", 8, 1, 1]],
  "Elder Ooze":   [["organic_glue", 16, 1, 2], ["strong_glue", 8, 1, 1]],
  "Hydraling":    [["rock", 16, 1, 2], ["organic_glue", 6, 1, 1], ["iron_dust", 6, 1, 1]],
};

let missing = [];
for (const e of db.enemies) {
  const drops = D[e.id];
  if (!drops) { missing.push(e.id); continue; }
  e.dropItems = drops.map(([ItemId, Weight, MinCount, MaxCount]) => ({
    ItemId, MinCount, MaxCount, Weight: +(Weight * RATE_MULT).toFixed(3), MinTier: rarityOf[ItemId] || 1
  }));
}
if (missing.length) { console.error('ERROR no mapping for:', missing.join(', ')); process.exit(1); }

fs.writeFileSync(path, JSON.stringify(db, null, indent) + '\n');
console.log('OK: v3 dropItems written to', db.enemies.length, 'enemies');