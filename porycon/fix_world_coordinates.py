#!/usr/bin/env python3
"""
Fix world coordinate offsets in hoenn.world file.

This script corrects the 2-tile (32-pixel) offset issues in map connections.
"""

import json
from pathlib import Path


def fix_world_coordinates(world_path):
    """Fix coordinate offsets in world file."""
    with open(world_path, 'r') as f:
        world = json.load(f)

    print(f"Loaded world file with {len(world['maps'])} maps")

    # Find maps that need fixing
    maps_by_name = {}
    for map_entry in world['maps']:
        name = Path(map_entry['fileName']).stem
        maps_by_name[name] = map_entry

    fixes_applied = []

    # Fix 1: dewford_town and route107 should have same Y coordinate
    if 'dewford_town' in maps_by_name and 'route107' in maps_by_name:
        dewford = maps_by_name['dewford_town']
        route107 = maps_by_name['route107']

        print(f"\nBefore fix:")
        print(f"  dewford_town: x={dewford['x']}, y={dewford['y']}")
        print(f"  route107: x={route107['x']}, y={route107['y']}")
        print(f"  Y difference: {abs(dewford['y'] - route107['y'])} pixels")

        if abs(dewford['y'] - route107['y']) == 32:
            # Route107 is 32 pixels too high, move it down
            route107['y'] = dewford['y']
            fixes_applied.append(f"route107: adjusted Y from {route107['y'] - 32} to {route107['y']}")
            print(f"\n✓ Fixed route107 Y coordinate to match dewford_town")

    # Fix 2: route114 and route115 offset
    if 'route114' in maps_by_name and 'route115' in maps_by_name:
        route114 = maps_by_name['route114']
        route115 = maps_by_name['route115']

        print(f"\nBefore fix:")
        print(f"  route115: x={route115['x']}, y={route115['y']}")
        print(f"  route114: x={route114['x']}, y={route114['y']}")
        print(f"  Y difference: {abs(route115['y'] - route114['y'])} pixels")

        # Expected offset: 40 tiles * 8 pixels/tile = 320 pixels
        # Actual offset in world file might be 640 pixels (double)
        expected_offset = 320  # 40 * 8
        actual_offset = abs(route115['y'] - route114['y'])

        if actual_offset != expected_offset:
            # Recalculate route114's position
            # route114 connects to route115 with offset=40 (direction=left)
            # This means route114 should be 320 pixels DOWN from route115
            correct_y = route115['y'] + expected_offset
            if route114['y'] != correct_y:
                old_y = route114['y']
                route114['y'] = correct_y
                fixes_applied.append(f"route114: adjusted Y from {old_y} to {route114['y']}")
                print(f"\n✓ Fixed route114 Y coordinate (offset from route115)")

    # Save fixed world file
    if fixes_applied:
        output_path = world_path.replace('.world', '_fixed.world')
        with open(output_path, 'w') as f:
            json.dump(world, f, indent=2)

        print(f"\n{'='*60}")
        print(f"Fixes applied:")
        for fix in fixes_applied:
            print(f"  - {fix}")
        print(f"\nFixed world file saved to: {output_path}")
        print(f"{'='*60}")

        # Also save over original if requested
        backup_path = world_path.replace('.world', '_backup.world')
        import shutil
        shutil.copy(world_path, backup_path)
        shutil.copy(output_path, world_path)
        print(f"\nOriginal backed up to: {backup_path}")
        print(f"Fixed file copied to: {world_path}")
    else:
        print("\nNo fixes needed - coordinates are already correct!")

    return fixes_applied


if __name__ == "__main__":
    world_path = "output/Worlds/hoenn.world"
    if not Path(world_path).exists():
        print(f"Error: {world_path} not found!")
        print("Run this script from the porycon directory")
        exit(1)

    fixes = fix_world_coordinates(world_path)
    print(f"\n✓ Done! Applied {len(fixes)} fixes")
