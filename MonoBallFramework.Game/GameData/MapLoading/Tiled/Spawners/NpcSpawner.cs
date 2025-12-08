using System.Globalization;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.GameState;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Spawns NPC entities directly from Tiled properties (no template lookup).
///     This is the preferred format - sprites and behaviors are specified directly in Tiled.
///
///     Required Tiled properties:
///     - type = "npc"
///     - spriteId (string): Sprite ID in format "category/spritename"
///
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
        var props = context.TiledObject.Properties;
        var obj = context.TiledObject;

        // Parse required spriteId - format: "npcs/generic_twin" or full "base:sprite:npcs/generic_twin"
        string spriteIdStr = TiledPropertyParser.GetRequiredString(props, "spriteId", errorContext);
        GameSpriteId spriteId;
        if (spriteIdStr.Contains(':'))
        {
            // Full ID format
            spriteId = new GameSpriteId(spriteIdStr);
        }
        else
        {
            // Path format - add base:sprite: prefix
            spriteId = new GameSpriteId($"base:sprite:{spriteIdStr}");
        }
        context.RegisterSpriteId(spriteId);

        // Get tile position
        var (tileX, tileY) = context.GetTilePosition();

        // Parse optional elevation (fail-fast on invalid format)
        byte elevation = TiledPropertyParser.GetOptionalByte(props, "elevation", errorContext) ?? 0;

        // Parse optional direction (fail-fast on invalid format)
        Direction direction = TiledPropertyParser.GetOptionalDirection(props, "direction", errorContext)
                              ?? Direction.South;

        // Generate NPC ID from object name
        string npcId = !string.IsNullOrWhiteSpace(obj.Name) ? obj.Name : $"npc_{obj.Id}";

        // Determine initial animation based on facing direction
        string initialAnimation = direction switch
        {
            Direction.North => "face_north",
            Direction.South => "face_south",
            Direction.East => "face_east",
            Direction.West => "face_west",
            _ => "face_south"
        };

        // Parse optional visibility flag (FLAG_HIDE_* pattern from pokeemerald)
        string? visibilityFlagStr = TiledPropertyParser.GetOptionalString(props, "visibilityFlag");
        bool hideWhenFlagSet = TiledPropertyParser.GetBoolOrFalse(props, "hideWhenFlagSet", errorContext)
                               || !TiledPropertyParser.HasProperty(props, "hideWhenFlagSet"); // Default to true

        // Determine initial visibility based on flag state
        bool shouldBeVisible = true;
        GameFlagId? visibilityFlagId = null;

        if (!string.IsNullOrWhiteSpace(visibilityFlagStr))
        {
            // Parse flag ID - support both full format and shorthand
            if (visibilityFlagStr.Contains(':'))
            {
                visibilityFlagId = new GameFlagId(visibilityFlagStr);
            }
            else
            {
                // Shorthand format - add base:flag: prefix
                visibilityFlagId = new GameFlagId($"base:flag:{visibilityFlagStr}");
            }

            // Check current flag state if GameStateApi is available
            if (context.GameStateApi != null)
            {
                bool flagValue = context.GameStateApi.GetFlag(visibilityFlagId.Value);
                // hideWhenFlagSet=true: flag=true -> hidden, flag=false -> visible
                // hideWhenFlagSet=false: flag=true -> visible, flag=false -> hidden
                shouldBeVisible = hideWhenFlagSet ? !flagValue : flagValue;
            }
        }

        // Create NPC entity with core components (same as npc/base + npc/generic templates)
        Entity npcEntity = context.World.Create(
            new Position(tileX, tileY, context.MapId, context.TileHeight),
            new Sprite(spriteId),
            new Npc(GameNpcId.Create(npcId)),
            new Elevation(elevation),
            direction,
            new Animation(initialAnimation),
            new Collision(true),
            new GridMovement(3.75f) // MOVE_SPEED_NORMAL
        );

        // Add Visible component only if entity should be visible
        if (shouldBeVisible)
        {
            npcEntity.Add(new Visible());
        }

        // Add VisibilityFlag component if flag is specified (enables runtime toggling)
        if (visibilityFlagId != null)
        {
            npcEntity.Add(new VisibilityFlag(visibilityFlagId, hideWhenFlagSet));
            _logger?.LogDebug(
                "NPC '{Name}' linked to visibility flag {FlagId} (hideWhenSet={Hide})",
                obj.Name, visibilityFlagId.Value, hideWhenFlagSet);
        }

        // Add behavior if specified
        string? behaviorId = TiledPropertyParser.GetOptionalString(props, "behaviorId");
        if (!string.IsNullOrWhiteSpace(behaviorId))
        {
            npcEntity.Add(new Behavior(behaviorId));
            ApplyBehaviorExtras(npcEntity, props, behaviorId, tileX, tileY, errorContext);
        }

        // Add movement range for wander behaviors
        ApplyMovementRange(npcEntity, props, tileX, tileY, errorContext);

        // Handle trainer data
        ApplyTrainerData(npcEntity, props, errorContext);

        // Add name if specified
        if (!string.IsNullOrWhiteSpace(obj.Name))
        {
            npcEntity.Add(new Name(obj.Name));
        }

        // Add parent relationship
        context.MapInfoEntity.AddRelationship(npcEntity, new ParentOf());

        _logger?.LogDebug(
            "Spawned NPC '{Name}' at ({TileX}, {TileY}) with sprite {SpriteId}",
            obj.Name, tileX, tileY, spriteIdStr);

        return npcEntity;
    }

    private void ApplyBehaviorExtras(
        Entity npcEntity,
        Dictionary<string, object> props,
        string behaviorId,
        int tileX,
        int tileY,
        string errorContext)
    {
        // Generate waypoints for patrol behavior when axis is specified but no explicit waypoints
        if (!behaviorId.Contains("patrol"))
        {
            return;
        }

        if (TiledPropertyParser.HasProperty(props, "waypoints"))
        {
            // Explicit waypoints - parse them
            Point[] waypoints = TiledPropertyParser.GetOptionalWaypoints(props, "waypoints", errorContext);
            if (waypoints.Length > 0)
            {
                float waitTime = TiledPropertyParser.GetOptionalFloat(props, "waypointWaitTime", errorContext) ?? 1.0f;
                npcEntity.Add(new MovementRoute(waypoints, loop: true, waitTime));
            }
            return;
        }

        string? axis = TiledPropertyParser.GetOptionalString(props, "axis");
        if (string.IsNullOrEmpty(axis))
        {
            return;
        }

        axis = axis.ToLowerInvariant();

        // Get range - check axis-specific range first, then general range
        int range;
        if (axis == "horizontal" && TiledPropertyParser.HasProperty(props, "rangeX"))
        {
            range = TiledPropertyParser.GetRequiredInt(props, "rangeX", errorContext);
        }
        else if (axis == "vertical" && TiledPropertyParser.HasProperty(props, "rangeY"))
        {
            range = TiledPropertyParser.GetRequiredInt(props, "rangeY", errorContext);
        }
        else
        {
            range = TiledPropertyParser.GetOptionalInt(props, "range", errorContext) ?? 2;
        }

        range = Math.Max(1, range); // Minimum of 1

        // Generate waypoints
        Point[] generatedWaypoints = axis == "horizontal"
            ? new[] { new Point(tileX, tileY), new Point(tileX + range, tileY) }
            : new[] { new Point(tileX, tileY), new Point(tileX, tileY + range) };

        float waypointWaitTime = TiledPropertyParser.GetOptionalFloat(props, "waypointWaitTime", errorContext) ?? 1.0f;
        npcEntity.Add(new MovementRoute(generatedWaypoints, loop: true, waypointWaitTime));

        _logger?.LogDebug(
            "Generated {Axis} patrol waypoints: ({X1},{Y1}) → ({X2},{Y2})",
            axis,
            generatedWaypoints[0].X, generatedWaypoints[0].Y,
            generatedWaypoints[1].X, generatedWaypoints[1].Y);
    }

    private void ApplyMovementRange(
        Entity npcEntity,
        Dictionary<string, object> props,
        int tileX,
        int tileY,
        string errorContext)
    {
        // Both rangeX and rangeY must be present for movement range
        if (!TiledPropertyParser.HasProperty(props, "rangeX") ||
            !TiledPropertyParser.HasProperty(props, "rangeY"))
        {
            return;
        }

        int rangeX = TiledPropertyParser.GetRequiredInt(props, "rangeX", errorContext);
        int rangeY = TiledPropertyParser.GetRequiredInt(props, "rangeY", errorContext);

        npcEntity.Add(new MovementRange(rangeX, rangeY, tileX, tileY));
    }

    private void ApplyTrainerData(
        Entity npcEntity,
        Dictionary<string, object> props,
        string errorContext)
    {
        string? trainerType = TiledPropertyParser.GetOptionalString(props, "trainerType");
        if (string.IsNullOrWhiteSpace(trainerType) || trainerType == "TRAINER_TYPE_NONE")
        {
            return;
        }

        ref var npcComp = ref npcEntity.Get<Npc>();
        npcComp.IsTrainer = true;

        // Set sight range if specified
        int? sightRange = TiledPropertyParser.GetOptionalInt(props, "sightRange", errorContext);
        if (sightRange.HasValue)
        {
            npcComp.ViewRange = sightRange.Value;
        }
    }
}
