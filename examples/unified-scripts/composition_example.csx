// composition_example.csx
// Example demonstrating script composition - multiple scripts on same tile
// Shows how ice + grass scripts can work together on the same tile
// Both scripts receive TileSteppedOnEvent and execute in priority order

using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Events;
using PokeSharp.Core.Components;
using System;
using System.Numerics;

// First script: Ice behavior (Priority 100 - executes first)
public class CompositeIceScript : ScriptBase
{
    public override int Priority => 100; // Higher priority executes first

    public float slideSpeed = 2.0f;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) return;

            ctx.Logger.Info("[Composite Ice] Player completed movement, checking for slide");

            if (IsOnIceTile(evt.NewPosition)) {
                ContinueSliding(evt.Entity, evt.Direction);
            } else {
                RestoreNormalSpeed(evt.Entity);
            }
        });

        On<TileSteppedOnEvent>(evt => {
            if (ctx.Player.IsPlayerEntity(evt.Entity)) {
                ctx.Effects.PlaySound("ice_slide");
                ctx.Logger.Info($"[Composite Ice] Ice activated at ({evt.TileX}, {evt.TileY})");
            }
        });
    }

    private bool IsOnIceTile(Vector2 position)
    {
        var tile = ctx.Map.GetTileAt(position);
        return tile?.Type == TileType.Ice;
    }

    private void ContinueSliding(Entity entity, Direction direction)
    {
        var targetPos = GetNextPosition(entity, direction);

        if (ctx.Map.IsWalkable(targetPos) && !ctx.Map.HasCollision(targetPos)) {
            var movement = entity.Get<MovementComponent>();
            movement.Speed = slideSpeed;
            movement.StartMove(targetPos, direction);
        } else {
            RestoreNormalSpeed(entity);
            ctx.Effects.PlaySound("bump");
        }
    }

    private void RestoreNormalSpeed(Entity entity)
    {
        var movement = entity.Get<MovementComponent>();
        movement.Speed = 1.0f;
    }

    private Vector2 GetNextPosition(Entity entity, Direction direction)
    {
        var currentPos = entity.Get<Position>().Value;
        return direction switch {
            Direction.Up => currentPos + new Vector2(0, -1),
            Direction.Down => currentPos + new Vector2(0, 1),
            Direction.Left => currentPos + new Vector2(-1, 0),
            Direction.Right => currentPos + new Vector2(1, 0),
            _ => currentPos
        };
    }
}

// Second script: Grass encounter (Priority 50 - executes after ice)
public class CompositeGrassScript : ScriptBase
{
    public override int Priority => 50; // Lower priority executes second

    private static readonly Random random = new Random();

    public float encounterRate = 0.10f;
    public string[] wildPokemon = new[] { "Spheal", "Snorunt", "Swinub" }; // Ice-type Pokemon!
    public int minLevel = 3;
    public int maxLevel = 6;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            if (!ctx.Player.IsPlayerEntity(evt.Entity)) return;

            ctx.Logger.Info($"[Composite Grass] Grass activated at ({evt.TileX}, {evt.TileY})");

            // Play grass rustle (combines with ice sound)
            ctx.Effects.PlayEffect("grass_rustle", evt.TilePosition);

            // Check for wild encounter
            CheckWildEncounter(evt);
        });
    }

    private void CheckWildEncounter(TileSteppedOnEvent evt)
    {
        if (random.NextDouble() < encounterRate) {
            ctx.Logger.Info("[Composite Grass] Wild encounter triggered on icy grass!");
            TriggerWildBattle(evt.Entity, evt.TilePosition);
        }
    }

    private void TriggerWildBattle(Entity player, Vector2 position)
    {
        var pokemonName = wildPokemon[random.Next(wildPokemon.Length)];
        var level = random.Next(minLevel, maxLevel + 1);

        ctx.Logger.Info($"[Composite Grass] Wild {pokemonName} (Lv.{level}) appeared!");

        ctx.Effects.PlaySound("wild_encounter");
        ctx.Effects.FlashScreen(Color.White, duration: 0.3f);
        ctx.GameState.StartWildBattle(pokemonName, level);
    }
}

// HOW TO USE:
// 1. Attach both scripts to the same tile in your map editor
// 2. ScriptAttachmentSystem will execute them in priority order:
//    - CompositeIceScript (Priority 100) executes first - handles sliding
//    - CompositeGrassScript (Priority 50) executes second - handles encounters
// 3. Both scripts receive the same events independently
// 4. Player slides on ice AND can encounter Pokemon at the same time!

// Example tile configuration in map data:
// {
//   "type": "ice_grass_composite",
//   "scripts": [
//     "composition_example.csx#CompositeIceScript",
//     "composition_example.csx#CompositeGrassScript"
//   ]
// }

// For testing, return both scripts as an array:
return new ScriptBase[] {
    new CompositeIceScript(),
    new CompositeGrassScript()
};

// Note: This demonstrates that multiple scripts can:
// - Share the same tile
// - Execute in priority order
// - React to the same events independently
// - Create emergent gameplay (icy grass = challenging encounters!)
