// Validate dropItems in dataEnemy.json against dataItems.json + v3 progression rules
const fs = require('fs');
const en = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataEnemy.json', 'utf8').replace(/^﻿/, ''));
const items = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataItems.json', 'utf8').replace(/^﻿/, '')).Items;

const matTier = {}; // ItemId -> tier 1..4 from rarity
for (const i of items.filter(i => i.Category === 3)) {
  matTier[i.Id] = i.ItemRarity; // Rarity 1=Common...4=Mythic-ish -> tier
}

// UNRESOLVED materials must NOT appear in drops (no clear source defined yet)
const BANNED = ['rubstone_powder', 'lumber_essence', 'compound_thread', 'vega_string',
  'steel_alloy', 'refined_steel', 'super_glue', 'colored_glue', 'compound_glue',
  'chocolate_additive', 'chocolate_syrup', 'edible_pigment', 'essence_of_hope'];

let errors = [], enemyCount = 0, entryCount = 0;
for (const e of en.enemies) {
  if (!e.id) { errors.push('enemy missing id'); continue; }
  enemyCount++;
  const drops = e.dropItems || [];
  if (!drops.length) { errors.push(`${e.id}: no dropItems`); continue; }

  // Pool size caps
  const isBoss = e.role === 'BOSS';
  const maxPool = isBoss ? 4 : e.exp >= 10 ? 3 : 2;
  if (drops.length > maxPool) errors.push(`${e.id}: pool ${drops.length} > ${maxPool} (exp ${e.exp})`);

  const seen = new Set();
  for (const d of drops) {
    entryCount++;
    if (!d.ItemId) { errors.push(`${e.id}: entry missing ItemId`); continue; }
    if (seen.has(d.ItemId)) errors.push(`${e.id}: duplicate ${d.ItemId}`); seen.add(d.ItemId);

    const tier = matTier[d.ItemId];
    if (!tier) { errors.push(`${e.id}: ${d.ItemId} NOT material in dataItems.json`); continue; }
    if (BANNED.includes(d.ItemId)) errors.push(`${e.id}: ${d.ItemId} is UNRESOLVED (banned from drops)`);

    // Progression gates
    if (tier >= 2 && e.exp < 7 && !isBoss) errors.push(`${e.id}: T2 ${d.ItemId} on exp ${e.exp} < 7`);
    if (tier >= 3 && e.exp < 12 && !isBoss) errors.push(`${e.id}: T3 ${d.ItemId} on exp ${e.exp} < 12`);
    if (tier >= 4) {
      if (!isBoss && !['Devourer', 'Dark Sage'].includes(e.id))
        errors.push(`${e.id}: T4 ${d.ItemId} outside BOSS/Devourer/DarkSage`);
      if (d.Weight > 2) errors.push(`${e.id}: T4 ${d.ItemId} chance ${d.Weight} > 2%`);
    }

    if (d.MinTier !== tier) errors.push(`${e.id}: ${d.ItemId} MinTier ${d.MinTier} != rarity ${tier}`);

    if (typeof d.Weight !== 'number' || d.Weight < 0 || d.Weight > 100) errors.push(`${e.id}/${d.ItemId}: Weight ${d.Weight} out of [0,100]`);
    if (d.MinCount < 1) errors.push(`${e.id}/${d.ItemId}: MinCount < 1`);
    if (d.MaxCount < d.MinCount) errors.push(`${e.id}/${d.ItemId}: Max < Min`);
  }
}

console.log('Enemies:', enemyCount, '| drop entries:', entryCount);
const dropless = Object.values(matTier).length
console.log('Materials used:', new Set(en.enemies.flatMap(e => (e.dropItems||[]).map(d => d.ItemId))).size);
if (errors.length) { console.error('FAIL:'); errors.forEach(x => console.error('  -', x)); process.exit(1); }
console.log('VALIDATION PASSED: ids, weights, pools, gates, banned-list');