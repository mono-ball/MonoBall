# Trainer Definitions

This directory contains trainer battle data (party, dialogue, AI).

## Structure

- Each trainer should have its own JSON file
- File name should match the `trainerId` (e.g., `youngster_joey.json`)

## Example: Basic Trainer

```json
{
  "trainerId": "trainer/youngster_joey",
  "displayName": "YOUNGSTER JOEY",
  "trainerClass": "youngster",
  "spriteId": "trainer-youngster",
  "prizeMoney": 64,
  "items": ["potion"],
  "aiScript": "AI/basic_trainer.csx",
  "introDialogue": "My RATTATA is in the top percentage of RATTATA!",
  "defeatDialogue": "I can't believe I lost...",
  "isRematchable": true,
  "party": [
    {
      "species": "rattata",
      "level": 4,
      "moves": ["tackle", "tail_whip"],
      "ability": "run_away"
    }
  ]
}
```

## Example: Gym Leader

```json
{
  "trainerId": "trainer/roxanne_1",
  "displayName": "ROXANNE",
  "trainerClass": "gym_leader",
  "spriteId": "trainer-roxanne",
  "prizeMoney": 1560,
  "items": ["potion", "potion"],
  "aiScript": "AI/gym_leader.csx",
  "introDialogue": "Welcome to the RUSTBORO GYM!",
  "defeatDialogue": "You've earned the STONE BADGE!",
  "onDefeatScript": "Events/roxanne_defeat.csx",
  "isRematchable": false,
  "party": [
    {
      "species": "geodude",
      "level": 12,
      "moves": ["tackle", "defense_curl", "rock_throw"],
      "heldItem": null,
      "ability": "sturdy",
      "ivs": {
        "hp": 6,
        "attack": 6,
        "defense": 6,
        "specialAttack": 6,
        "specialDefense": 6,
        "speed": 6
      }
    },
    {
      "species": "nosepass",
      "level": 15,
      "moves": ["tackle", "harden", "rock_throw", "block"],
      "heldItem": "oran_berry",
      "ability": "sturdy",
      "ivs": {
        "hp": 12,
        "attack": 12,
        "defense": 12,
        "specialAttack": 12,
        "specialDefense": 12,
        "speed": 12
      }
    }
  ]
}
```

## Fields

- **trainerId** (required): Unique identifier
- **displayName** (required): Name shown in battle
- **trainerClass**: Type of trainer (youngster, gym_leader, etc.)
- **spriteId**: Battle sprite to use
- **prizeMoney**: Base prize money (multiplied by highest level)
- **items**: Items the trainer can use in battle
- **aiScript**: AI behavior script
- **introDialogue**: Text before battle starts
- **defeatDialogue**: Text when defeated
- **onDefeatScript**: Script to run after defeat (e.g., give badge)
- **isRematchable**: Can battle again
- **party**: Array of Pokémon

### Party Member Fields

- **species**: Pokémon species (e.g., "rattata")
- **level**: Level (1-100)
- **moves**: Array of move names (max 4)
- **heldItem**: Item held (optional)
- **ability**: Ability name (optional)
- **ivs**: Individual values (0-31 per stat, optional)

