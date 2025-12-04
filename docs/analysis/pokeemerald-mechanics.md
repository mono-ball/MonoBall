# Pokemon Emerald Game Mechanics Analysis for MonoBall Framework ECS Conversion

## Executive Summary

This document analyzes the core game mechanics from the pokeemerald decompilation project and maps them to ECS event-driven architecture for MonoBall Framework. The analysis covers battle systems, wild encounters, trainer AI, map/event systems, and data structures.

---

## 1. Core Data Structures

### 1.1 Pokemon Data (pokemon.h)

**C Struct: `struct Pokemon` (232 bytes)**
```c
struct Pokemon {
    struct BoxPokemon box;  // Core encrypted data
    u32 status;             // Battle status (PSN, BRN, PAR, etc.)
    u8 level;
    u8 mail;
    u16 hp;                 // Current HP
    u16 maxHP;
    u16 attack, defense, speed, spAttack, spDefense;
};
```

**Key Sub-structures:**
- `BoxPokemon`: Encrypted storage format (80 bytes)
  - Personality value (determines nature, gender, shininess, Unown form)
  - OT ID (Original Trainer ID)
  - Nickname (10 chars)
  - 4 Substructs rotated based on personality:
    - Substruct 0: Species, held item, experience, PP bonuses, friendship
    - Substruct 1: Moves[4], PP[4]
    - Substruct 2: EVs (6 stats), Contest stats
    - Substruct 3: IVs (6 stats, 5 bits each), pokerus, met location, ribbons

**MonoBall Framework ECS Mapping:**
```csharp
// Components
public class PokemonIdentity : IComponent {
    public ushort Species;
    public uint Personality;
    public uint OtId;
    public string Nickname;
    public PokemonNature Nature;
}

public class PokemonStats : IComponent {
    public byte Level;
    public ushort CurrentHp, MaxHp;
    public ushort Attack, Defense, Speed, SpAttack, SpDefense;
    public byte[] IVs; // 6 values, 0-31
    public byte[] EVs; // 6 values, 0-255
}

public class PokemonMoveSet : IComponent {
    public ushort[] Moves; // 4 moves
    public byte[] PP;      // Current PP for each move
    public byte[] MaxPP;   // Max PP for each move
}

public class PokemonStatus : IComponent {
    public StatusCondition PrimaryStatus; // Sleep, Poison, Burn, etc.
    public byte SleepTurns;
    public byte ToxicCounter;
}
```

---

### 1.2 Battle System (battle.h)

**C Struct: `struct BattlePokemon` (88 bytes)**
```c
struct BattlePokemon {
    u16 species;
    u16 attack, defense, speed, spAttack, spDefense;
    u16 moves[4];
    u32 hpIV:5, attackIV:5, defenseIV:5, speedIV:5, spAttackIV:5, spDefenseIV:5;
    s8 statStages[8];  // -6 to +6 for each stat
    u8 ability;
    u8 types[2];
    u8 pp[4];
    u16 hp;
    u8 level;
    u16 maxHP;
    u16 item;
    u8 nickname[11];
    u32 status1;  // Non-volatile status
    u32 status2;  // Volatile status (confusion, flinch, etc.)
    u32 personality;
    u32 otId;
};
```

**Battle Actions (battle.h:26-42)**
```c
#define B_ACTION_USE_MOVE     0
#define B_ACTION_USE_ITEM     1
#define B_ACTION_SWITCH       2
#define B_ACTION_RUN          3
#define B_ACTION_SAFARI_*     4-8
```

**Battle Structures:**
- `DisableStruct`: Tracks move disabling (Disable, Encore, Taunt, etc.)
- `ProtectStruct`: Tracks protection moves and immobility
- `SpecialStatus`: Tracks stat lowering, intimidate, focus band, shell bell
- `SideTimer`: Tracks per-side effects (Reflect, Light Screen, Safeguard, Spikes)
- `WishFutureKnock`: Tracks delayed moves (Future Sight, Wish)
- `AI_ThinkingStruct`: AI decision making

**MonoBall Framework ECS Mapping:**
```csharp
// Battle Entities
public class BattleEntity : Entity {
    // One entity per active Pokemon in battle (up to 4)
}

// Battle Components
public class BattlePosition : IComponent {
    public BattlerSide Side;      // Player/Opponent
    public BattlerFlank Flank;    // Left/Right
    public byte BattlerIndex;     // 0-3
}

public class BattleStats : IComponent {
    public short[] StatStages;  // -6 to +6 for each stat
    public ushort CurrentStats[7]; // Calculated stats after stages
}

public class VolatileStatus : IComponent {
    public uint Status2Flags;  // Confusion, flinch, etc.
    public byte ConfusionTurns;
    public byte UproarTurns;
    public BattlerIndex InfatuatedWith;
    public bool HasSubstitute;
    public byte SubstituteHP;
}

public class BattleDisables : IComponent {
    public ushort DisabledMove;
    public byte DisableTimer;
    public ushort EncoredMove;
    public byte EncoreTimer;
    public byte TauntTimer;
    public bool IsTruant;
}

// Battle Events
public class BattleTurnStartEvent {
    public ushort TurnNumber;
}

public class SelectMoveEvent {
    public Entity Battler;
    public ushort Move;
    public Entity Target;
}

public class ExecuteMoveEvent {
    public Entity Attacker;
    public Entity Defender;
    public ushort MoveId;
    public byte Effectiveness; // 0, 50, 100, 200
}

public class DamageCalculationEvent {
    public Entity Attacker;
    public Entity Defender;
    public ushort BasePower;
    public PokemonType MoveType;
    public bool IsPhysical;
    public int CalculatedDamage;
}

public class ApplyDamageEvent {
    public Entity Target;
    public int Damage;
    public bool IsCritical;
}

public class FaintEvent {
    public Entity FaintedBattler;
}

public class SwitchPokemonEvent {
    public Entity OldBattler;
    public Entity NewBattler;
    public byte PartyIndex;
}
```

---

## 2. Battle System Flow

### 2.1 Turn Order System

**pokeemerald Implementation:**
1. Player/AI select actions (move, item, switch, run)
2. Determine action priority:
   - Switching has highest priority
   - Move priority (moves have -7 to +5 priority)
   - Speed stat determines order within same priority
3. Execute actions in order
4. Process end-of-turn effects (weather, status, etc.)

**MonoBall Framework ECS Event Flow:**
```csharp
// Turn Processing System
public class BattleTurnSystem : IEventListener<BattleTurnStartEvent> {
    public void OnEvent(BattleTurnStartEvent evt) {
        // 1. Gather actions from all battlers
        var actions = GatherBattlerActions();

        // 2. Sort by priority
        actions.Sort(new BattleActionPriorityComparer());

        // 3. Execute each action
        foreach (var action in actions) {
            EmitEvent(new ExecuteBattleActionEvent(action));
        }

        // 4. End-of-turn processing
        EmitEvent(new BattleTurnEndEvent());
    }
}

// Damage Calculation System
public class DamageCalculationSystem : IEventListener<DamageCalculationEvent> {
    public void OnEvent(DamageCalculationEvent evt) {
        // Formula from CalculateBaseDamage (pokemon.h:429)
        // Damage = ((2 * Level / 5 + 2) * Power * Atk / Def) / 50 + 2

        var attacker = evt.Attacker.Get<BattleStats>();
        var defender = evt.Defender.Get<BattleStats>();

        int baseDamage = ((2 * attacker.Level / 5 + 2)
            * evt.BasePower
            * GetAttackStat(attacker, evt.IsPhysical)
            / GetDefenseStat(defender, evt.IsPhysical)) / 50 + 2;

        // Apply modifiers (STAB, type effectiveness, random, etc.)
        evt.CalculatedDamage = ApplyDamageModifiers(baseDamage, evt);

        EmitEvent(new ApplyDamageEvent {
            Target = evt.Defender,
            Damage = evt.CalculatedDamage
        });
    }
}
```

### 2.2 Status Conditions

**Non-Volatile Status (STATUS1_*):**
- Sleep (3 bits: turns remaining)
- Poison (1 bit)
- Burn (1 bit)
- Freeze (1 bit)
- Paralysis (1 bit)
- Toxic (1 bit + 4 bits for counter)

**Volatile Status (STATUS2_*):**
- Confusion (3 bits: turns)
- Flinch (1 bit)
- Uproar (3 bits: turns)
- Bide (2 bits: turns)
- Lock-Confuse/Thrash (2 bits: turns)
- Wrapped (3 bits: turns)
- Infatuation (4 bits: one per battler)
- Focus Energy, Transformed, Recharge, Rage, Substitute, Destiny Bond, etc.

**MonoBall Framework Status Events:**
```csharp
public class InflictStatusEvent {
    public Entity Target;
    public StatusCondition Status;
    public byte Duration; // For sleep, toxic, etc.
}

public class StatusTickEvent {
    public Entity Battler;
    public StatusCondition Status;
    // Emitted each turn for poison damage, sleep counting, etc.
}

public class CureStatusEvent {
    public Entity Target;
    public StatusCondition Status;
}
```

---

## 3. Wild Encounter System

### 3.1 Encounter Rate Calculation

**wild_encounter.c:**
```c
#define MAX_ENCOUNTER_RATE 2880

// Encounter check:
// 1. Generate random 0-2879
// 2. Compare to encounter rate
// 3. Apply modifiers (Repel, Cleanse Tag, abilities)
```

**Encounter Areas:**
- `WILD_AREA_LAND`: Grass, cave floor
- `WILD_AREA_WATER`: Surfing
- `WILD_AREA_ROCKS`: Rock Smash
- `WILD_AREA_FISHING`: Old/Good/Super Rod

**MonoBall Framework ECS Mapping:**
```csharp
// Map Components
public class WildEncounterData : IComponent {
    public int EncounterRate; // 0-2880
    public WildPokemonSlot[] LandSlots;   // 12 slots
    public WildPokemonSlot[] WaterSlots;  // 5 slots
    public WildPokemonSlot[] RockSlots;   // 5 slots
    public WildPokemonSlot[] FishingSlots; // 10 slots (3 rods)
}

public struct WildPokemonSlot {
    public byte MinLevel;
    public byte MaxLevel;
    public ushort Species;
}

// Wild Encounter Events
public class StepTakenEvent {
    public Entity Player;
    public TileCoordinates Position;
}

public class WildEncounterCheckEvent {
    public Entity Player;
    public bool ShouldEncounter; // Set by system
}

public class WildEncounterStartEvent {
    public ushort Species;
    public byte Level;
    public WildEncounterType Type; // Grass, Water, Fishing, etc.
}

// Wild Encounter System
public class WildEncounterSystem : IEventListener<StepTakenEvent> {
    public void OnEvent(StepTakenEvent evt) {
        var tile = GetTileAt(evt.Position);
        var encounterData = tile.Get<WildEncounterData>();

        if (encounterData == null) return;

        // Check encounter rate
        int roll = Random.Range(0, MAX_ENCOUNTER_RATE);
        int rate = encounterData.EncounterRate;

        // Apply modifiers
        rate = ApplyRepelModifier(rate, evt.Player);
        rate = ApplyCleanseTagModifier(rate, evt.Player);
        rate = ApplyAbilityModifier(rate, evt.Player);

        if (roll < rate) {
            var encounter = SelectWildPokemon(encounterData, tile);
            EmitEvent(new WildEncounterStartEvent {
                Species = encounter.Species,
                Level = encounter.Level
            });
        }
    }
}
```

### 3.2 Ability-Based Encounters

**Abilities that affect encounters:**
- **Illuminate**: Doubles encounter rate
- **Keen Eye** / **Intimidate**: Reduces encounter rate by 50%
- **Arena Trap** / **Magnet Pull**: Increases encounters of specific types
- **Static** / **Flash Fire** / etc.: Increases encounters of matching type (50% chance)

---

## 4. Event/Script System

### 4.1 Map Event Types

**From map.json structure:**

1. **Object Events** (NPCs, trainers, items):
```json
{
  "local_id": "LOCALID_LITTLEROOT_TWIN",
  "graphics_id": "OBJ_EVENT_GFX_TWIN",
  "x": 16, "y": 10, "elevation": 3,
  "movement_type": "MOVEMENT_TYPE_WANDER_AROUND",
  "script": "LittlerootTown_EventScript_Twin"
}
```

2. **Warp Events** (doors, teleports):
```json
{
  "x": 14, "y": 8, "elevation": 0,
  "dest_map": "MAP_LITTLEROOT_TOWN_MAYS_HOUSE_1F",
  "dest_warp_id": "1"
}
```

3. **Coord Events** (trigger zones):
```json
{
  "type": "trigger",
  "x": 10, "y": 1, "elevation": 3,
  "var": "VAR_LITTLEROOT_TOWN_STATE",
  "var_value": "0",
  "script": "LittlerootTown_EventScript_NeedPokemonTrigger"
}
```

4. **BG Events** (signs, hidden items):
```json
{
  "type": "sign",
  "x": 15, "y": 13, "elevation": 0,
  "script": "LittlerootTown_EventScript_TownSign"
}
```

**MonoBall Framework ECS Mapping:**
```csharp
// Map Entity with Event Components
public class MapObjectEvent : IComponent {
    public string LocalId;
    public SpriteId GraphicsId;
    public Vector2Int Position;
    public byte Elevation;
    public MovementType Movement;
    public string ScriptName;
    public string VisibilityFlag;
}

public class WarpEvent : IComponent {
    public Vector2Int Position;
    public string DestinationMap;
    public byte DestinationWarpId;
}

public class CoordTrigger : IComponent {
    public Vector2Int Position;
    public byte Elevation;
    public string VariableName;
    public ushort RequiredValue;
    public string ScriptName;
}

public class SignEvent : IComponent {
    public Vector2Int Position;
    public string ScriptName;
}

// Script Events
public class InteractWithNpcEvent {
    public Entity Npc;
    public Entity Player;
}

public class StepOnTriggerEvent {
    public Entity Trigger;
    public Entity Player;
}

public class ExecuteScriptEvent {
    public string ScriptName;
    public Dictionary<string, object> Parameters;
}

// Script Execution System
public class ScriptExecutionSystem : IEventListener<ExecuteScriptEvent> {
    public void OnEvent(ExecuteScriptEvent evt) {
        // Load .csx script file
        var script = LoadScript(evt.ScriptName);

        // Create script context with ECS access
        var context = new ScriptContext {
            World = this.World,
            Parameters = evt.Parameters
        };

        // Execute script
        await script.ExecuteAsync(context);
    }
}
```

### 4.2 Script Format Conversion

**pokeemerald uses custom scripting language:**
```
.inc format (compiled to bytecode):
LittlerootTown_EventScript_Twin::
    msgbox LittlerootTown_Text_Twin, MSGBOX_NPC
    end
```

**MonoBall Framework uses C# Scripting (.csx):**
```csharp
// LittlerootTown_EventScript_Twin.csx
public async Task Execute(ScriptContext ctx) {
    await ctx.ShowMessage("LittlerootTown_Text_Twin", MessageBoxType.Npc);
}
```

**Common Script Commands to Map:**
- `msgbox` → `ShowMessage()`
- `giveitem` → `GiveItem()`
- `givemon` → `GivePokemon()`
- `trainerbattle` → `StartTrainerBattle()`
- `warp` → `WarpPlayer()`
- `playmoncry` → `PlayCry()`
- `fadescreen` → `FadeScreen()`
- `setflag/clearflag` → `SetFlag()/ClearFlag()`
- `setvar/checkvar` → `SetVariable()/CheckVariable()`
- `call/goto` → C# function calls

---

## 5. Trainer AI System

### 5.1 AI Flags (battle_ai_script_commands.c)

**AI Flag Levels:**
```c
#define AI_FLAG_CHECK_BAD_MOVE      (1 << 0)  // Don't use ineffective moves
#define AI_FLAG_TRY_TO_FAINT        (1 << 1)  // Prefer moves that KO
#define AI_FLAG_CHECK_VIABILITY     (1 << 2)  // Don't spam status moves
#define AI_FLAG_SETUP_FIRST_TURN    (1 << 3)  // Use setup moves early
#define AI_FLAG_RISKY               (1 << 4)  // Use high risk moves
#define AI_FLAG_PREFER_STRONGEST    (1 << 5)  // Prefer highest power
#define AI_FLAG_PREFER_BATON_PASS   (1 << 6)
// etc... up to 32 flags
```

**AI Scoring System:**
```c
struct AI_ThinkingStruct {
    u8 aiState;
    u16 moveConsidered;
    s8 score[MAX_MON_MOVES];  // Score for each move (-128 to 127)
    u32 aiFlags;              // Combination of AI_FLAG_*
};
```

**MonoBall Framework AI Events:**
```csharp
public class AiMoveSelectionEvent {
    public Entity AiBattler;
    public Entity[] Targets;
    public ushort SelectedMove;
    public Entity SelectedTarget;
}

public class AiScoreMoveEvent {
    public ushort Move;
    public Entity User;
    public Entity Target;
    public int Score; // -128 to 127
}

// AI Scoring System
public class BattleAiSystem : IEventListener<AiMoveSelectionEvent> {
    public void OnEvent(AiMoveSelectionEvent evt) {
        var trainer = evt.AiBattler.Get<TrainerData>();
        int[] scores = new int[4];

        for (int i = 0; i < 4; i++) {
            var move = evt.AiBattler.Get<PokemonMoveSet>().Moves[i];
            scores[i] = ScoreMove(move, evt.AiBattler, evt.Targets, trainer.AiFlags);
        }

        // Select best move
        int bestIndex = GetBestMoveIndex(scores);
        evt.SelectedMove = evt.AiBattler.Get<PokemonMoveSet>().Moves[bestIndex];
        evt.SelectedTarget = SelectTarget(evt.Targets, evt.SelectedMove);
    }

    private int ScoreMove(ushort moveId, Entity user, Entity[] targets, uint aiFlags) {
        int score = 0;

        // Check effectiveness
        if ((aiFlags & AI_FLAG_CHECK_BAD_MOVE) != 0) {
            foreach (var target in targets) {
                float effectiveness = GetTypeEffectiveness(moveId, target);
                if (effectiveness < 1.0f) score -= 10;
                if (effectiveness > 1.0f) score += 10;
            }
        }

        // Check KO potential
        if ((aiFlags & AI_FLAG_TRY_TO_FAINT) != 0) {
            foreach (var target in targets) {
                if (CanKO(moveId, user, target)) {
                    score += 20;
                }
            }
        }

        // etc... more scoring logic
        return score;
    }
}
```

---

## 6. Data Conversion Requirements

### 6.1 Pokemon Species Data

**pokeemerald: `struct SpeciesInfo` (26 bytes)**
```c
struct SpeciesInfo {
    u8 baseHP, baseAttack, baseDefense, baseSpeed, baseSpAttack, baseSpDefense;
    u8 types[2];
    u8 catchRate;
    u8 expYield;
    u16 evYield_HP:2, evYield_Attack:2, ...;
    u16 itemCommon, itemRare;
    u8 genderRatio;
    u8 eggCycles;
    u8 friendship;
    u8 growthRate;
    u8 eggGroups[2];
    u8 abilities[2];
    u8 safariZoneFleeRate;
    u8 bodyColor:7, noFlip:1;
};
```

**MonoBall Framework JSON format:**
```json
{
  "species_id": 25,
  "name": "Pikachu",
  "base_stats": {
    "hp": 35, "attack": 55, "defense": 40,
    "speed": 90, "sp_attack": 50, "sp_defense": 50
  },
  "types": ["Electric"],
  "abilities": ["Static", "LightningRod"],
  "catch_rate": 190,
  "exp_yield": 112,
  "ev_yield": { "speed": 2 },
  "gender_ratio": 0.5,
  "egg_cycles": 10,
  "growth_rate": "MediumFast"
}
```

### 6.2 Move Data

**pokeemerald: `struct BattleMove` (8 bytes)**
```c
struct BattleMove {
    u8 effect;             // Move effect ID (1-60)
    u8 power;
    u8 type;
    u8 accuracy;
    u8 pp;
    u8 secondaryEffectChance;
    u8 target;             // Single, all opponents, etc.
    s8 priority;           // -7 to +5
    u8 flags;              // Contact, protect-able, etc.
};
```

**MonoBall Framework JSON:**
```json
{
  "move_id": 85,
  "name": "Thunderbolt",
  "type": "Electric",
  "category": "Special",
  "power": 90,
  "accuracy": 100,
  "pp": 15,
  "priority": 0,
  "target": "SelectedTarget",
  "effect": "Damage",
  "secondary_effect": {
    "chance": 10,
    "status": "Paralysis"
  },
  "flags": ["Protect", "MagicCoat", "Snatch"]
}
```

### 6.3 Map Data

**pokeemerald: Binary map format + JSON**
- Metatiles (16x16 px tiles with behavior)
- Connections to adjacent maps
- Event objects (NPCs, items)
- Wild encounter tables

**MonoBall Framework: Tiled TMX format + JSON**
```json
{
  "map_id": "LittlerootTown",
  "tilemap_file": "LittlerootTown.tmx",
  "music": "MUS_LITTLEROOT",
  "weather": "Sunny",
  "wild_encounters": {
    "land": [],
    "water": [],
    "fishing": []
  },
  "events": {
    "npcs": [...],
    "warps": [...],
    "triggers": [...],
    "signs": [...]
  }
}
```

---

## 7. Priority Implementation List

### Phase 1: Core Battle System (Highest Priority)
1. **Battle Entity Creation**
   - Convert Pokemon to BattleEntity
   - Initialize battle components (stats, status, moves)

2. **Turn Order & Action Selection**
   - Priority queue for actions
   - Speed-based ordering
   - Move priority handling

3. **Damage Calculation**
   - Base damage formula
   - Type effectiveness
   - Critical hits
   - STAB (Same Type Attack Bonus)
   - Random factor (0.85-1.0)

4. **Status Conditions**
   - Non-volatile (sleep, poison, burn, freeze, paralysis)
   - Volatile (confusion, flinch, etc.)
   - Status tick events

5. **Move Effects**
   - Implement top 20 most common move effects:
     - Damage only
     - Damage + stat changes
     - Damage + status infliction
     - Stat changes only
     - Healing
     - Weather changes
     - Entry hazards

### Phase 2: Wild Encounters (High Priority)
1. **Encounter Rate System**
   - Step counter
   - Repel checking
   - Ability modifiers (Illuminate, Keen Eye)

2. **Wild Pokemon Selection**
   - Slot-based selection (12 land slots, 5 water, etc.)
   - Level variation
   - Ability-based encounters (Static, Magnet Pull)

3. **Encounter Types**
   - Grass encounters
   - Water (surfing)
   - Fishing (Old/Good/Super Rod)
   - Rock Smash
   - Cave encounters

### Phase 3: Trainer Battles (High Priority)
1. **Trainer Data**
   - Party composition
   - AI flags
   - Reward money
   - Rematch support

2. **Trainer AI**
   - Move scoring system
   - Switch logic
   - Item usage

### Phase 4: Map & Events (Medium Priority)
1. **Map Loading**
   - Tiled TMX integration
   - Collision detection
   - Elevation/layers

2. **Event System**
   - NPC interactions
   - Warp events
   - Coord triggers
   - Sign reading

3. **Script Execution**
   - C# scripting (.csx)
   - Script context with ECS access
   - Common script commands

### Phase 5: Items & Inventory (Medium Priority)
1. **Item Usage**
   - Battle items (Potions, status healers)
   - Field items (Repel, Escape Rope)
   - Hold items (Leftovers, Choice Band)

2. **Bag System**
   - 5 pockets (Items, Key Items, Poke Balls, TMs/HMs, Berries)
   - Stack limits
   - Sort/organize

### Phase 6: Advanced Features (Lower Priority)
1. **Weather System**
   - Rain, Sandstorm, Hail, Sun
   - Weather-based abilities and move effects

2. **Abilities**
   - Implement top 50 abilities
   - Ability activation events

3. **Held Items**
   - Implement common held items (Leftovers, berries, type boosters)

4. **Double Battles**
   - 2v2 targeting
   - Partner AI
   - Multi-target moves

5. **Battle Facilities**
   - Battle Tower
   - Battle Frontier (later phase)

---

## 8. Event Type Summary

### Battle Events
- `BattleTurnStartEvent` - Start of battle turn
- `SelectMoveEvent` - Player/AI selects move
- `ExecuteMoveEvent` - Move is executed
- `DamageCalculationEvent` - Calculate damage
- `ApplyDamageEvent` - Apply calculated damage
- `FaintEvent` - Pokemon faints
- `SwitchPokemonEvent` - Switch Pokemon
- `InflictStatusEvent` - Apply status condition
- `StatusTickEvent` - Process ongoing status
- `CureStatusEvent` - Remove status
- `StatStageChangeEvent` - Change stat stages
- `BattleTurnEndEvent` - End of turn processing

### Overworld Events
- `StepTakenEvent` - Player takes step
- `WildEncounterCheckEvent` - Check for encounter
- `WildEncounterStartEvent` - Start wild battle
- `InteractWithNpcEvent` - Talk to NPC
- `StepOnTriggerEvent` - Coord trigger activated
- `ExecuteScriptEvent` - Run event script
- `WarpEvent` - Teleport/warp
- `ItemPickupEvent` - Pick up item

### System Events
- `MapLoadedEvent` - New map loaded
- `SaveGameEvent` - Save game state
- `LoadGameEvent` - Load game state

---

## 9. Conclusion

The pokeemerald decompilation reveals a complex but well-structured battle system built around:
- **Bitpacked data structures** for memory efficiency (can be unpacked in C#)
- **State machines** for battle flow (maps well to ECS events)
- **Scriptable events** (convert .inc to .csx)
- **Modular systems** (wild encounters, trainer AI, status effects)

**Key Challenges for ECS Conversion:**
1. Converting bitpacked C structs to C# components
2. Maintaining battle turn order with event-driven architecture
3. Translating pokeemerald's script language to C# scripts
4. Preserving exact damage calculation formulas
5. Implementing 200+ move effects as event listeners

**Recommended Approach:**
1. Start with minimal battle system (1v1, 20 moves, 5 status effects)
2. Add wild encounters with basic rate calculation
3. Expand move pool gradually (implement effects in priority order)
4. Add trainer battles and basic AI
5. Implement map events and scripting
6. Add advanced features (double battles, abilities, weather)

This phased approach allows for early testing and iteration while building toward feature parity with Pokemon Emerald.
