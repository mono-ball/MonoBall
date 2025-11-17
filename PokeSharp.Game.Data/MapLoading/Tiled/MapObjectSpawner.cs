using System;
using System.Collections.Generic;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Game.Components.Common;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.Services;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Handles spawning of map objects (NPCs, items, triggers, etc.) from Tiled object layers.
///     Supports both definition-based (EF Core) and manual property-based object creation.
/// </summary>
public class MapObjectSpawner
{
    private readonly IEntityFactoryService? _entityFactory;
    private readonly NpcDefinitionService? _npcDefinitionService;
    private readonly ILogger<MapObjectSpawner>? _logger;

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
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileWidth">Tile width for X coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for Y coordinate conversion.</param>
    /// <param name="requiredSpriteIds">Collection to track sprite IDs for lazy loading.</param>
    /// <returns>Number of entities created from objects.</returns>
    public int SpawnMapObjects(
        World world,
        TmxDocument tmxDoc,
        MapRuntimeId mapId,
        int tileWidth,
        int tileHeight,
        HashSet<SpriteId>? requiredSpriteIds = null
    )
    {
        if (_entityFactory == null)
            // No entity factory - can't spawn from templates
            return 0;

        var created = 0;

        foreach (var objectGroup in tmxDoc.ObjectGroups)
        foreach (var obj in objectGroup.Objects)
        {
            // Get template ID from object type or properties
            var templateId = obj.Type;
            if (
                string.IsNullOrEmpty(templateId)
                && obj.Properties.TryGetValue("template", out var templateProp)
            )
                templateId = templateProp.ToString();

            if (string.IsNullOrEmpty(templateId))
            {
                _logger?.LogOperationSkipped($"Object '{obj.Name}'", "no type/template");
                continue;
            }

            // Check if template exists
            if (!_entityFactory.HasTemplate(templateId))
            {
                _logger?.LogResourceNotFound("Template", $"{templateId} for '{obj.Name}'");
                continue;
            }

            // Convert pixel coordinates to tile coordinates
            // Tiled Y coordinate is from top of object, use top-left corner for positioning
            var tileX = (int)Math.Floor(obj.X / tileWidth);
            var tileY = (int)Math.Floor(obj.Y / tileHeight);

            try
            {
                // Spawn entity from template
                var entity = _entityFactory.SpawnFromTemplate(
                    templateId,
                    world,
                    builder =>
                    {
                        // Override position with map coordinates
                        builder.OverrideComponent(new Position(tileX, tileY, mapId, tileHeight));

                        // Apply custom elevation if specified (Pokemon Emerald style)
                        if (obj.Properties.TryGetValue("elevation", out var elevProp))
                        {
                            var elevValue = Convert.ToByte(elevProp);
                            builder.OverrideComponent(new Elevation(elevValue));
                        }

                        // Apply any custom properties from the object
                        if (obj.Properties.TryGetValue("direction", out var dirProp))
                        {
                            var dirStr = dirProp.ToString()?.ToLower();
                            var direction = dirStr switch
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
        if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
        {
            var npcId = npcIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                var npcDef = _npcDefinitionService?.GetNpc(npcId);
                if (npcDef != null)
                {
                    // Apply definition data
                    builder.OverrideComponent(new Npc(npcId));
                    builder.OverrideComponent(new Name(npcDef.DisplayName));

                    if (npcDef.SpriteId.HasValue)
                    {
                        var spriteId = npcDef.SpriteId.Value;
                        // PHASE 2: Collect sprite ID for lazy loading
                        requiredSpriteIds?.Add(spriteId);
                        _logger?.LogTrace("Collected sprite ID for lazy loading: {SpriteId}", spriteId.Value);

                        // Use SpriteId properties directly
                        builder.OverrideComponent(new Sprite(spriteId.SpriteName, spriteId.Category));
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
        else if (obj.Properties.TryGetValue("trainerId", out var trainerIdProp))
        {
            var trainerId = trainerIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(trainerId))
            {
                var trainerDef = _npcDefinitionService?.GetTrainer(trainerId);
                if (trainerDef != null)
                {
                    // Apply trainer definition data
                    builder.OverrideComponent(new Name(trainerDef.DisplayName));

                    if (trainerDef.SpriteId.HasValue)
                    {
                        var spriteId = trainerDef.SpriteId.Value;
                        // PHASE 2: Collect sprite ID for lazy loading
                        requiredSpriteIds?.Add(spriteId);
                        _logger?.LogTrace("Collected sprite ID for lazy loading: {SpriteId}", spriteId.Value);

                        // Use SpriteId properties directly
                        builder.OverrideComponent(new Sprite(spriteId.SpriteName, spriteId.Category));
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
        if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
        {
            var npcId = npcIdProp?.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                builder.OverrideComponent(new Npc(npcId));
            }
        }

        // Manual displayName from map
        if (obj.Properties.TryGetValue("displayName", out var displayNameProp))
        {
            var displayName = displayNameProp?.ToString();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                builder.OverrideComponent(new Name(displayName));
            }
        }

        // Manual movement speed from map
        if (obj.Properties.TryGetValue("movementSpeed", out var speedProp))
        {
            if (float.TryParse(speedProp.ToString(), out var speed))
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
        if (obj.Properties.TryGetValue("waypoints", out var waypointsProp))
        {
            var waypointsStr = waypointsProp.ToString();
            if (!string.IsNullOrEmpty(waypointsStr))
            {
                // Parse waypoints: "x1,y1;x2,y2;x3,y3"
                var points = new List<Point>();
                var pairs = waypointsStr.Split(';');
                foreach (var pair in pairs)
                {
                    var coords = pair.Split(',');
                    if (
                        coords.Length == 2
                        && int.TryParse(coords[0].Trim(), out var x)
                        && int.TryParse(coords[1].Trim(), out var y)
                    )
                    {
                        points.Add(new Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    var waypointWaitTime = 1.0f;
                    if (
                        obj.Properties.TryGetValue("waypointWaitTime", out var waitProp)
                        && float.TryParse(waitProp.ToString(), out var waitTime)
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

}

