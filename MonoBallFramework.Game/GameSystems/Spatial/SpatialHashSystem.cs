using System.Runtime.InteropServices;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Common.Utilities;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameSystems.Spatial;

/// <summary>
///     System that builds and maintains specialized spatial hashes with pre-computed component data.
///     Eliminates ECS calls during spatial queries by extracting component data at index time.
/// </summary>
/// <remarks>
///     <para>
///         <b>Architecture:</b>
///         Instead of storing raw Entity references that require Has&lt;T&gt;() and Get&lt;T&gt;() calls,
///         this system maintains multiple specialized hashes containing pre-computed data:
///     </para>
///     <list type="bullet">
///         <item><b>Collision Hash:</b> Entity + Elevation + IsSolid + HasTileBehavior</item>
///         <item><b>Tile Render Hash:</b> All data needed to render a tile</item>
///         <item><b>Dynamic Entity Hash:</b> Entity + collision state for NPCs/player</item>
///         <item><b>Entity Hash:</b> Raw Entity references for scripting/advanced use</item>
///     </list>
///     <para>
///         <b>Performance Benefits:</b>
///     </para>
///     <list type="bullet">
///         <item>Zero ECS calls during collision checks (was ~4 per entity)</item>
///         <item>Zero ECS calls during tile rendering (was ~3-4 per tile)</item>
///         <item>ReadOnlySpan returns enable zero-allocation iteration</item>
///     </list>
/// </remarks>
public class SpatialHashSystem : SystemBase, IUpdateSystem, ISpatialQuery
{
    // === Specialized hashes with pre-computed data ===
    private readonly SpatialHash<CollisionEntry> _staticCollisionHash = new();
    private readonly SpatialHash<CollisionEntry> _dynamicCollisionHash = new();
    private readonly SpatialHash<TileRenderEntry> _tileRenderHash = new();
    private readonly SpatialHash<DynamicEntry> _dynamicEntityHash = new();

    // === Entity-only hashes for generic queries ===
    private readonly SpatialHash<Entity> _staticEntityHash = new();
    private readonly SpatialHash<Entity> _dynamicEntityOnlyHash = new();

    // === Query result buffers ===
    private readonly List<Entity> _entityBuffer = new(128);
    private readonly List<CollisionEntry> _collisionBuffer = new(32);

    private readonly ILogger<SpatialHashSystem>? _logger;
    private bool _staticTilesIndexed;

    public SpatialHashSystem(ILogger<SpatialHashSystem>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public int UpdatePriority => SystemPriority.SpatialHash;

    /// <inheritdoc />
    public override int Priority => SystemPriority.SpatialHash;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Index static tiles once (or when invalidated)
        if (!_staticTilesIndexed)
        {
            IndexStaticTiles(world);
            _staticTilesIndexed = true;
        }

        // Re-index dynamic entities every frame
        IndexDynamicEntities(world);
    }

    /// <summary>
    ///     Indexes all static tiles with pre-computed collision and render data.
    ///     Runs once at startup and when InvalidateStaticTiles() is called.
    /// </summary>
    private void IndexStaticTiles(World world)
    {
        _staticCollisionHash.Clear();
        _tileRenderHash.Clear();
        _staticEntityHash.Clear();

        int tileCount = 0;

        // Single query gets ALL tile data at once
        // This is where we extract component data - at query time we have everything
        var query = new QueryDescription()
            .WithAll<TilePosition, TileSprite, Elevation>();

        world.Query(
            in query,
            (Entity entity, ref TilePosition pos, ref TileSprite sprite, ref Elevation elevation) =>
            {
                if (pos.MapId == null)
                {
                    return;
                }

                GameMapId mapId = pos.MapId;

                // === Entity hash (for generic queries) ===
                _staticEntityHash.Add(mapId, pos.X, pos.Y, entity);

                // === Collision hash ===
                // Extract collision data NOW while we have access to components
                bool hasCollision = entity.Has<Collision>();
                bool isSolid = hasCollision && entity.Get<Collision>().IsSolid;
                bool hasBehavior = entity.Has<TileBehavior>();

                _staticCollisionHash.Add(
                    mapId,
                    pos.X,
                    pos.Y,
                    new CollisionEntry(entity, elevation.Value, isSolid, hasBehavior)
                );

                // === Render hash ===
                // Extract ALL render data NOW
                float offsetX = 0;
                float offsetY = 0;
                if (entity.TryGet<LayerOffset>(out LayerOffset offset))
                {
                    offsetX = offset.X;
                    offsetY = offset.Y;
                }

                // Check if tile is animated - animated tiles need SourceRect lookup at render time
                bool isAnimated = entity.Has<AnimatedTile>();

                _tileRenderHash.Add(
                    mapId,
                    pos.X,
                    pos.Y,
                    new TileRenderEntry(
                        sprite.SourceRect,
                        sprite.TilesetId,
                        offsetX,
                        offsetY,
                        entity,
                        elevation.Value,
                        sprite.FlipHorizontally,
                        sprite.FlipVertically,
                        isAnimated
                    )
                );

                tileCount++;
            }
        );

        _logger?.LogSpatialHashIndexed(tileCount);
    }

    /// <summary>
    ///     Indexes dynamic entities (NPCs, player) with pre-computed data.
    ///     Runs every frame since these entities can move.
    /// </summary>
    private void IndexDynamicEntities(World world)
    {
        _dynamicCollisionHash.Clear();
        _dynamicEntityHash.Clear();
        _dynamicEntityOnlyHash.Clear();

        var query = new QueryDescription().WithAll<Position>();

        world.Query(
            in query,
            (Entity entity, ref Position pos) =>
            {
                if (pos.MapId == null)
                {
                    return;
                }

                GameMapId mapId = pos.MapId;

                // === Entity hash ===
                _dynamicEntityOnlyHash.Add(mapId, pos.X, pos.Y, entity);

                // === Pre-compute collision data ===
                byte elevation = entity.TryGet<Elevation>(out Elevation elev)
                    ? elev.Value
                    : Elevation.Default;

                bool hasCollision = entity.Has<Collision>();
                bool isSolid = hasCollision && entity.Get<Collision>().IsSolid;

                _dynamicCollisionHash.Add(
                    mapId,
                    pos.X,
                    pos.Y,
                    new CollisionEntry(entity, elevation, isSolid, false)
                );

                _dynamicEntityHash.Add(
                    mapId,
                    pos.X,
                    pos.Y,
                    new DynamicEntry(entity, elevation, hasCollision, isSolid)
                );
            }
        );
    }

    // ============================================================================
    // ISpatialQuery Implementation - COLLISION QUERIES
    // ============================================================================

    /// <inheritdoc />
    public ReadOnlySpan<CollisionEntry> GetCollisionEntriesAt(GameMapId mapId, int x, int y)
    {
        ReadOnlySpan<CollisionEntry> staticEntries = _staticCollisionHash.GetAt(mapId, x, y);
        ReadOnlySpan<CollisionEntry> dynamicEntries = _dynamicCollisionHash.GetAt(mapId, x, y);

        // Fast path: if only one source has data, return directly (avoids buffer copy)
        // This is the common case (~95% of tiles have no dynamic entities)
        if (dynamicEntries.IsEmpty)
        {
            return staticEntries;
        }

        if (staticEntries.IsEmpty)
        {
            return dynamicEntries;
        }

        // Slow path: both have data, need to combine into buffer
        _collisionBuffer.Clear();

        foreach (ref readonly CollisionEntry entry in staticEntries)
        {
            _collisionBuffer.Add(entry);
        }

        foreach (ref readonly CollisionEntry entry in dynamicEntries)
        {
            _collisionBuffer.Add(entry);
        }

        return CollectionsMarshal.AsSpan(_collisionBuffer);
    }

    /// <inheritdoc />
    public ReadOnlySpan<CollisionEntry> GetStaticCollisionEntriesAt(GameMapId mapId, int x, int y)
    {
        return _staticCollisionHash.GetAt(mapId, x, y);
    }

    /// <inheritdoc />
    public ReadOnlySpan<CollisionEntry> GetDynamicCollisionEntriesAt(GameMapId mapId, int x, int y)
    {
        return _dynamicCollisionHash.GetAt(mapId, x, y);
    }

    // ============================================================================
    // ISpatialQuery Implementation - RENDER QUERIES
    // ============================================================================

    /// <inheritdoc />
    public ReadOnlySpan<TileRenderEntry> GetTileRenderEntriesAt(GameMapId mapId, int x, int y)
    {
        return _tileRenderHash.GetAt(mapId, x, y);
    }

    /// <inheritdoc />
    public void GetTileRenderEntries(GameMapId mapId, Rectangle bounds, List<TileRenderEntry> results)
    {
        _tileRenderHash.GetInBounds(mapId, bounds, results);
    }

    // ============================================================================
    // ISpatialQuery Implementation - DYNAMIC ENTITY QUERIES
    // ============================================================================

    /// <inheritdoc />
    public ReadOnlySpan<DynamicEntry> GetDynamicEntriesAt(GameMapId mapId, int x, int y)
    {
        return _dynamicEntityHash.GetAt(mapId, x, y);
    }

    // ============================================================================
    // ISpatialQuery Implementation - GENERIC ENTITY QUERIES
    // ============================================================================

    /// <inheritdoc />
    public ReadOnlySpan<Entity> GetEntitiesAt(GameMapId mapId, int x, int y)
    {
        ReadOnlySpan<Entity> staticEntities = _staticEntityHash.GetAt(mapId, x, y);
        ReadOnlySpan<Entity> dynamicEntities = _dynamicEntityOnlyHash.GetAt(mapId, x, y);

        // Fast path: if only one source has data, return directly (avoids buffer copy)
        // This is the common case (~95% of tiles have no dynamic entities)
        if (dynamicEntities.IsEmpty)
        {
            return staticEntities;
        }

        if (staticEntities.IsEmpty)
        {
            return dynamicEntities;
        }

        // Slow path: both have data, need to combine into buffer
        _entityBuffer.Clear();

        foreach (Entity entity in staticEntities)
        {
            _entityBuffer.Add(entity);
        }

        foreach (Entity entity in dynamicEntities)
        {
            _entityBuffer.Add(entity);
        }

        return CollectionsMarshal.AsSpan(_entityBuffer);
    }

    /// <inheritdoc />
    public void GetEntitiesInBounds(GameMapId mapId, Rectangle bounds, List<Entity> results)
    {
        _staticEntityHash.GetInBounds(mapId, bounds, results);
        _dynamicEntityOnlyHash.GetInBounds(mapId, bounds, results);
    }

    /// <inheritdoc />
    public IReadOnlyList<Entity> GetStaticEntitiesInBounds(GameMapId mapId, Rectangle bounds)
    {
        _entityBuffer.Clear();
        _staticEntityHash.GetInBounds(mapId, bounds, _entityBuffer);
        return _entityBuffer;
    }

    // ============================================================================
    // PUBLIC API - Map Management
    // ============================================================================

    /// <summary>
    ///     Forces a full rebuild of the static spatial hash on the next update.
    ///     Call this when maps are loaded/unloaded or tiles are added/removed.
    /// </summary>
    public void InvalidateStaticTiles()
    {
        _staticTilesIndexed = false;
        _logger?.LogDebug("Static tiles invalidated, will rebuild spatial hash on next update");
    }

    /// <summary>
    ///     Adds tiles from a newly loaded map to the spatial hash without full rebuild.
    ///     More efficient than InvalidateStaticTiles() for incremental loading.
    /// </summary>
    /// <param name="mapId">The map identifier for the loaded map.</param>
    /// <param name="tiles">List of tile entities to add.</param>
    public void AddMapTiles(GameMapId mapId, IReadOnlyList<Entity> tiles)
    {
        int tilesAdded = 0;

        foreach (Entity entity in tiles)
        {
            if (World == null || !World.IsAlive(entity))
            {
                continue;
            }

            if (!World.TryGet(entity, out TilePosition pos) || pos.MapId == null)
            {
                continue;
            }

            if (!World.TryGet(entity, out TileSprite sprite))
            {
                continue;
            }

            if (!World.TryGet(entity, out Elevation elevation))
            {
                continue;
            }

            // Entity hash
            _staticEntityHash.Add(mapId, pos.X, pos.Y, entity);

            // Collision hash
            bool hasCollision = entity.Has<Collision>();
            bool isSolid = hasCollision && entity.Get<Collision>().IsSolid;
            bool hasBehavior = entity.Has<TileBehavior>();

            _staticCollisionHash.Add(
                mapId,
                pos.X,
                pos.Y,
                new CollisionEntry(entity, elevation.Value, isSolid, hasBehavior)
            );

            // Render hash
            float offsetX = 0;
            float offsetY = 0;
            if (entity.TryGet<LayerOffset>(out LayerOffset offset))
            {
                offsetX = offset.X;
                offsetY = offset.Y;
            }

            // Check if tile is animated - animated tiles need SourceRect lookup at render time
            bool isAnimated = entity.Has<AnimatedTile>();

            _tileRenderHash.Add(
                mapId,
                pos.X,
                pos.Y,
                new TileRenderEntry(
                    sprite.SourceRect,
                    sprite.TilesetId,
                    offsetX,
                    offsetY,
                    entity,
                    elevation.Value,
                    sprite.FlipHorizontally,
                    sprite.FlipVertically,
                    isAnimated
                )
            );

            tilesAdded++;
        }

        _logger?.LogDebug(
            "Incrementally added {TilesAdded} tiles for map {MapId} to spatial hash",
            tilesAdded,
            mapId
        );
    }

    /// <summary>
    ///     Removes all tiles belonging to a specific map from the spatial hash.
    /// </summary>
    /// <param name="mapId">The map identifier whose tiles should be removed.</param>
    public void RemoveMapTiles(GameMapId mapId)
    {
        bool removed = _staticEntityHash.RemoveMap(mapId);
        _staticCollisionHash.RemoveMap(mapId);
        _tileRenderHash.RemoveMap(mapId);

        if (removed)
        {
            _logger?.LogDebug("Removed all tiles for map {MapId} from spatial hash", mapId);
        }
    }

    // ============================================================================
    // DIAGNOSTICS
    // ============================================================================

    /// <summary>
    ///     Gets diagnostic information about the spatial hash.
    /// </summary>
    public (int staticTiles, int dynamicEntities, int occupiedPositions) GetDiagnostics()
    {
        int staticCount = _staticEntityHash.GetEntryCount();
        int dynamicCount = _dynamicEntityOnlyHash.GetEntryCount();
        int staticPositions = _staticEntityHash.GetOccupiedPositionCount();
        int dynamicPositions = _dynamicEntityOnlyHash.GetOccupiedPositionCount();

        return (staticCount, dynamicCount, staticPositions + dynamicPositions);
    }
}
