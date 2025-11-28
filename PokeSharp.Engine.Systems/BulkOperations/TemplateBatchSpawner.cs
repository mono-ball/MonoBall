using System.Numerics;
using Arch.Core;
using PokeSharp.Engine.Systems.Factories;

namespace PokeSharp.Engine.Systems.BulkOperations;

/// <summary>
///     Specialized spawner for creating multiple entities from templates efficiently.
///     Integrates with EntityFactoryService to leverage template system while
///     optimizing for bulk creation scenarios.
/// </summary>
/// <remarks>
///     <para>
///         This spawner is optimized for common bulk spawning patterns:
///         - Wave-based enemy spawning
///         - Grid/pattern placement (tilemap objects, particles)
///         - Circle/area spawning (explosions, loot drops)
///     </para>
///     <example>
///         <code>
/// var spawner = new TemplateBatchSpawner(factoryService, world);
///
/// // Spawn enemy wave
/// var wave = spawner.SpawnWave("enemy/goblin", 10, new WaveConfiguration
/// {
///     SpawnPosition = new Vector2(800, 300),
///     SpawnInterval = 0.5f,
///     PositionFactory = i => new Vector2(800 + i * 20, 300)
/// });
///
/// // Spawn particle circle
/// var particles = spawner.SpawnCircle("vfx/sparkle", 20, playerPos, radius: 50f);
/// </code>
///     </example>
/// </remarks>
public sealed class TemplateBatchSpawner
{
    private readonly IEntityFactoryService _factory;
    private readonly World _world;

    /// <summary>
    ///     Creates a new template batch spawner.
    /// </summary>
    /// <param name="factory">Entity factory service for template resolution</param>
    /// <param name="world">World to spawn entities in</param>
    public TemplateBatchSpawner(IEntityFactoryService factory, World world)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    ///     Spawn multiple entities from the same template.
    ///     More efficient than calling SpawnFromTemplate multiple times.
    /// </summary>
    /// <param name="templateId">Template ID to spawn from</param>
    /// <param name="count">Number of entities to spawn</param>
    /// <param name="configure">Optional per-entity configuration (receives entity builder and index)</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Spawn 50 coins with different positions
    /// var coins = spawner.SpawnBatch("items/coin", 50, (builder, i) =>
    /// {
    ///     builder.OverrideComponent(new Position(
    ///         Random.Shared.Next(0, 800),
    ///         Random.Shared.Next(0, 600)
    ///     ));
    /// });
    /// </code>
    /// </example>
    public Entity[] SpawnBatch(
        string templateId,
        int count,
        Action<EntityBuilder, int>? configure = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            if (configure != null)
            {
                entities[i] = _factory.SpawnFromTemplate(
                    templateId,
                    _world,
                    builder =>
                    {
                        configure(builder, i);
                    }
                );
            }
            else
            {
                entities[i] = _factory.SpawnFromTemplate(templateId, _world);
            }
        }

        return entities;
    }

    /// <summary>
    ///     Spawn entities in a grid pattern.
    ///     Perfect for tilemap objects, structured layouts, or organized spawning.
    /// </summary>
    /// <param name="templateId">Template ID to spawn</param>
    /// <param name="rows">Number of rows</param>
    /// <param name="cols">Number of columns</param>
    /// <param name="spacing">Space between entities</param>
    /// <param name="startPosition">Top-left starting position</param>
    /// <returns>Array of spawned entities (row-major order)</returns>
    /// <example>
    ///     <code>
    /// // Spawn 5x5 grid of obstacles
    /// var obstacles = spawner.SpawnGrid(
    ///     "obstacles/crate",
    ///     rows: 5,
    ///     cols: 5,
    ///     spacing: 64,
    ///     startPosition: new Vector2(100, 100)
    /// );
    ///
    /// // Access specific position: obstacles[row * cols + col]
    /// </code>
    /// </example>
    public Entity[] SpawnGrid(
        string templateId,
        int rows,
        int cols,
        int spacing,
        Vector2 startPosition
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentOutOfRangeException.ThrowIfNegative(spacing);

        int totalCount = rows * cols;
        var entities = new Entity[totalCount];

        for (int row = 0; row < rows; row++)
        for (int col = 0; col < cols; col++)
        {
            int index = (row * cols) + col;
            var position = new Vector2(
                startPosition.X + (col * spacing),
                startPosition.Y + (row * spacing)
            );

            entities[index] = _factory.SpawnFromTemplate(
                templateId,
                _world,
                builder =>
                {
                    // Assuming Position component exists - adjust based on your component structure
                    builder.WithProperty("GridPosition", new { Row = row, Col = col });
                    builder.WithProperty("SpawnPosition", position);
                }
            );
        }

        return entities;
    }

    /// <summary>
    ///     Spawn entities in a circle pattern around a center point.
    ///     Useful for explosions, radial effects, or surrounding spawns.
    /// </summary>
    /// <param name="templateId">Template ID to spawn</param>
    /// <param name="count">Number of entities to spawn around circle</param>
    /// <param name="center">Center position of the circle</param>
    /// <param name="radius">Radius of the circle</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Spawn 8 projectiles in all directions
    /// var projectiles = spawner.SpawnCircle(
    ///     "projectile/bullet",
    ///     count: 8,
    ///     center: playerPos,
    ///     radius: 20f
    /// );
    ///
    /// // Spawn particle explosion
    /// var particles = spawner.SpawnCircle(
    ///     "vfx/explosion_particle",
    ///     count: 24,
    ///     center: explosionPos,
    ///     radius: 100f
    /// );
    /// </code>
    /// </example>
    public Entity[] SpawnCircle(string templateId, int count, Vector2 center, float radius)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        var entities = new Entity[count];
        float angleStep = MathF.PI * 2f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep;
            var position = new Vector2(
                center.X + (MathF.Cos(angle) * radius),
                center.Y + (MathF.Sin(angle) * radius)
            );

            entities[i] = _factory.SpawnFromTemplate(
                templateId,
                _world,
                builder =>
                {
                    builder.WithProperty("CircleAngle", angle);
                    builder.WithProperty("CircleIndex", i);
                    builder.WithProperty("SpawnPosition", position);
                }
            );
        }

        return entities;
    }

    /// <summary>
    ///     Spawn a wave of entities with staggered timing configuration.
    ///     Perfect for enemy waves, timed spawning, or cinematic sequences.
    /// </summary>
    /// <param name="templateId">Template ID to spawn</param>
    /// <param name="count">Number of entities in the wave</param>
    /// <param name="config">Wave configuration (timing and positioning)</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Spawn enemy wave that enters from right side
    /// var wave = spawner.SpawnWave("enemy/skeleton", 15, new WaveConfiguration
    /// {
    ///     SpawnPosition = new Vector2(900, 300),
    ///     SpawnInterval = 0.8f,
    ///     PositionFactory = i => new Vector2(
    ///         900,
    ///         200 + i * 30  // Spread vertically
    ///     )
    /// });
    ///
    /// // Each entity has SpawnDelay property for timed activation
    /// </code>
    /// </example>
    public Entity[] SpawnWave(string templateId, int count, WaveConfiguration config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(config);

        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            float spawnDelay = i * config.SpawnInterval;
            Vector2 position = config.PositionFactory?.Invoke(i) ?? config.SpawnPosition;

            entities[i] = _factory.SpawnFromTemplate(
                templateId,
                _world,
                builder =>
                {
                    builder.WithProperty("WaveIndex", i);
                    builder.WithProperty("SpawnDelay", spawnDelay);
                    builder.WithProperty("SpawnPosition", position);
                }
            );
        }

        return entities;
    }

    /// <summary>
    ///     Spawn entities in a line from start to end position.
    /// </summary>
    /// <param name="templateId">Template ID to spawn</param>
    /// <param name="count">Number of entities along the line</param>
    /// <param name="startPos">Start position</param>
    /// <param name="endPos">End position</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Create a wall of obstacles
    /// var wall = spawner.SpawnLine(
    ///     "obstacles/pillar",
    ///     count: 10,
    ///     startPos: new Vector2(100, 200),
    ///     endPos: new Vector2(700, 200)
    /// );
    /// </code>
    /// </example>
    public Entity[] SpawnLine(string templateId, int count, Vector2 startPos, Vector2 endPos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var entities = new Entity[count];
        var direction = Vector2.Normalize(endPos - startPos);
        float totalDistance = Vector2.Distance(startPos, endPos);
        float stepSize = totalDistance / (count - 1);

        for (int i = 0; i < count; i++)
        {
            Vector2 position = startPos + (direction * (stepSize * i));

            entities[i] = _factory.SpawnFromTemplate(
                templateId,
                _world,
                builder =>
                {
                    builder.WithProperty("LineIndex", i);
                    builder.WithProperty("SpawnPosition", position);
                }
            );
        }

        return entities;
    }

    /// <summary>
    ///     Spawn entities randomly within a rectangular area.
    /// </summary>
    /// <param name="templateId">Template ID to spawn</param>
    /// <param name="count">Number of entities to spawn</param>
    /// <param name="minBounds">Minimum X,Y bounds</param>
    /// <param name="maxBounds">Maximum X,Y bounds</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Scatter collectibles across play area
    /// var collectibles = spawner.SpawnRandom(
    ///     "items/gem",
    ///     count: 30,
    ///     minBounds: new Vector2(0, 0),
    ///     maxBounds: new Vector2(800, 600)
    /// );
    /// </code>
    /// </example>
    public Entity[] SpawnRandom(string templateId, int count, Vector2 minBounds, Vector2 maxBounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            var position = new Vector2(
                (Random.Shared.NextSingle() * (maxBounds.X - minBounds.X)) + minBounds.X,
                (Random.Shared.NextSingle() * (maxBounds.Y - minBounds.Y)) + minBounds.Y
            );

            entities[i] = _factory.SpawnFromTemplate(
                templateId,
                _world,
                builder =>
                {
                    builder.WithProperty("SpawnPosition", position);
                }
            );
        }

        return entities;
    }
}

/// <summary>
///     Configuration for wave-based spawning with timing and positioning.
/// </summary>
public sealed class WaveConfiguration
{
    /// <summary>Default spawn position for all entities</summary>
    public Vector2 SpawnPosition { get; set; }

    /// <summary>Time in seconds between each spawn</summary>
    public float SpawnInterval { get; set; }

    /// <summary>
    ///     Optional factory to compute position per entity.
    ///     Receives entity index, returns position.
    ///     If null, SpawnPosition is used for all entities.
    /// </summary>
    public Func<int, Vector2>? PositionFactory { get; set; }
}
