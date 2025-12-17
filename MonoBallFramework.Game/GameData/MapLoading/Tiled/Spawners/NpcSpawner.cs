using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Spawns NPC entities directly from Tiled properties (no template lookup).
///     This is the preferred format - sprites and behaviors are specified directly in Tiled.
///     Required Tiled properties:
///     - type = "npc"
///     - spriteId (string): Sprite ID in format "category/spritename"
///     Optional properties:
///     - direction (string): north/south/east/west (default: south)
///     - elevation (int): 0-255 (default: 0)
///     - behaviorId (string): Behavior definition ID
///     - rangeX, rangeY (int): Movement range for wander behaviors
///     - axis (string): horizontal/vertical for patrol
///     - range (int): Range for patrol on specified axis
///     - waypointWaitTime (float): Seconds to wait at waypoints
///     - trainerType (string): Trainer type if this is a trainer NPC
///     - sightRange (int): View range for trainers
///     - visibilityFlag (string): Flag ID controlling visibility (e.g., "base:flag:hide/rival_oak_lab")
///     - hideWhenFlagSet (bool): If true (default), hide NPC when flag is set (FLAG_HIDE_* pattern)
/// </summary>
public sealed class NpcSpawner : IEntitySpawner
{
    private readonly ILogger<NpcSpawner>? _logger;

    public NpcSpawner(ILogger<NpcSpawner>? logger = null)
    {
        _logger = logger;
    }

    public string Name => "NpcSpawner";

    public int Priority => 100;

    public bool CanSpawn(EntitySpawnContext context)
    {
        // Must be type "npc" with a spriteId property
        return context.TiledObject.Type == "npc"
               && TiledPropertyParser.HasProperty(context.TiledObject.Properties, "spriteId");
    }

    public Entity Spawn(EntitySpawnContext context)
    {
        string errorContext = context.CreateErrorContext();
        Dictionary<string, object> props = context.TiledObject.Properties;
        TmxObject obj = context.TiledObject;

        // Parse required spriteId - format: "npcs/generic_twin" or full "base:sprite:npcs/generic_twin"
        string spriteIdStr = TiledPropertyParser.GetRequiredString(props, "spriteId", errorContext);
        GameSpriteId spriteId = spriteIdStr.Contains(':')
            ? new GameSpriteId(spriteIdStr)
            : new GameSpriteId($"base:sprite:{spriteIdStr}");
        context.RegisterSpriteId(spriteId);

        // Get tile position
        (int tileX, int tileY) = context.GetTilePosition();

        // Parse optional properties
        byte elevation = TiledPropertyParser.GetOptionalByte(props, "elevation", errorContext) ?? 0;
        Direction direction = TiledPropertyParser.GetOptionalDirection(props, "direction", errorContext)
                              ?? Direction.South;
        string npcId = !string.IsNullOrWhiteSpace(obj.Name) ? obj.Name : $"npc_{obj.Id}";

        // Parse visibility flag and determine initial visibility
        string? visibilityFlagStr = TiledPropertyParser.GetOptionalString(props, "visibilityFlag");
        bool hideWhenFlagSet = TiledPropertyParser.GetBoolOrFalse(props, "hideWhenFlagSet", errorContext)
                               || !TiledPropertyParser.HasProperty(props, "hideWhenFlagSet");
        bool shouldBeVisible = true;
        GameFlagId? visibilityFlagId = null;

        if (!string.IsNullOrWhiteSpace(visibilityFlagStr))
        {
            visibilityFlagId = visibilityFlagStr.Contains(':')
                ? new GameFlagId(visibilityFlagStr)
                : new GameFlagId($"base:flag:{visibilityFlagStr}");

            if (context.GameStateApi != null)
            {
                bool flagValue = context.GameStateApi.GetFlag(visibilityFlagId);
                shouldBeVisible = hideWhenFlagSet ? !flagValue : flagValue;
            }
        }

        // Use NpcSpawnBuilder to create the entity
        INpcSpawnBuilder builder = new NpcSpawnBuilder(context.World, tileX, tileY, _logger)
            .FromDefinition(GameNpcId.Create(npcId))
            .WithSprite(spriteId)
            .Facing(direction)
            .AtElevation(elevation)
            .Visible(shouldBeVisible)
            .OnMap(context.MapId, context.TileHeight)
            .WithParent(context.MapInfoEntity);

        // Add visibility flag if specified
        if (visibilityFlagId != null)
        {
            builder.WithVisibilityFlag(visibilityFlagId, hideWhenFlagSet);
            _logger?.LogDebug(
                "NPC '{Name}' linked to visibility flag {FlagId} (hideWhenSet={Hide})",
                obj.Name, visibilityFlagId.Value, hideWhenFlagSet);
        }

        // Add display name if specified
        if (!string.IsNullOrWhiteSpace(obj.Name))
        {
            builder.WithDisplayName(obj.Name);
        }

        // Add behavior if specified
        // Supports both full ID format (base:behavior:movement/wander) and short format (wander)
        string? behaviorIdStr = TiledPropertyParser.GetOptionalString(props, "behaviorId");
        if (!string.IsNullOrWhiteSpace(behaviorIdStr))
        {
            GameBehaviorId behaviorId = behaviorIdStr.Contains(':')
                ? new GameBehaviorId(behaviorIdStr)
                : GameBehaviorId.Create(behaviorIdStr);
            builder.WithBehavior(behaviorId);

            // Get patrol waypoints from Tiled properties (explicit or axis-generated)
            Point[]? waypoints =
                TiledPropertyParser.GetPatrolWaypoints(props, behaviorIdStr, tileX, tileY, errorContext);
            if (waypoints != null)
            {
                builder.WithPath(waypoints, true);
            }
        }

        // Add movement range for wander behaviors
        if (TiledPropertyParser.HasProperty(props, "rangeX") &&
            TiledPropertyParser.HasProperty(props, "rangeY"))
        {
            int rangeX = TiledPropertyParser.GetRequiredInt(props, "rangeX", errorContext);
            int rangeY = TiledPropertyParser.GetRequiredInt(props, "rangeY", errorContext);
            builder.WithMovementRange(rangeX, rangeY);
        }

        // Configure trainer data
        string? trainerType = TiledPropertyParser.GetOptionalString(props, "trainerType");
        if (!string.IsNullOrWhiteSpace(trainerType) && trainerType != "TRAINER_TYPE_NONE")
        {
            int viewRange = TiledPropertyParser.GetOptionalInt(props, "sightRange", errorContext) ?? 5;
            builder.AsTrainer(viewRange);
        }

        // Spawn the entity
        Entity npcEntity = builder.Spawn();

        _logger?.LogDebug(
            "Spawned NPC '{Name}' at ({TileX}, {TileY}) with sprite {SpriteId}",
            obj.Name, tileX, tileY, spriteIdStr);

        return npcEntity;
    }
}
