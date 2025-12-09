using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Registry lookup API for accessing game definitions and IDs.
///     Provides read-only access to NPC, sprite, map, behavior, and template registries.
/// </summary>
/// <remarks>
///     <para>
///         This API enables scripts to query available game content and access definitions.
///         All methods are read-only - modifications to definitions should be done through
///         the modding system, not through scripts.
///     </para>
///     <para>
///         Definitions returned are the runtime data objects loaded from JSON/database.
///         Scripts can read properties but should not modify the returned objects.
///     </para>
/// </remarks>
public interface IRegistryApi
{
    #region NPC Registry

    /// <summary>
    ///     Gets an NPC definition by its ID.
    /// </summary>
    /// <param name="npcId">The NPC identifier.</param>
    /// <returns>The NPC definition, or null if not found.</returns>
    NpcDefinition? GetNpcDefinition(GameNpcId npcId);

    /// <summary>
    ///     Gets all registered NPC IDs.
    /// </summary>
    /// <returns>Collection of all NPC identifiers.</returns>
    IEnumerable<GameNpcId> GetAllNpcIds();

    /// <summary>
    ///     Gets all NPC IDs in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "townfolk", "trainer").</param>
    /// <returns>Collection of matching NPC identifiers.</returns>
    IEnumerable<GameNpcId> GetNpcIdsByCategory(string category);

    /// <summary>
    ///     Checks if an NPC definition exists.
    /// </summary>
    /// <param name="npcId">The NPC identifier to check.</param>
    /// <returns>True if the NPC exists.</returns>
    bool NpcExists(GameNpcId npcId);

    #endregion

    #region Trainer Registry

    /// <summary>
    ///     Gets a trainer definition by its ID.
    /// </summary>
    /// <param name="trainerId">The trainer identifier.</param>
    /// <returns>The trainer definition, or null if not found.</returns>
    TrainerDefinition? GetTrainerDefinition(GameTrainerId trainerId);

    /// <summary>
    ///     Gets all registered trainer IDs.
    /// </summary>
    /// <returns>Collection of all trainer identifiers.</returns>
    IEnumerable<GameTrainerId> GetAllTrainerIds();

    /// <summary>
    ///     Gets all trainer IDs in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "youngster", "lass").</param>
    /// <returns>Collection of matching trainer identifiers.</returns>
    IEnumerable<GameTrainerId> GetTrainerIdsByCategory(string category);

    /// <summary>
    ///     Checks if a trainer definition exists.
    /// </summary>
    /// <param name="trainerId">The trainer identifier to check.</param>
    /// <returns>True if the trainer exists.</returns>
    bool TrainerExists(GameTrainerId trainerId);

    #endregion

    #region Sprite Registry

    /// <summary>
    ///     Gets all registered sprite IDs.
    /// </summary>
    /// <returns>Collection of all sprite identifiers.</returns>
    IEnumerable<GameSpriteId> GetAllSpriteIds();

    /// <summary>
    ///     Gets all sprite IDs in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "players", "trainers").</param>
    /// <returns>Collection of matching sprite identifiers.</returns>
    IEnumerable<GameSpriteId> GetSpriteIdsByCategory(string category);

    /// <summary>
    ///     Checks if a sprite exists.
    /// </summary>
    /// <param name="spriteId">The sprite identifier to check.</param>
    /// <returns>True if the sprite exists.</returns>
    bool SpriteExists(GameSpriteId spriteId);

    /// <summary>
    ///     Gets the animation names defined for a sprite.
    /// </summary>
    /// <param name="spriteId">The sprite identifier.</param>
    /// <returns>Collection of animation names, or empty if sprite not found.</returns>
    IEnumerable<string> GetSpriteAnimationNames(GameSpriteId spriteId);

    /// <summary>
    ///     Checks if a sprite has specific animation(s).
    /// </summary>
    /// <param name="spriteId">The sprite identifier.</param>
    /// <param name="animationNames">One or more animation names to check for.</param>
    /// <returns>True if the sprite has ALL specified animations.</returns>
    bool SpriteHasAnimations(GameSpriteId spriteId, params string[] animationNames);

    /// <summary>
    ///     Checks if a sprite has the required animations for NPC movement (go_* and face_*).
    /// </summary>
    /// <param name="spriteId">The sprite identifier.</param>
    /// <returns>True if the sprite has go_south/go_north/go_east/go_west and face_south/face_north/face_east/face_west.</returns>
    bool HasNpcAnimations(GameSpriteId spriteId);

    #endregion

    #region Behavior Registry

    /// <summary>
    ///     Gets a behavior definition by its ID.
    /// </summary>
    /// <param name="behaviorId">The behavior identifier.</param>
    /// <returns>The behavior definition, or null if not found.</returns>
    BehaviorDefinition? GetBehaviorDefinition(GameBehaviorId behaviorId);

    /// <summary>
    ///     Gets all registered behavior IDs.
    /// </summary>
    /// <returns>Collection of all behavior identifiers.</returns>
    IEnumerable<GameBehaviorId> GetAllBehaviorIds();

    /// <summary>
    ///     Gets all behavior IDs in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "npc", "tile", "trainer").</param>
    /// <returns>Collection of matching behavior identifiers.</returns>
    IEnumerable<GameBehaviorId> GetBehaviorIdsByCategory(string category);

    /// <summary>
    ///     Checks if a behavior definition exists.
    /// </summary>
    /// <param name="behaviorId">The behavior identifier to check.</param>
    /// <returns>True if the behavior exists.</returns>
    bool BehaviorExists(GameBehaviorId behaviorId);

    #endregion

    #region Map Registry

    /// <summary>
    ///     Gets all registered map IDs.
    /// </summary>
    /// <returns>Collection of all map identifiers.</returns>
    IEnumerable<GameMapId> GetAllMapIds();

    /// <summary>
    ///     Gets all map IDs in a specific category/region.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "hoenn", "kanto").</param>
    /// <returns>Collection of matching map identifiers.</returns>
    IEnumerable<GameMapId> GetMapIdsByCategory(string category);

    /// <summary>
    ///     Checks if a map exists.
    /// </summary>
    /// <param name="mapId">The map identifier to check.</param>
    /// <returns>True if the map exists.</returns>
    bool MapExists(GameMapId mapId);

    #endregion

    #region Template Registry

    /// <summary>
    ///     Gets all registered entity template IDs.
    /// </summary>
    /// <returns>Collection of all template identifiers.</returns>
    IEnumerable<string> GetAllTemplateIds();

    /// <summary>
    ///     Gets all template IDs with a specific tag.
    /// </summary>
    /// <param name="tag">The tag to filter by (e.g., "npc", "trigger", "item").</param>
    /// <returns>Collection of matching template identifiers.</returns>
    IEnumerable<string> GetTemplateIdsByTag(string tag);

    /// <summary>
    ///     Checks if an entity template exists.
    /// </summary>
    /// <param name="templateId">The template identifier to check.</param>
    /// <returns>True if the template exists.</returns>
    bool TemplateExists(string templateId);

    #endregion

    #region Flag Registry

    /// <summary>
    ///     Gets all flag IDs that have been defined in the game data.
    ///     Note: This returns predefined flags, not runtime-set flags.
    /// </summary>
    /// <returns>Collection of all predefined flag identifiers.</returns>
    IEnumerable<GameFlagId> GetAllFlagIds();

    /// <summary>
    ///     Gets all flag IDs in a specific category.
    /// </summary>
    /// <param name="category">The category to filter by (e.g., "story", "hide", "event").</param>
    /// <returns>Collection of matching flag identifiers.</returns>
    IEnumerable<GameFlagId> GetFlagIdsByCategory(string category);

    #endregion
}
