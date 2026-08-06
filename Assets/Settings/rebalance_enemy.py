#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
rebalance_enemy.py

Mereduksi nilai *damage* dan *health* pada semua enemy yang berada
dalam file JSON dengan struktur yang sama seperti yang kamu kirim.
Hanya field “damage” dan “health” yang diubah; semua field lain
tetap apa adanya.

   damage  → ceil(original_damage  * 0.65)   # ~‑35 % pengurangan
   health  → ceil(original_health  * 0.75)   # ~‑25 % pengurangan

Penggunaan
---------
    python rebalance_enemy.py dataEnemy.json               # menimpa file lama
    python rebalance_enemy.py dataEnemy.json out.json      # menyimpan ke file baru
    python rebalance_enemy.py dataEnemy.json out.json --damage-factor 0.60 --health-factor 0.70
"""

import argparse
import json
import math
import sys
from pathlib import Path


# --------------------------------------------------------------------------- #
#  Helper functions
# --------------------------------------------------------------------------- #
def _scale_value(value: int, factor: float) -> int:
    """Mengalikan nilai dengan *factor* dan membulatkan ke atas."""
    return math.ceil(value * factor)


def rebalance_enemy_stats(
    enemy: dict,
    dmg_factor: float = 0.65,
    hp_factor: float = 0.75,
) -> dict:
    """
    Kembalikan dictionary yang sama dengan *enemy* namun dengan field
    ``damage`` dan ``health`` yang sudah diperkecil.
    """
    # Salin dulu supaya tidak mengubah dict asal (memudahkan debugging)
    new_enemy = enemy.copy()

    # Damage
    if "damage" in enemy and isinstance(enemy["damage"], (int, float)):
        new_enemy["damage"] = _scale_value(int(enemy["damage"]), dmg_factor)

    # Health
    if "health" in enemy and isinstance(enemy["health"], (int, float)):
        new_enemy["health"] = _scale_value(int(enemy["health"]), hp_factor)

    return new_enemy


def process_file(
    in_path: Path,
    out_path: Path,
    dmg_factor: float = 0.65,
    hp_factor: float = 0.75,
) -> None:
    """Baca *in_path*, ubah nilai damage/health, lalu tulis ke *out_path*."""
    try:
        data = json.loads(in_path.read_text(encoding="utf-8"))
    except Exception as exc:
        sys.exit(f"❌  Gagal membaca JSON dari {in_path!s}: {exc}")

    if "enemies" not in data or not isinstance(data["enemies"], list):
        sys.exit("❌  JSON harus memiliki key 'enemies' yang berupa list.")

    # Re‑balance setiap enemy
    new_enemies = [
        rebalance_enemy_stats(e, dmg_factor, hp_factor) for e in data["enemies"]
    ]
    data["enemies"] = new_enemies

    # Tulis kembali (pretty‑printed, indent=2)
    out_path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    print(f"✅  Data telah direbalance dan disimpan ke {out_path!s}")


# --------------------------------------------------------------------------- #
#  CLI
# --------------------------------------------------------------------------- #
def build_cli() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Kurangi damage & health pada semua enemy di file JSON."
    )
    parser.add_argument(
        "infile",
        type=Path,
        help="Path ke file JSON sumber (mis: dataEnemy.json)",
    )
    parser.add_argument(
        "outfile",
        type=Path,
        nargs="?",
        help="Path output. Jika tidak diberikan file sumber akan ditimpa.",
    )
    parser.add_argument(
        "--damage-factor",
        type=float,
        default=0.65,
        help="Faktor pengali untuk damage (default 0.65 → ‑35 %%).",
    )
    parser.add_argument(
        "--health-factor",
        type=float,
        default=0.75,
        help="Faktor pengali untuk health (default 0.75 → ‑25 %%).",
    )
    return parser


def main() -> None:
    args = build_cli().parse_args()

    in_path = args.infile.resolve()
    out_path = args.outfile.resolve() if args.outfile else in_path

    if not in_path.is_file():
        sys.exit(f"❌  File tidak ditemukan: {in_path!s}")

    # Keamanan: konfirmasi jika akan menimpa file yang berbeda
    if out_path != in_path and out_path.exists():
        answer = input(f"File {out_path!s} sudah ada – timpa? [y/N] ")
        if answer.lower() != "y":
            sys.exit("❌  Dibatalkan oleh pengguna.")

    process_file(
        in_path,
        out_path,
        dmg_factor=args.damage_factor,
        hp_factor=args.health_factor,
    )


if __name__ == "__main__":
    main()