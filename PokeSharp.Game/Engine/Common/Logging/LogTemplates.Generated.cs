using System;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Engine.Common.Logging;

/// <summary>
///     Source-generated logging templates using LoggerMessage attribute.
///     Provides zero-allocation, high-performance logging with Spectre.Console formatting.
///     Event ID ranges: 1000-1999 (Rendering), 2000-2999 (Systems), 3000-3999 (Map Loading),
///     4000-4999 (Data Loading), 5000-5999 (Scripting)
/// </summary>
public static partial class LogTemplates
{
    // ═══════════════════════════════════════════════════════════════
    // RENDERING TEMPLATES (Event IDs: 1000-1999)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Sprite animation updated for entity</summary>
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[mediumorchid1]R[/] [cyan]{EntityType}[/] [dim]#{EntityId}[/] | anim: [yellow]{AnimationName}[/] | frame: {CurrentFrame}/{TotalFrames}")]
    public static partial void LogSpriteAnimationUpdated(
        this ILogger logger,
        string entityType,
        int entityId,
        string animationName,
        int currentFrame,
        int totalFrames);

    /// <summary>Sprite texture loaded successfully</summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "[mediumorchid1]R[/] [green]✓[/] Sprite loaded | [cyan]{SpriteId}[/] | [yellow]{TextureId}[/] | {Width}x{Height}px")]
    public static partial void LogSpriteTextureLoaded(
        this ILogger logger,
        string spriteId,
        string textureId,
        int width,
        int height);

    /// <summary>Sprite manifest loaded with animation count</summary>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "[mediumorchid1]R[/] Manifest loaded | [cyan]{SpriteName}[/] | animations: [yellow]{AnimationCount}[/] | frames: {FrameCount}")]
    public static partial void LogSpriteManifestLoaded(
        this ILogger logger,
        string spriteName,
        int animationCount,
        int frameCount);

    /// <summary>Sprite not found in loader cache</summary>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "[mediumorchid1]R[/] [orange1]⚠[/] Sprite not found | [cyan]{SpriteName}[/] | category: {Category}")]
    public static partial void LogSpriteNotFound(
        this ILogger logger,
        string spriteName,
        string category);

    /// <summary>Render pass completed with draw count</summary>
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "[mediumorchid1]R[/] Render pass | tiles: [yellow]{TileCount}[/] | sprites: [magenta]{SpriteCount}[/] | calls: {DrawCalls}")]
    public static partial void LogRenderPassCompleted(
        this ILogger logger,
        int tileCount,
        int spriteCount,
        int drawCalls);

    /// <summary>Elevation layer rendering statistics</summary>
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "[mediumorchid1]R[/] Elevation [cyan]{Elevation}[/] | entities: [yellow]{EntityCount}[/] | {TimeMs:F2}ms")]
    public static partial void LogElevationLayerRendered(
        this ILogger logger,
        int elevation,
        int entityCount,
        double timeMs);

    // ═══════════════════════════════════════════════════════════════
    // SYSTEMS TEMPLATES (Event IDs: 2000-2999)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>System registered with priority</summary>
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "[orange3]SYS[/] [green]✓[/] System registered | [cyan]{SystemName}[/] | priority: [yellow]{Priority}[/]")]
    public static partial void LogSystemRegistered(
        this ILogger logger,
        string systemName,
        int priority);

    /// <summary>System lifecycle event (enable/disable)</summary>
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "[orange3]SYS[/] {SystemName} | [yellow]{Action}[/]")]
    public static partial void LogSystemLifecycle(
        this ILogger logger,
        string systemName,
        string action);

    /// <summary>System update completed with timing</summary>
    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "[orange3]SYS[/] [cyan]{SystemName}[/] | [yellow]{TimeMs:F2}ms[/]")]
    public static partial void LogSystemUpdateCompleted(
        this ILogger logger,
        string systemName,
        double timeMs);

    /// <summary>Spatial hash rebuilt with entity count</summary>
    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "[orange3]SYS[/] Spatial hash rebuilt | entities: [yellow]{EntityCount}[/] | cells: {CellCount} | {TimeMs:F2}ms")]
    public static partial void LogSpatialHashRebuilt(
        this ILogger logger,
        int entityCount,
        int cellCount,
        double timeMs);

    /// <summary>Component pool created with capacity</summary>
    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Debug,
        Message = "[orange3]SYS[/] Pool created | [cyan]{ComponentType}[/] | capacity: [yellow]{Capacity}[/]")]
    public static partial void LogComponentPoolCreated(
        this ILogger logger,
        string componentType,
        int capacity);

    /// <summary>Component pooling statistics</summary>
    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Debug,
        Message = "[orange3]SYS[/] Pool stats | [cyan]{ComponentType}[/] | active: [yellow]{Active}[/] | available: [green]{Available}[/] | hit rate: {HitRate:F1}%")]
    public static partial void LogComponentPoolStats(
        this ILogger logger,
        string componentType,
        int active,
        int available,
        double hitRate);

    /// <summary>System dependency not found</summary>
    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Warning,
        Message = "[orange3]SYS[/] [orange1]⚠[/] System dependency missing | [cyan]{SystemName}[/] needs [yellow]{DependencyName}[/]")]
    public static partial void LogSystemDependencyNotFound(
        this ILogger logger,
        string systemName,
        string dependencyName);

    /// <summary>Pathfinding computation completed</summary>
    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Debug,
        Message = "[orange3]SYS[/] Pathfinding | from [cyan]{StartX},{StartY}[/] to [magenta]{EndX},{EndY}[/] | nodes: [yellow]{NodesExplored}[/] | {TimeMs:F2}ms")]
    public static partial void LogPathfindingCompleted(
        this ILogger logger,
        int startX,
        int startY,
        int endX,
        int endY,
        int nodesExplored,
        double timeMs);

    // ═══════════════════════════════════════════════════════════════
    // MAP LOADING TEMPLATES (Event IDs: 3000-3999)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Map loading started</summary>
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "[springgreen1]M[/] Loading map | [yellow]{MapId}[/] ([cyan]{DisplayName}[/])")]
    public static partial void LogMapLoadingStarted(
        this ILogger logger,
        string mapId,
        string displayName);

    /// <summary>Map definition loaded from database</summary>
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Map definition | [cyan]{MapId}[/] | path: [yellow]{TiledDataPath}[/]")]
    public static partial void LogMapDefinitionLoaded(
        this ILogger logger,
        string mapId,
        string tiledDataPath);

    /// <summary>Tileset loaded successfully</summary>
    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "[springgreen1]M[/] [green]✓[/] Tileset loaded | [cyan]{TilesetId}[/] | {TileWidth}x{TileHeight}px | tiles: [yellow]{TileCount}[/]")]
    public static partial void LogTilesetLoaded(
        this ILogger logger,
        string tilesetId,
        int tileWidth,
        int tileHeight,
        int tileCount);

    /// <summary>External tileset file loaded</summary>
    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] External tileset | [cyan]{Name}[/] | animations: [yellow]{AnimationCount}[/] | from: {Source}")]
    public static partial void LogExternalTilesetLoaded(
        this ILogger logger,
        string name,
        int animationCount,
        string source);

    /// <summary>Tile layer parsed with dimensions</summary>
    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Layer parsed | [cyan]{LayerName}[/] | {Width}x{Height} | elevation: [yellow]{Elevation}[/]")]
    public static partial void LogTileLayerParsed(
        this ILogger logger,
        string layerName,
        int width,
        int height,
        int elevation);

    /// <summary>Animated tile entities created</summary>
    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Animated tiles | count: [yellow]{Count}[/] | tileset: [cyan]{TilesetName}[/]")]
    public static partial void LogAnimatedTilesCreated(
        this ILogger logger,
        int count,
        string tilesetName);

    /// <summary>Image layer created</summary>
    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Image layer | [cyan]{LayerName}[/] | texture: [yellow]{TextureId}[/] | depth: {Depth:F2}")]
    public static partial void LogImageLayerCreated(
        this ILogger logger,
        string layerName,
        string textureId,
        float depth);

    /// <summary>Map object spawned from template</summary>
    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Object spawned | [cyan]{ObjectName}[/] | template: [yellow]{TemplateId}[/] | at ({X}, {Y})")]
    public static partial void LogMapObjectSpawned(
        this ILogger logger,
        string objectName,
        string templateId,
        int x,
        int y);

    /// <summary>NPC definition applied to entity</summary>
    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Information,
        Message = "[springgreen1]M[/] [green]✓[/] NPC definition | [cyan]{NpcId}[/] ([yellow]{DisplayName}[/]) | behavior: {BehaviorScript}")]
    public static partial void LogNpcDefinitionApplied(
        this ILogger logger,
        string npcId,
        string displayName,
        string behaviorScript);

    /// <summary>Sprite collection completed for lazy loading</summary>
    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Information,
        Message = "[springgreen1]M[/] [green]✓[/] Sprites collected | count: [yellow]{Count}[/] | for map: [cyan]{MapId}[/]")]
    public static partial void LogSpriteCollectionCompleted(
        this ILogger logger,
        int count,
        string mapId);

    /// <summary>Map texture tracking initialized</summary>
    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Debug,
        Message = "[springgreen1]M[/] Textures tracked | map: [cyan]{MapId}[/] | count: [yellow]{Count}[/]")]
    public static partial void LogMapTexturesTracked(
        this ILogger logger,
        int mapId,
        int count);

    // ═══════════════════════════════════════════════════════════════
    // DATA LOADING TEMPLATES (Event IDs: 4000-4999)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Game data loader initialized</summary>
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "[aqua]A[/] [green]✓[/] Game data loader initialized | database: [cyan]{DatabasePath}[/]")]
    public static partial void LogGameDataLoaderInitialized(
        this ILogger logger,
        string databasePath);

    /// <summary>Database migration completed</summary>
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "[aqua]A[/] [green]✓[/] Database migrated | version: [yellow]{Version}[/] | {TimeMs:F0}ms")]
    public static partial void LogDatabaseMigrated(
        this ILogger logger,
        string version,
        double timeMs);

    /// <summary>Entity template loaded from JSON</summary>
    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Template loaded | [cyan]{TemplateId}[/] | components: [yellow]{ComponentCount}[/]")]
    public static partial void LogTemplateLoaded(
        this ILogger logger,
        string templateId,
        int componentCount);

    /// <summary>Component deserializer registered</summary>
    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Deserializer registered | [cyan]{ComponentType}[/]")]
    public static partial void LogDeserializerRegistered(
        this ILogger logger,
        string componentType);

    /// <summary>Asset loaded with timing</summary>
    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Asset loaded | [cyan]{AssetId}[/] | type: [yellow]{AssetType}[/] | {TimeMs:F1}ms")]
    public static partial void LogAssetLoadedWithType(
        this ILogger logger,
        string assetId,
        string assetType,
        double timeMs);

    /// <summary>Asset cache hit</summary>
    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Cache hit | [cyan]{AssetId}[/] | type: {AssetType}")]
    public static partial void LogAssetCacheHit(
        this ILogger logger,
        string assetId,
        string assetType);

    /// <summary>Asset cache miss with load required</summary>
    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Cache miss | [cyan]{AssetId}[/] | loading...")]
    public static partial void LogAssetCacheMiss(
        this ILogger logger,
        string assetId);

    /// <summary>Asset evicted from LRU cache</summary>
    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Cache evict | [cyan]{AssetId}[/] | reason: {Reason}")]
    public static partial void LogAssetEvicted(
        this ILogger logger,
        string assetId,
        string reason);

    /// <summary>Type registered in type registry</summary>
    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Debug,
        Message = "[aqua]A[/] Type registered | [cyan]{TypeName}[/] | assembly: {AssemblyName}")]
    public static partial void LogTypeRegistered(
        this ILogger logger,
        string typeName,
        string assemblyName);

    // ═══════════════════════════════════════════════════════════════
    // SCRIPTING TEMPLATES (Event IDs: 5000-5999)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Script compilation started</summary>
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]S[/] Compiling script | [yellow]{ScriptPath}[/]")]
    public static partial void LogScriptCompilationStarted(
        this ILogger logger,
        string scriptPath);

    /// <summary>Script compilation succeeded with timing</summary>
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]S[/] [green]✓[/] Script compiled | [cyan]{TypeId}[/] | version: [yellow]{Version}[/] | {TimeMs:F0}ms")]
    public static partial void LogScriptCompilationSucceeded(
        this ILogger logger,
        string typeId,
        int version,
        double timeMs);

    /// <summary>Script compilation failed with error count</summary>
    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]S[/] [red]✗[/] Script compilation failed | [cyan]{TypeId}[/] | errors: [yellow]{ErrorCount}[/] | {TimeMs:F0}ms")]
    public static partial void LogScriptCompilationFailed(
        this ILogger logger,
        string typeId,
        int errorCount,
        double timeMs);

    /// <summary>Script compilation diagnostic error</summary>
    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]S[/] [red]ERR[/] Line {Line}, Col {Column} | {Message} | [{Code}]")]
    public static partial void LogScriptDiagnosticError(
        this ILogger logger,
        int line,
        int column,
        string message,
        string code);

    /// <summary>Script hot-reload rollback performed</summary>
    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]S[/] [orange1]↶[/] Rolled back | [cyan]{TypeId}[/] | to version: [yellow]{Version}[/] | via: {Method}")]
    public static partial void LogScriptRollback(
        this ILogger logger,
        string typeId,
        int version,
        string method);

    /// <summary>Script hot-reload service started</summary>
    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]S[/] [green]✓[/] Hot-reload started | watcher: [cyan]{WatcherType}[/] | debounce: [yellow]{DebounceMs}ms[/]")]
    public static partial void LogHotReloadStarted(
        this ILogger logger,
        string watcherType,
        int debounceMs);

    /// <summary>Script change debounced</summary>
    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]S[/] Debounced | [cyan]{FileName}[/] | total: [yellow]{TotalDebounced}[/]")]
    public static partial void LogScriptChangeDebounced(
        this ILogger logger,
        string fileName,
        int totalDebounced);

    /// <summary>Script backup created before compilation</summary>
    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]S[/] Backup created | [cyan]{TypeId}[/] | version: [yellow]{Version}[/]")]
    public static partial void LogScriptBackupCreated(
        this ILogger logger,
        string typeId,
        int version);

    /// <summary>NPC behavior script attached</summary>
    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]S[/] [green]✓[/] Behavior attached | entity: [cyan]#{EntityId}[/] | script: [yellow]{ScriptTypeId}[/]")]
    public static partial void LogNpcBehaviorAttached(
        this ILogger logger,
        int entityId,
        string scriptTypeId);

    /// <summary>Script execution error</summary>
    [LoggerMessage(
        EventId = 5009,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]S[/] [red]✗[/] Script error | [cyan]{TypeId}[/] | method: [yellow]{MethodName}[/] | {ErrorMessage}")]
    public static partial void LogScriptExecutionError(
        this ILogger logger,
        string typeId,
        string methodName,
        string errorMessage);

    /// <summary>Script watcher error occurred</summary>
    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]S[/] [red]✗[/] Watcher error | critical: [yellow]{IsCritical}[/] | {Message}")]
    public static partial void LogScriptWatcherError(
        this ILogger logger,
        bool isCritical,
        string message);
}
