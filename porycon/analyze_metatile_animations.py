#!/usr/bin/env python3
"""
Pokemon Emerald Metatile Animation Analyzer

This script analyzes metatile.bin files to identify which metatiles contain
animated tiles and in which layer positions.

Usage:
    python3 analyze_metatile_animations.py <path_to_metatiles.bin>
"""

import sys
import struct
from typing import List, Dict, Set, Tuple

# Animation tile ranges (from tileset_anims.c)
ANIMATION_RANGES = {
    'flower': (508, 511),           # 4 tiles, 4 frames
    'water': (432, 461),            # 30 tiles, 8 frames
    'sand_water_edge': (464, 473),  # 10 tiles, 8 frames
    'waterfall': (496, 501),        # 6 tiles, 4 frames
    'land_water_edge': (474, 477),  # 4 tiles, 4 frames (estimated)
}

def parse_tile_reference(word: int) -> Dict:
    """Parse a 16-bit tile reference into its components."""
    return {
        'tile_id': word & 0x3FF,          # Bits 0-9
        'palette': (word >> 10) & 0x7,    # Bits 10-12
        'hflip': (word >> 13) & 1,        # Bit 13
        'vflip': (word >> 14) & 1,        # Bit 14
    }

def get_animation_type(tile_id: int) -> str:
    """Determine which animation type a tile belongs to."""
    for anim_type, (start, end) in ANIMATION_RANGES.items():
        if start <= tile_id <= end:
            return anim_type
    return None

def analyze_metatile(data: bytes, metatile_id: int) -> Dict:
    """Analyze a single metatile for animated tiles."""
    offset = metatile_id * 16
    tiles = []

    for pos in range(8):
        byte_offset = offset + (pos * 2)
        if byte_offset + 1 >= len(data):
            break

        word = struct.unpack('<H', data[byte_offset:byte_offset+2])[0]
        tile_info = parse_tile_reference(word)
        tile_info['position'] = pos
        tile_info['layer'] = 'bottom' if pos < 4 else 'top'
        tile_info['animation'] = get_animation_type(tile_info['tile_id'])
        tiles.append(tile_info)

    return {
        'metatile_id': metatile_id,
        'tiles': tiles,
        'has_animation': any(t['animation'] for t in tiles),
        'animations': {t['animation'] for t in tiles if t['animation']}
    }

def analyze_tileset(filepath: str):
    """Analyze entire metatile.bin file."""
    with open(filepath, 'rb') as f:
        data = f.read()

    total_metatiles = len(data) // 16
    print(f"Analyzing {filepath}")
    print(f"Total size: {len(data)} bytes")
    print(f"Total metatiles: {total_metatiles}")
    print("=" * 80)
    print()

    # Statistics
    stats = {anim: {'count': 0, 'metatiles': set()} for anim in ANIMATION_RANGES.keys()}
    stats['total_animated'] = set()

    # Analyze each metatile
    animated_metatiles = []

    for mt_id in range(total_metatiles):
        analysis = analyze_metatile(data, mt_id)

        if analysis['has_animation']:
            animated_metatiles.append(analysis)
            stats['total_animated'].add(mt_id)

            for anim_type in analysis['animations']:
                stats[anim_type]['count'] += 1
                stats[anim_type]['metatiles'].add(mt_id)

    # Print statistics
    print("ANIMATION STATISTICS:")
    print("-" * 80)
    print(f"Total metatiles with animations: {len(stats['total_animated'])}")
    print()

    for anim_type, tile_range in ANIMATION_RANGES.items():
        count = len(stats[anim_type]['metatiles'])
        tile_start, tile_end = tile_range
        tile_count = tile_end - tile_start + 1
        print(f"{anim_type.replace('_', ' ').title():20s}: "
              f"{count:3d} metatiles (tiles {tile_start}-{tile_end}, {tile_count} tiles)")

    print()
    print("=" * 80)
    print()

    # Show example metatiles
    print("EXAMPLE ANIMATED METATILES:")
    print("-" * 80)

    examples_shown = {anim: 0 for anim in ANIMATION_RANGES.keys()}
    max_examples = 2

    for analysis in animated_metatiles[:20]:
        mt_id = analysis['metatile_id']

        # Only show examples for each animation type
        for anim_type in analysis['animations']:
            if examples_shown[anim_type] >= max_examples:
                continue

            print(f"\nMetatile {mt_id} ({anim_type.replace('_', ' ').title()}):")
            print("  Bottom layer:")
            for tile in analysis['tiles'][:4]:
                anim_marker = f" <- {tile['animation'].upper()}" if tile['animation'] else ""
                print(f"    [{tile['position']}] Tile {tile['tile_id']:3d} "
                      f"pal={tile['palette']} flip={tile['hflip']}{tile['vflip']}{anim_marker}")

            print("  Top layer:")
            for tile in analysis['tiles'][4:]:
                anim_marker = f" <- {tile['animation'].upper()}" if tile['animation'] else ""
                print(f"    [{tile['position']}] Tile {tile['tile_id']:3d} "
                      f"pal={tile['palette']} flip={tile['hflip']}{tile['vflip']}{anim_marker}")

            examples_shown[anim_type] += 1

    print()
    print("=" * 80)
    print()

    # Layer analysis
    print("LAYER USAGE ANALYSIS:")
    print("-" * 80)

    for anim_type in ANIMATION_RANGES.keys():
        metatiles = stats[anim_type]['metatiles']
        if not metatiles:
            continue

        bottom_only = 0
        top_only = 0
        both_layers = 0

        for mt_id in metatiles:
            analysis = analyze_metatile(data, mt_id)
            has_bottom = any(t['animation'] == anim_type and t['layer'] == 'bottom'
                           for t in analysis['tiles'])
            has_top = any(t['animation'] == anim_type and t['layer'] == 'top'
                        for t in analysis['tiles'])

            if has_bottom and has_top:
                both_layers += 1
            elif has_bottom:
                bottom_only += 1
            elif has_top:
                top_only += 1

        total = len(metatiles)
        print(f"\n{anim_type.replace('_', ' ').title()}:")
        print(f"  Bottom layer only: {bottom_only:3d} ({100*bottom_only//total if total else 0}%)")
        print(f"  Top layer only:    {top_only:3d} ({100*top_only//total if total else 0}%)")
        print(f"  Both layers:       {both_layers:3d} ({100*both_layers//total if total else 0}%)")

    print()
    print("=" * 80)

def main():
    if len(sys.argv) != 2:
        print("Usage: python3 analyze_metatile_animations.py <path_to_metatiles.bin>")
        print()
        print("Example:")
        print("  python3 analyze_metatile_animations.py pokeemerald/data/tilesets/primary/general/metatiles.bin")
        sys.exit(1)

    filepath = sys.argv[1]
    analyze_tileset(filepath)

    print("\nKEY FINDINGS:")
    print("-" * 80)
    print("1. Metatiles are 16 bytes: 8 tiles × 2 bytes per tile")
    print("2. Each tile reference encodes: tile_id[10], palette[3], hflip[1], vflip[1]")
    print("3. Positions 0-3 = bottom layer, 4-7 = top layer")
    print("4. Animated tiles can appear in EITHER or BOTH layers")
    print("5. Water animations typically use bottom layer")
    print("6. Flower animations typically use top layer")
    print()

if __name__ == '__main__':
    main()
