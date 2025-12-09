using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Registry lookup service implementation.
///     Provides read-only access to game definitions and type registries.
/// </summary>
public class RegistryApiService(
    IBehaviorRegistry behaviorRegistry,
    NpcDefinitionService npcDefinitionService,
    MapRegistry mapRegistry,
    SpriteRegistry spriteRegistry,
    IEntityFactoryService entityFactoryService,
    ILogger<RegistryApiService> logger
) : IRegistryApi
{
    private readonly IBehaviorRegistry _behaviorRegistry =
        behaviorRegistry ?? throw new ArgumentNullException(nameof(behaviorRegistry));

    private readonly NpcDefinitionService _npcDefinitionService =
        npcDefinitionService ?? throw new ArgumentNullException(nameof(npcDefinitionService));

    private readonly MapRegistry _mapRegistry =
        mapRegistry ?? throw new ArgumentNullException(nameof(mapRegistry));

    private readonly SpriteRegistry _spriteRegistry =
        spriteRegistry ?? throw new ArgumentNullException(nameof(spriteRegistry));

    private readonly IEntityFactoryService _entityFactoryService =
        entityFactoryService ?? throw new ArgumentNullException(nameof(entityFactoryService));

    private readonly ILogger<RegistryApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    #region NPC Registry

    /// <inheritdoc />
    public NpcDefinition? GetNpcDefinition(GameNpcId npcId)
    {
        ArgumentNullException.ThrowIfNull(npcId);

        _logger.LogDebug("Looking up NPC definition: {NpcId}", npcId);

        // Use NpcDefinitionService for cached lookup
        return _npcDefinitionService.GetNpc(npcId);
    }

    /// <inheritdoc />
    public IEnumerable<GameNpcId> GetAllNpcIds()
    {
        _logger.LogDebug("Getting all NPC IDs");

        // Note: This requires async call to database - using synchronous warning pattern
        _logger.LogWarning(
            "GetAllNpcIds requires database query - consider using async GetNpcsByTypeAsync instead"
        );

        // Return empty collection for now - proper implementation would need async support
        return [];
    }

    /// <inheritdoc />
    public IEnumerable<GameNpcId> GetNpcIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting NPC IDs by category: {Category}", category);

        // Note: NpcDefinitionService uses "NpcType" for categorization
        // Async method GetNpcsByTypeAsync is available but not accessible from sync context
        _logger.LogWarning(
            "GetNpcIdsByCategory requires async database query - returning empty collection"
        );

        return [];
    }

    /// <inheritdoc />
    public bool NpcExists(GameNpcId npcId)
    {
        ArgumentNullException.ThrowIfNull(npcId);

        return _npcDefinitionService.HasNpc(npcId);
    }

    #endregion

    #region Trainer Registry

    /// <inheritdoc />
    public TrainerDefinition? GetTrainerDefinition(GameTrainerId trainerId)
    {
        ArgumentNullException.ThrowIfNull(trainerId);

        _logger.LogDebug("Looking up trainer definition: {TrainerId}", trainerId);

        // Use NpcDefinitionService for cached trainer lookup
        return _npcDefinitionService.GetTrainer(trainerId);
    }

    /// <inheritdoc />
    public IEnumerable<GameTrainerId> GetAllTrainerIds()
    {
        _logger.LogDebug("Getting all trainer IDs");

        // Note: This requires async call to database
        _logger.LogWarning(
            "GetAllTrainerIds requires database query - consider using async GetTrainersByClassAsync instead"
        );

        // Return empty collection for now - proper implementation would need async support
        return [];
    }

    /// <inheritdoc />
    public IEnumerable<GameTrainerId> GetTrainerIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting trainer IDs by category: {Category}", category);

        // Note: NpcDefinitionService uses "TrainerClass" for categorization
        // Async method GetTrainersByClassAsync is available but not accessible from sync context
        _logger.LogWarning(
            "GetTrainerIdsByCategory requires async database query - returning empty collection"
        );

        return [];
    }

    /// <inheritdoc />
    public bool TrainerExists(GameTrainerId trainerId)
    {
        ArgumentNullException.ThrowIfNull(trainerId);

        return _npcDefinitionService.HasTrainer(trainerId);
    }

    #endregion

    #region Sprite Registry

    /// <inheritdoc />
    public IEnumerable<GameSpriteId> GetAllSpriteIds()
    {
        _logger.LogDebug("Getting all sprite IDs");

        // Use SpriteRegistry to get all registered sprite IDs
        return _spriteRegistry.GetAllSpriteIds();
    }

    /// <inheritdoc />
    public IEnumerable<GameSpriteId> GetSpriteIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting sprite IDs by category: {Category}", category);

        // Filter sprites by category from the ID path
        // GameSpriteId format: "base:sprite:category/name"
        return GetAllSpriteIds().Where(s => s.SpriteCategory == category);
    }

    /// <inheritdoc />
    public bool SpriteExists(GameSpriteId spriteId)
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        // Check SpriteRegistry for sprite existence
        return _spriteRegistry.TryGetSprite(spriteId, out _);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetSpriteAnimationNames(GameSpriteId spriteId)
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        if (_spriteRegistry.TryGetSprite(spriteId, out var definition) && definition != null)
        {
            return definition.Animations.Select(a => a.Name);
        }

        return [];
    }

    /// <inheritdoc />
    public bool SpriteHasAnimations(GameSpriteId spriteId, params string[] animationNames)
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        if (animationNames.Length == 0)
            return true;

        if (_spriteRegistry.TryGetSprite(spriteId, out var definition) && definition != null)
        {
            var availableAnimations = definition.Animations.Select(a => a.Name).ToHashSet();
            return animationNames.All(name => availableAnimations.Contains(name));
        }

        return false;
    }

    /// <inheritdoc />
    public bool HasNpcAnimations(GameSpriteId spriteId)
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        if (_spriteRegistry.TryGetSprite(spriteId, out var definition) && definition != null)
        {
            var animations = definition.Animations.Select(a => a.Name).ToHashSet();

            // Check for required NPC animations: go_* and face_* for all directions
            bool hasGoAnimations = animations.Contains("go_south") ||
                                   animations.Contains("go_north") ||
                                   animations.Contains("go_east") ||
                                   animations.Contains("go_west");

            bool hasFaceAnimations = animations.Contains("face_south") ||
                                     animations.Contains("face_north") ||
                                     animations.Contains("face_east") ||
                                     animations.Contains("face_west");

            return hasGoAnimations && hasFaceAnimations;
        }

        return false;
    }

    #endregion

    #region Behavior Registry

    /// <inheritdoc />
    public BehaviorDefinition? GetBehaviorDefinition(GameBehaviorId behaviorId)
    {
        ArgumentNullException.ThrowIfNull(behaviorId);

        _logger.LogDebug("Looking up behavior definition: {BehaviorId}", behaviorId);

        return _behaviorRegistry.GetBehavior(behaviorId);
    }

    /// <inheritdoc />
    public IEnumerable<GameBehaviorId> GetAllBehaviorIds()
    {
        _logger.LogDebug("Getting all behavior IDs");

        // Convert string IDs to GameBehaviorId
        foreach (var id in _behaviorRegistry.GetAllBehaviorIds())
        {
            var behaviorId = GameBehaviorId.TryCreate(id);
            if (behaviorId != null)
                yield return behaviorId;
        }
    }

    /// <inheritdoc />
    public IEnumerable<GameBehaviorId> GetBehaviorIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting behavior IDs by category: {Category}", category);

        return GetAllBehaviorIds().Where(b => b.BehaviorCategory == category);
    }

    /// <inheritdoc />
    public bool BehaviorExists(GameBehaviorId behaviorId)
    {
        ArgumentNullException.ThrowIfNull(behaviorId);

        return _behaviorRegistry.HasBehavior(behaviorId);
    }

    #endregion

    #region Map Registry

    /// <inheritdoc />
    public IEnumerable<GameMapId> GetAllMapIds()
    {
        _logger.LogDebug("Getting all map IDs");

        // Note: MapRegistry only tracks currently loaded maps
        // There's no central registry of all available maps yet
        _logger.LogWarning(
            "GetAllMapIds only returns currently loaded maps - full map enumeration not yet implemented"
        );

        return _mapRegistry.GetLoadedMapIds();
    }

    /// <inheritdoc />
    public IEnumerable<GameMapId> GetMapIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting map IDs by category/region: {Category}", category);

        // Filter loaded maps by category/region from the ID
        // GameMapId format: "base:map:region/mapname"
        return GetAllMapIds().Where(m => m.Region == category);
    }

    /// <inheritdoc />
    public bool MapExists(GameMapId mapId)
    {
        ArgumentNullException.ThrowIfNull(mapId);

        // Check if map is currently loaded
        // Note: MapRegistry doesn't track all available maps, only loaded ones
        return _mapRegistry.IsMapLoaded(mapId);
    }

    #endregion

    #region Template Registry

    /// <inheritdoc />
    public IEnumerable<string> GetAllTemplateIds()
    {
        _logger.LogDebug("Getting all template IDs");

        // Note: EntityFactoryService doesn't expose GetAllTemplateIds directly
        // We could get all by iterating through empty tag filter, but that's not ideal
        _logger.LogWarning(
            "GetAllTemplateIds not directly supported - use GetTemplateIdsByTag for specific entity types"
        );

        return [];
    }

    /// <inheritdoc />
    public IEnumerable<string> GetTemplateIdsByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return [];

        _logger.LogDebug("Getting template IDs by tag: {Tag}", tag);

        // Use EntityFactoryService to get templates by tag
        return _entityFactoryService.GetTemplateIdsByTag(tag);
    }

    /// <inheritdoc />
    public bool TemplateExists(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return false;

        // Check EntityFactoryService template cache
        return _entityFactoryService.HasTemplate(templateId);
    }

    #endregion

    #region Flag Registry

    /// <inheritdoc />
    public IEnumerable<GameFlagId> GetAllFlagIds()
    {
        _logger.LogDebug("Getting all predefined flag IDs");

        // Note: Flags are dynamic and not stored in a central registry
        // They're created on-the-fly when referenced in scripts/maps
        // There's no central flag definition service yet
        _logger.LogWarning(
            "Flag registry not yet implemented - flags are created dynamically when referenced"
        );

        return [];
    }

    /// <inheritdoc />
    public IEnumerable<GameFlagId> GetFlagIdsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return [];

        _logger.LogDebug("Getting flag IDs by category: {Category}", category);

        // Would filter GetAllFlagIds if it were implemented
        return GetAllFlagIds().Where(f => f.Category == category);
    }

    #endregion
}
