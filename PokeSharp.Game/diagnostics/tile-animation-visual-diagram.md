# Tile Animation Architecture - Visual Diagram

## Current Broken Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    CURRENT ENTITY LAYOUT                        │
└─────────────────────────────────────────────────────────────────┘

Entity #1 (Map Entity)
┌─────────────────────────────────┐
│  Components:                    │
│  ├─ TileMap                     │
│  │   ├─ MapId: "test-map"       │
│  │   ├─ Width: 20               │
│  │   ├─ Height: 15              │
│  │   ├─ GroundLayer[15,20]      │
│  │   ├─ ObjectLayer[15,20]      │
│  │   └─ OverheadLayer[15,20]    │
│  └─ TileCollider                │
│      ├─ SolidTiles[15,20]       │
│      └─ DirectionalBlocks       │
└─────────────────────────────────┘

Entity #2 (Water Animation)
┌─────────────────────────────────┐
│  Components:                    │
│  └─ AnimatedTile                │
│      ├─ BaseTileId: 17          │
│      ├─ FrameTileIds: [17,18]   │
│      ├─ FrameDurations: [0.5]   │
│      └─ CurrentFrame: 0         │
└─────────────────────────────────┘

Entity #3 (Grass Animation)
┌─────────────────────────────────┐
│  Components:                    │
│  └─ AnimatedTile                │
│      ├─ BaseTileId: 19          │
│      ├─ FrameTileIds: [19,20]   │
│      └─ ...                     │
└─────────────────────────────────┘

Entity #4 (Flower Animation)
┌─────────────────────────────────┐
│  Components:                    │
│  └─ AnimatedTile                │
│      ├─ BaseTileId: 21          │
│      ├─ FrameTileIds: [21,22,23]│
│      └─ ...                     │
└─────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    SYSTEM QUERY                                 │
└─────────────────────────────────────────────────────────────────┘

TileAnimationSystem Query:
  .WithAll<TileMap, AnimatedTile>()

  Looking for entities with BOTH components:

  Entity #1: Has TileMap? ✅  Has AnimatedTile? ❌  MATCH: ❌
  Entity #2: Has TileMap? ❌  Has AnimatedTile? ✅  MATCH: ❌
  Entity #3: Has TileMap? ❌  Has AnimatedTile? ✅  MATCH: ❌
  Entity #4: Has TileMap? ❌  Has AnimatedTile? ✅  MATCH: ❌

  TOTAL MATCHES: 0 ❌ System never runs!
```

## Fixed Architecture (Option 1B)

```
┌─────────────────────────────────────────────────────────────────┐
│                    FIXED ENTITY LAYOUT                          │
└─────────────────────────────────────────────────────────────────┘

Entity #1 (Map Entity) - ALL map data together
┌─────────────────────────────────────────────────────┐
│  Components:                                        │
│  ├─ TileMap                                         │
│  │   ├─ MapId: "test-map"                           │
│  │   ├─ Width: 20                                   │
│  │   ├─ Height: 15                                  │
│  │   ├─ GroundLayer[15,20]                          │
│  │   ├─ ObjectLayer[15,20]                          │
│  │   ├─ OverheadLayer[15,20]                        │
│  │   └─ AnimatedTiles[]  ← NEW!                     │
│  │       ├─ [0]: Water (BaseTileId:17)              │
│  │       ├─ [1]: Grass (BaseTileId:19)              │
│  │       └─ [2]: Flower (BaseTileId:21)             │
│  └─ TileCollider                                    │
│      ├─ SolidTiles[15,20]                           │
│      └─ DirectionalBlocks                           │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    SYSTEM QUERY                                 │
└─────────────────────────────────────────────────────────────────┘

TileAnimationSystem Query:
  .WithAll<TileMap>()  ← Simplified query

  Looking for entities with TileMap:

  Entity #1: Has TileMap? ✅  MATCH: ✅

  Then iterate over TileMap.AnimatedTiles array:
    - Update each AnimatedTile's frame timer
    - When timer expires, advance to next frame
    - Update TileMap layers with new frame tile IDs

  TOTAL MATCHES: 1 ✅ System runs successfully!
```

## Data Flow Comparison

### Current (Broken)
```
MapLoader.LoadAnimatedTiles()
         ↓
    AnimatedTile[]
         ↓
    foreach animTile
         ↓
    Create separate entity ← ❌ WRONG!
         ↓
    Entities scattered
         ↓
    Query never matches
         ↓
    System never runs
```

### Fixed
```
MapLoader.LoadAnimatedTiles()
         ↓
    AnimatedTile[]
         ↓
    Store in TileMap.AnimatedTiles ← ✅ CORRECT!
         ↓
    Create ONE entity with ALL data
         ↓
    Query matches the map entity
         ↓
    System iterates over AnimatedTiles array
         ↓
    Animations work! 🎉
```

## Why This Happens: ECS Query Semantics

```
┌─────────────────────────────────────────────────────────────────┐
│  ECS Query: .WithAll<A, B>()                                    │
│                                                                 │
│  Meaning: "Find entities that have component A AND component B  │
│            on the SAME entity"                                  │
│                                                                 │
│  NOT: "Find entities with A and separately find entities with B"│
│  NOT: "Find pairs of entities where one has A and one has B"   │
│  NOT: "Find A and B anywhere in the world"                     │
│                                                                 │
│  It's an intersection operation on component sets:             │
│    Entity Components ∩ {A, B} = {A, B}                         │
│                                                                 │
│  Example:                                                       │
│    Entity #1 = {TileMap, TileCollider}                         │
│    Query = {TileMap, AnimatedTile}                             │
│    Intersection = {TileMap} ≠ {TileMap, AnimatedTile}          │
│    Result: NO MATCH ❌                                          │
└─────────────────────────────────────────────────────────────────┘
```

## Comparison with Working System

```
┌─────────────────────────────────────────────────────────────────┐
│  CollisionSystem (WORKS)                                        │
└─────────────────────────────────────────────────────────────────┘

Query: .WithAll<TileMap, TileCollider>()

Entity #1: {TileMap, TileCollider}
           ^^^^^^^^  ^^^^^^^^^^^^^
           BOTH present on SAME entity ✅

Match: ✅ System runs successfully!

┌─────────────────────────────────────────────────────────────────┐
│  TileAnimationSystem (BROKEN)                                   │
└─────────────────────────────────────────────────────────────────┘

Query: .WithAll<TileMap, AnimatedTile>()

Entity #1: {TileMap, TileCollider}
           ^^^^^^^^  (no AnimatedTile) ❌

Entity #2: {AnimatedTile}
           (no TileMap) ❌

Match: ❌ System never runs!
```

## Implementation Checklist

- [ ] **Step 1**: Add `AnimatedTile[]` property to TileMap.cs
- [ ] **Step 2**: Store AnimatedTiles in TileMap in PokeSharpGame.cs
- [ ] **Step 3**: Remove separate entity creation loop
- [ ] **Step 4**: Update TileAnimationSystem query to `.WithAll<TileMap>()`
- [ ] **Step 5**: Update TileAnimationSystem to iterate over array
- [ ] **Step 6**: Compile and test
- [ ] **Step 7**: Verify animations work in-game

---

**Visual Guide Created**: 2025-11-01
**Purpose**: Clarify architecture mismatch for developers
