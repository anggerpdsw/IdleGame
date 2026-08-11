// Expected drop analysis (v3) — kills/wave by spawn curve, weighted per-material rates.
const fs = require('fs');
const en = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataEnemy.json', 'utf8').replace(/^﻿/, ''));
const items = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataItems.json', 'utf8').replace(/^﻿/, '')).Items;
const cfg = JSON.parse(fs.readFileSync('Assets/Resources/Data/dataWave.json', 'utf8'));

const start = cfg.spawning.baseSpawnInterval, min = cfg.spawning.minSpawnInterval, TARGET = cfg.spawning.minWaveSpawnDecay;
const decay = Math.pow(min / start, 1 / (TARGET - 1));
const killsPerWave = w => 30 / Math.max(start * Math.pow(decay, w - 1), min);
const killsCum = (a, b) => { let s = 0; for (let w = a; w <= b; w++) s += killsPerWave(w); return s; };

const K_W18 = killsCum(1, 18);               // ~398 (user's observation point)
const K_W10 = killsCum(10, 19);              // ~248
const K_TIER = killsCum(1, 350);             // per tier 1/2/3 identical (curve resets)

// spawn-weight shares
const totalW = en.enemies.reduce((s, e) => s + e.spawnWeight, 0);
const share = e => e.spawnWeight / totalW;

// per-material expected per 1000 kills (weighted over full enemy pool)
const agg = {};
for (const e of en.enemies) for (const d of e.dropItems) {
  const key = d.ItemId;
  agg[key] = agg[key] || { rate: 0, sources: [] };
  const avgQty = (d.MinCount + d.MaxCount) / 2;
  agg[key].rate += share(e) * (d.Weight / 100) * avgQty;
  agg[key].sources.push(`${e.id}(${d.Weight}%x${d.MinCount}-${d.MaxCount})`);
}
// per-kill total (all materials)
let perKill = 0; for (const k in agg) perKill += agg[k].rate;

const row = k => {
  const r = agg[k].rate; // per-kill probability
  return {
    per1000: +(r * 1000).toFixed(2),
    perWave: +(r * killsPerWave(18)).toFixed(2),
    per10w: +(r * K_W10).toFixed(2),
    perTier: +(r * K_TIER).toFixed(1)
  };
};

console.log('kills: W1-18=' + K_W18.toFixed(0), '| 10 waves(W10-19)=' + K_W10.toFixed(0), '| per tier=' + K_TIER.toFixed(0));
console.log('items/kill (weighted): ' + (perKill).toFixed(3));
console.log('T1 W1-18 expected total items: ' + (perKill * K_W18).toFixed(1));

console.log('\nMATERIAL | per1000kills | perWave(W18) | per10w(W18) | perTier | sources');
for (const k of Object.keys(agg).sort((a, b) => agg[b].rate - agg[a].rate)) {
  console.log([k, ...Object.values(row(k)), agg[k].sources.join('+')].join(' | '));
}

// Tier-visible pools: entry visible in tier t when MinTier <= t
for (let t = 1; t <= 3; t++) {
  let vis = {}, pkill = 0;
  for (const e of en.enemies) for (const d of e.dropItems) {
    if (d.MinTier > t) continue;
    vis[d.ItemId] = (vis[d.ItemId] || 0) + share(e) * (d.Weight / 100) * ((d.MinCount + d.MaxCount) / 2);
  }
  for (const k in vis) pkill += vis[k];
  console.log(`\n=== Tier ${t}-visible pool (${Object.keys(vis).length} mats, ${pkill.toFixed(4)} items/kill) ===`);
  console.log('  Expected W1-18 (' + (pkill * K_W18).toFixed(1) + ' total), 10 waves (' + (pkill * K_W10).toFixed(1) + '):');
  console.log('  ' + Object.keys(vis).sort((a, b) => vis[b] - vis[a]).map(k => `${k} ~${(vis[k] * K_W18).toFixed(1)}`).join(', '));
}

// drops that currently have NO source (should be empty of the dropable 34)
const used = new Set(Object.keys(agg));
const allIds = new Set(items.filter(i => i.Category === 3).map(i => i.Id));
console.log('\nDROPLESS materials:', [...allIds].filter(i => !used.has(i)).join(', '));