using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.Common.Logging;
using PokeSharp.Game.Engine.Core.Types;
using PokeSharp.Game.Engine.Systems.Factories;
using PokeSharp.Game.Components.Common;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Relationships;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Data.Entities;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.Services;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Handles spawning of map objects (NPCs, items, triggers, etc.) from Tiled object layers.
///     Supports both definition-based (EF Core) and manual property-based object creation.
/// </summary>
public class MapObjectSpawner
{
    private readonly IEntityFactoryService? _entityFactory;
    private readonly ILogger<MapObjectSpawner>? _logger;
    private readonly NpcDefinitionService? _npcDefinitionService;

    public MapObjectSpawner(
        IEntityFactoryService? entityFactory = null,
        NpcDefinitionService? npcDefinitionService = null,
        ILogger<MapObjectSpawner>? logger = null
    )
    {
        _entityFactory = entityFactory;
        _npcDefinitionService = npcDefinitionService;
        _logger = logger;
    }

    /// <summary>
    ///     Spawns entities from map objects (NPCs, items, triggers, etc.).
    ///     Objects must have a "type" property indicating entity template (e.g., "npc/generic").
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapInfoEntity">The map info entity for establishing relationships.</param>
    /// <param name="mapId">The map runtime ID.</param>
    /// <param name="tileWidth">Tile width for X coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for Y coordinate conversion.</param>
    /// <param name="requiredSpriteIds">Collection to track sprite IDs for lazy loading.</param>
    /// <returns>Number of entities created from objects.</returns>
    public int SpawnMapObjects(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        MapRuntimeId mapId,
        int tileWidth,
        int tileHeight,
        HashSet<SpriteId>? requiredSpriteIds = null
    )
    {
        int created = 0;

        foreach (TmxObjectGroup objectGroup in tmxDoc.ObjectGroups)
        foreach (TmxObject obj in objectGroup.Objects)
        {
            // Get template ID from object type or properties
            string? templateId = obj.Type;
            if (
                string.IsNullOrEmpty(templateId)
                && obj.Properties.TryGetValue("template", out object? templateProp)
            )
            {
                templateId = templateProp.ToString();
            }

            // Handle warp_event objects specially (no template required)
            if (templateId == "warp_event")
            {
                if (TrySpawnWarpEntity(world, obj, mapInfoEntity, mapId, tileWidth, tileHeight))
                {
                    created++;
                }

                continue;
            }

            if (string.IsNullOrEmpty(templateId))
            {
                _logger?.LogOperationSkipped($"Object '{obj.Name}'", "no type/template");
                continue;
            }

            // Check if template exists
            if (_entityFactory == null || !_entityFactory.HasTemplate(templateId))
            {
                if (_entityFactory != null)
                {
                    _logger?.LogResourceNotFound("Template", $"{templateId} for '{obj.Name}'");
                }

                continue;
            }

            // Convert pixel coordinates to tile coordinates
            // Tiled Y coordinate is from top of object, use top-left corner for positioning
            int tileX = (int)Math.Floor(obj.X / tileWidth);
            int tileY = (int)Math.Floor(obj.Y / tileHeight);

            try
            {
                // Spawn entity from template
                Entity entity = _entityFactory.SpawnFromTemplate(
                    templateId,
                    world,
                    builder =>
                    {
                        // Override position with map coordinates
                        builder.OverrideComponent(new Position(tileX, tileY, mapId, tileHeight));

                        // Apply custom elevation if specified (Pokemon Emerald style)
                        if (obj.Properties.TryGetValue("elevation", out object? elevProp))
                        {
                            byte elevValue = Convert.ToByte(elevProp);
                            builder.OverrideComponent(new Elevation(elevValue));
                        }

                        // Apply any custom properties from the object
                        if (obj.Properties.TryGetValue("direction", out object? dirProp))
                        {
                            string? dirStr = dirProp.ToString()?.ToLower();
                            Direction direction = dirStr switch
                            {
                                "north" or "up" => Direction.North,
                                "south" or "down" => Direction.South,
                                "west" or "left" => Direction.West,
                                "east" or "right" => Direction.East,
                                _ => Direction.South,
                            };
                            builder.OverrideComponent(direction);
                        }

                        // Handle NPC/Trainer definitions (NEW: uses EF Core definitions)
                        if (templateId.StartsWith("npc/") || templateId.StartsWith("trainer/"))
                        {
                            ApplyNpcDefinition(builder, obj, templateId, requiredSpriteIds);
                        }
                    }
                );

                // Add ParentOf relationship - map is parent of spawned objects
                mapInfoEntity.AddRelationship(entity, new ParentOf());

                _logger?.LogDebug(
                    "Spawned '{ObjectName}' ({TemplateId}) at ({X}, {Y})",
                    obj.Name,
                    templateId,
                    tileX,
                    tileY
                );
                created++;
            }
            catch (Exception ex)
            {
                _logger?.LogExceptionWithContext(
                    ex,
                    "Failed to spawn '{ObjectName}' from template '{TemplateId}'",
                    obj.Name,
                    templateId
                );
            }
        }

        // Log warp count
        if (mapInfoEntity.Has<MapWarps>())
        {
            MapWarps warps = mapInfoEntity.Get<MapWarps>();
            if (warps.Count > 0)
            {
                _logger?.LogDebug("Map has {Count} warps registered in spatial index", warps.Count);
            }
        }

        return created;
    }

    /// <summary>
    ///     Apply NPC/Trainer definition data from EF Core to entity builder.
    ///     Supports both NPC definitions and Trainer definitions.
    /// </summary>
    private void ApplyNpcDefinition(
        EntityBuilder builder,
        TmxObject obj,
        string templateId,
        HashSet<SpriteId>? requiredSpriteIds
    )
    {
        // Check for NPC definition reference
        if (obj.Properties.TryGetValue("npcId", out object? npcIdProp))
        {
            string? npcId = npcIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                NpcDefinition? npcDef = _npcDefinitionService?.GetNpc(npcId);
                if (npcDef != null)
                {
                    // Apply definition data
                    builder.OverrideComponent(new Npc(npcId));
                    builder.OverrideComponent(new Name(npcDef.DisplayName));

                    if (npcDef.SpriteId.HasValue)
                    {
                        SpriteId spriteId = npcDef.SpriteId.Value;
                        // PHASE 2: Collect sprite ID for lazy loading
                        requiredSpriteIds?.Add(spriteId);
                        _logger?.LogTrace(
                            "Collected sprite ID for lazy loading: {SpriteId}",
                            spriteId.Value
                        );

                        // Use SpriteId properties directly
                        builder.OverrideComponent(
                            new Sprite(spriteId.SpriteName, spriteId.Category)
                        );
                    }

                    builder.OverrideComponent(new GridMovement(npcDef.MovementSpeed));

                    if (!string.IsNullOrEmpty(npcDef.BehaviorScript))
                    {
                        builder.OverrideComponent(new Behavior(npcDef.BehaviorScript));
                        _logger?.LogWorkflowStatus(
                            "Added Behavior component",
                            ("typeId", npcDef.BehaviorScript),
                            ("npcId", npcId)
                        );
                    }

                    _logger?.LogWorkflowStatus(
                        "Applied NPC definition",
                        ("npcId", npcId),
                        ("displayName", npcDef.DisplayName),
                        ("behavior", npcDef.BehaviorScript ?? "none")
                    );
                }
                else
                {
                    _logger?.LogWarning(
                        "NPC definition not found: '{NpcId}' (falling back to map properties)",
                        npcId
                    );
                    // Fall back to manual property parsing
                    ApplyManualNpcProperties(builder, obj);
                }
            }
        }
        // Check for Trainer definition reference
        else if (obj.Properties.TryGetValue("trainerId", out object? trainerIdProp))
        {
            string? trainerId = trainerIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(trainerId))
            {
                TrainerDefinition? trainerDef = _npcDefinitionService?.GetTrainer(trainerId);
                if (trainerDef != null)
                {
                    // Apply trainer definition data
                    builder.OverrideComponent(new Name(trainerDef.DisplayName));

                    if (trainerDef.SpriteId.HasValue)
                    {
                        SpriteId spriteId = trainerDef.SpriteId.Value;
                        // PHASE 2: Collect sprite ID for lazy loading
                        requiredSpriteIds?.Add(spriteId);
                        _logger?.LogTrace(
                            "Collected sprite ID for lazy loading: {SpriteId}",
                            spriteId.Value
                        );

                        // Use SpriteId properties directly
                        builder.OverrideComponent(
                            new Sprite(spriteId.SpriteName, spriteId.Category)
                        );
                    }

                    // Add trainer-specific component (when Trainer component exists)
                    // For now, just use Npc component with trainerId
                    builder.OverrideComponent(new Npc(trainerId));

                    _logger?.LogDebug(
                        "Applied Trainer definition '{TrainerId}' ({DisplayName})",
                        trainerId,
                        trainerDef.DisplayName
                    );

                    // TODO: When battle system is implemented, deserialize party:
                    // var party = JsonSerializer.Deserialize<List<TrainerPartyMemberDto>>(
                    //     trainerDef.PartyJson
                    // );
                }
                else
                {
                    _logger?.LogWarning("Trainer definition not found: '{TrainerId}'", trainerId);
                }
            }
        }
        else
        {
            // No definition reference - use manual properties (backward compatibility)
            ApplyManualNpcProperties(builder, obj);
        }

        // Always apply map-level overrides (waypoints, custom properties)
        ApplyMapLevelOverrides(builder, obj);
    }

    /// <summary>
    ///     Apply manual NPC properties from map (backward compatibility).
    ///     Used when no definition is referenced or definition not found.
    /// </summary>
    private void ApplyManualNpcProperties(EntityBuilder builder, TmxObject obj)
    {
        // Manual npcId from map
        if (obj.Properties.TryGetValue("npcId", out object? npcIdProp))
        {
            string? npcId = npcIdProp?.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                builder.OverrideComponent(new Npc(npcId));
            }
        }

        // Manual displayName from map
        if (obj.Properties.TryGetValue("displayName", out object? displayNameProp))
        {
            string? displayName = displayNameProp?.ToString();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                builder.OverrideComponent(new Name(displayName));
            }
        }

        // Manual movement speed from map
        if (obj.Properties.TryGetValue("movementSpeed", out object? speedProp))
        {
            if (float.TryParse(speedProp.ToString(), out float speed))
            {
                builder.OverrideComponent(new GridMovement(speed));
            }
        }
    }

    /// <summary>
    ///     Apply map-level overrides (waypoints, custom properties).
    ///     These override definition data.
    /// </summary>
    private void ApplyMapLevelOverrides(EntityBuilder builder, TmxObject obj)
    {
        // Movement route (waypoints) - instance-specific
        if (obj.Properties.TryGetValue("waypoints", out object? waypointsProp))
        {
            string? waypointsStr = waypointsProp.ToString();
            if (!string.IsNullOrEmpty(waypointsStr))
            {
                // Parse waypoints: "x1,y1;x2,y2;x3,y3"
                var points = new List<Point>();
                string[] pairs = waypointsStr.Split(';');
                foreach (string pair in pairs)
                {
                    string[] coords = pair.Split(',');
                    if (
                        coords.Length == 2
                        && int.TryParse(coords[0].Trim(), out int x)
                        && int.TryParse(coords[1].Trim(), out int y)
                    )
                    {
                        points.Add(new Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    float waypointWaitTime = 1.0f;
                    if (
                        obj.Properties.TryGetValue("waypointWaitTime", out object? waitProp)
                        && float.TryParse(waitProp.ToString(), out float waitTime)
                    )
                    {
                        waypointWaitTime = waitTime;
                    }

                    builder.OverrideComponent(
                        new MovementRoute(points.ToArray(), true, waypointWaitTime)
                    );

                    _logger?.LogDebug("Applied waypoint route with {Count} points", points.Count);
                }
            }
        }
    }

    /// <summary>
    ///     Spawns a warp entity from a warp_event Tiled object.
    ///     Creates entity with Position, WarpPoint, and BelongsToMap components.
    ///     Registers warp in MapWarps spatial index for O(1) lookup.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="obj">The Tiled object with warp_event type.</param>
    /// <param name="mapInfoEntity">The map entity for relationship and spatial index.</param>
    /// <param name="mapId">The current map's runtime ID.</param>
    /// <param name="tileWidth">Tile width for coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for coordinate conversion.</param>
    /// <returns>True if warp entity was created successfully.</returns>
    private bool TrySpawnWarpEntity(
        World world,
        TmxObject obj,
        Entity mapInfoEntity,
        MapRuntimeId mapId,
        int tileWidth,
        int tileHeight
    )
    {
        // Get the warp property (nested class object)
        if (!obj.Properties.TryGetValue("warp", out object? warpProp))
        {
            _logger?.LogWarning("warp_event '{Name}' missing 'warp' property, skipping", obj.Name);
            return false;
        }

        // Parse warp data from Dictionary (parsed from Tiled class property)
        if (warpProp is not Dictionary<string, object?> warpData)
        {
            _logger?.LogWarning(
                "warp_event '{Name}' has invalid 'warp' property format, expected Dictionary",
                obj.Name
            );
            return false;
        }

        // Extract warp destination data
        string? targetMap = warpData.TryGetValue("map", out object? mapVal)
            ? mapVal?.ToString()
            : null;

        if (string.IsNullOrEmpty(targetMap))
        {
            _logger?.LogWarning(
                "warp_event '{Name}' missing target map in warp property",
                obj.Name
            );
            return false;
        }

        int targetX =
            warpData.TryGetValue("x", out object? xVal) && xVal != null ? Convert.ToInt32(xVal) : 0;
        int targetY =
            warpData.TryGetValue("y", out object? yVal) && yVal != null ? Convert.ToInt32(yVal) : 0;

        // Note: Elevation is not used in Position struct - using default tile size instead
        // Pokemon Emerald elevation values in map files are often incorrect anyway

        // Convert pixel coordinates to tile coordinates
        int tileX = (int)Math.Floor(obj.X / tileWidth);
        int tileY = (int)Math.Floor(obj.Y / tileHeight);

        try
        {
            // Create warp entity with Position and WarpPoint components
            Entity warpEntity = world.Create(
                new Position(tileX, tileY, mapId, tileHeight),
                new WarpPoint(targetMap, targetX, targetY)
            );

            // Add ParentOf relationship - map is parent of warps
            mapInfoEntity.AddRelationship(warpEntity, new ParentOf());

            // Register warp in MapWarps spatial index for O(1) lookup
            ref MapWarps mapWarps = ref mapInfoEntity.Get<MapWarps>();
            if (!mapWarps.AddWarp(tileX, tileY, warpEntity))
            {
                _logger?.LogWarning(
                    "Warp at ({TileX}, {TileY}) overwrites existing warp - duplicate warp positions",
                    tileX,
                    tileY
                );
            }

            _logger?.LogDebug(
                "Created warp at ({TileX}, {TileY}) → {TargetMap} @ ({TargetX}, {TargetY})",
                tileX,
                tileY,
                targetMap,
                targetX,
                targetY
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogExceptionWithContext(
                ex,
                "Failed to create warp entity for '{ObjectName}'",
                obj.Name
            );
            return false;
        }
    }
}
