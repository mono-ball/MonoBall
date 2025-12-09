using Arch.Core;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameData.Services;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Spawns entities from templates using IEntityFactoryService.
///     Fallback for objects that specify a template ID but aren't handled by specialized spawners.
///
///     Required Tiled properties:
///     - type (string): Template ID (e.g., "npc/generic", "item/pokeball")
///
///     Optional property overrides:
///     - direction (string): Override facing direction
///     - elevation (int): Override elevation
///     - npcId (string): For NPC templates - load from NpcDefinitionService
///     - trainerId (string): For trainer templates - load from trainer definitions
/// </summary>
public sealed class TemplateEntitySpawner : IEntitySpawner
{
    private readonly IEntityFactoryService _entityFactory;
    private readonly NpcDefinitionService? _npcDefinitionService;
    private readonly ILogger<TemplateEntitySpawner>? _logger;

    public TemplateEntitySpawner(
        IEntityFactoryService entityFactory,
        NpcDefinitionService? npcDefinitionService = null,
        ILogger<TemplateEntitySpawner>? logger = null)
    {
        _entityFactory = entityFactory;
        _npcDefinitionService = npcDefinitionService;
        _logger = logger;
    }

    public string Name => "TemplateEntitySpawner";

    // Lower priority than specialized spawners
    public int Priority => 50;

    public bool CanSpawn(EntitySpawnContext context)
    {
        string? templateId = GetTemplateId(context);
        if (string.IsNullOrEmpty(templateId))
        {
            return false;
        }

        // Don't handle if template doesn't exist
        return _entityFactory.HasTemplate(templateId);
    }

    public Entity Spawn(EntitySpawnContext context)
    {
        string errorContext = context.CreateErrorContext();
        var props = context.TiledObject.Properties;
        var obj = context.TiledObject;

        // Get template ID (required for this spawner)
        string templateId = GetTemplateId(context)!;

        // Get tile position
        var (tileX, tileY) = context.GetTilePosition();

        // Spawn from template
        Entity entity = _entityFactory.SpawnFromTemplate(
            templateId,
            context.World,
            builder =>
            {
                // Override position with map coordinates
                builder.OverrideComponent(new Position(tileX, tileY, context.MapId, context.TileHeight));

                // Apply elevation override if specified
                byte? elevation = TiledPropertyParser.GetOptionalByte(props, "elevation", errorContext);
                if (elevation.HasValue)
                {
                    builder.OverrideComponent(new Elevation(elevation.Value));
                }

                // Apply direction override if specified
                Direction? direction = TiledPropertyParser.GetOptionalDirection(props, "direction", errorContext);
                if (direction.HasValue)
                {
                    builder.OverrideComponent(direction.Value);
                }

                // Handle NPC/Trainer definitions for legacy support
                if (templateId.StartsWith("npc/") || templateId.StartsWith("trainer/"))
                {
                    ApplyNpcDefinition(builder, props, context.RequiredSpriteIds, errorContext);
                }
            });

        // Add parent relationship
        context.MapInfoEntity.AddRelationship(entity, new ParentOf());

        _logger?.LogDebug(
            "Spawned '{ObjectName}' ({TemplateId}) at ({X}, {Y})",
            obj.Name, templateId, tileX, tileY);

        return entity;
    }

    private static string? GetTemplateId(EntitySpawnContext context)
    {
        // Template ID comes from object type or explicit "template" property
        string? templateId = context.TiledObject.Type;
        if (string.IsNullOrEmpty(templateId))
        {
            templateId = TiledPropertyParser.GetOptionalString(context.TiledObject.Properties, "template");
        }
        return templateId;
    }

    /// <summary>
    ///     Apply NPC/Trainer definition data to entity builder (legacy support).
    /// </summary>
    private void ApplyNpcDefinition(
        EntityBuilder builder,
        Dictionary<string, object> props,
        HashSet<GameSpriteId>? requiredSpriteIds,
        string errorContext)
    {
        // Check for npcId to load from NpcDefinitionService
        string? npcIdStr = TiledPropertyParser.GetOptionalString(props, "npcId");
        if (!string.IsNullOrWhiteSpace(npcIdStr) && _npcDefinitionService != null)
        {
            GameNpcId npcId = GameNpcId.Create(npcIdStr);
            var npcDef = _npcDefinitionService.GetNpc(npcId);

            if (npcDef != null)
            {
                // Apply NPC definition data
                if (npcDef.SpriteId != null)
                {
                    requiredSpriteIds?.Add(npcDef.SpriteId);
                    builder.OverrideComponent(new Sprite(npcDef.SpriteId));
                }

                if (!string.IsNullOrEmpty(npcDef.BehaviorScript))
                {
                    builder.OverrideComponent(new Behavior(npcDef.BehaviorScript));
                }

                // Create Npc component with the ID (IsTrainer/ViewRange are set via Tiled properties, not NpcDefinition)
                builder.OverrideComponent(new Npc(npcId));
            }
            else
            {
                throw new InvalidDataException(
                    $"NPC definition '{npcIdStr}' not found. Context: {errorContext}");
            }
        }

        // Check for trainerId similarly (legacy support)
        string? trainerIdStr = TiledPropertyParser.GetOptionalString(props, "trainerId");
        if (!string.IsNullOrWhiteSpace(trainerIdStr))
        {
            // TODO: Implement trainer definition lookup when TrainerDefinitionService exists
            _logger?.LogWarning(
                "trainerId '{TrainerId}' specified but TrainerDefinitionService not implemented. Context: {Context}",
                trainerIdStr, errorContext);
        }
    }
}
