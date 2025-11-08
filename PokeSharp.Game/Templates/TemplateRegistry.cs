using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Core.Templates;
using PokeSharp.Input.Components;

namespace PokeSharp.Game.Templates;

/// <summary>
///     Centralized registry for all entity templates.
///     Registers templates with the cache during game initialization.
///     Located in Game project to access all component types without circular dependencies.
/// </summary>
public static class TemplateRegistry
{
    /// <summary>
    ///     Register all built-in entity templates with the cache.
    ///     Called during game initialization.
    /// </summary>
    /// <param name="cache">Template cache to register templates with</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public static void RegisterAllTemplates(TemplateCache cache, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cache);

        logger?.LogInformation("Registering entity templates...");

        // Register tile templates (base for all map tiles)
        RegisterTileTemplates(cache, logger);

        // Register player template
        RegisterPlayerTemplate(cache, logger);

        // Register NPC templates
        RegisterNpcTemplates(cache, logger);

        var stats = cache.GetStatistics();
        logger?.LogInformation("Registered {Count} entity templates", stats.TotalTemplates);
    }

    /// <summary>
    ///     Register tile entity templates using inheritance hierarchy.
    ///     Base template: tile/base
    ///     ├── tile/ground (basic walkable tile)
    ///     ├── tile/wall (solid obstacle)
    ///     ├── tile/grass (encounter zone)
    ///     ├── tile/ledge/down (directional ledges)
    ///     ├── tile/ledge/up
    ///     ├── tile/ledge/left
    ///     └── tile/ledge/right
    /// </summary>
    private static void RegisterTileTemplates(TemplateCache cache, ILogger? logger = null)
    {
        // Base tile template - minimal components (position and sprite will be overridden at spawn)
        var baseTile = new EntityTemplate
        {
            TemplateId = "tile/base",
            Name = "Base Tile",
            Tag = "tile",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterTileTemplates",
            },
        };
        baseTile.WithComponent(new TilePosition(0, 0)); // Will be overridden
        baseTile.WithComponent(new TileSprite("default", 0, TileLayer.Ground, Rectangle.Empty)); // Will be overridden
        cache.Register(baseTile);
        logger?.LogDebug("Registered base tile template: {TemplateId}", baseTile.TemplateId);

        // Ground tile - basic walkable tile (inherits from base, no additional components)
        var groundTile = new EntityTemplate
        {
            TemplateId = "tile/ground",
            Name = "Ground Tile",
            Tag = "tile",
            BaseTemplateId = "tile/base",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterTileTemplates",
            },
        };
        // No additional components - just a walkable tile
        // We need at least one component for validation, so override sprite layer
        groundTile.WithComponent(new TileSprite("default", 0, TileLayer.Ground, Rectangle.Empty));
        cache.Register(groundTile);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            groundTile.TemplateId,
            groundTile.BaseTemplateId
        );

        // Wall tile - solid collision
        var wallTile = new EntityTemplate
        {
            TemplateId = "tile/wall",
            Name = "Wall Tile",
            Tag = "tile",
            BaseTemplateId = "tile/base",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterTileTemplates",
            },
        };
        wallTile.WithComponent(new Collision(true)); // Solid
        wallTile.WithComponent(new TileSprite("default", 0, TileLayer.Object, Rectangle.Empty)); // Object layer
        cache.Register(wallTile);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            wallTile.TemplateId,
            wallTile.BaseTemplateId
        );

        // Grass tile - encounter zone
        var grassTile = new EntityTemplate
        {
            TemplateId = "tile/grass",
            Name = "Grass Tile",
            Tag = "tile",
            BaseTemplateId = "tile/base",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterTileTemplates",
            },
        };
        grassTile.WithComponent(new EncounterZone("default", 10)); // Default encounter rate
        cache.Register(grassTile);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            grassTile.TemplateId,
            grassTile.BaseTemplateId
        );

        // Ledge tiles - directional (inherit wall's collision, add ledge component)
        var ledgeDirections = new[]
        {
            ("down", Direction.Down),
            ("up", Direction.Up),
            ("left", Direction.Left),
            ("right", Direction.Right),
        };

        foreach (var (dirName, direction) in ledgeDirections)
        {
            var ledgeTile = new EntityTemplate
            {
                TemplateId = $"tile/ledge/{dirName}",
                Name = $"Ledge ({dirName})",
                Tag = "tile",
                BaseTemplateId = "tile/wall", // Inherits collision from wall
                Metadata = new EntityTemplateMetadata
                {
                    Version = "1.0.0",
                    CompiledAt = DateTime.UtcNow,
                    SourcePath = "TemplateRegistry.RegisterTileTemplates",
                },
            };
            ledgeTile.WithComponent(new TileLedge(direction)); // Add ledge behavior
            cache.Register(ledgeTile);
            logger?.LogDebug(
                "Registered template: {TemplateId} (inherits from {BaseId})",
                ledgeTile.TemplateId,
                ledgeTile.BaseTemplateId
            );
        }

        logger?.LogInformation("Registered tile template hierarchy with {Count} templates", 8);
    }

    /// <summary>
    ///     Register the player entity template.
    /// </summary>
    private static void RegisterPlayerTemplate(TemplateCache cache, ILogger? logger = null)
    {
        var template = new EntityTemplate
        {
            TemplateId = "player",
            Name = "Player Character",
            Tag = "player",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterPlayerTemplate",
            },
        };

        // Add components in the order they should be created
        template.WithComponent(new Player("PLAYER", 3000)); // Default name and starting money
        template.WithComponent(new Position(0, 0)); // Default position, overridden at spawn
        template.WithComponent(new Sprite("player-spritesheet") { Tint = Color.White, Scale = 1f });
        template.WithComponent(new GridMovement(4.0f)); // 4 tiles per second
        template.WithComponent(Direction.Down); // Face down by default
        template.WithComponent(new Animation("idle_down")); // Start idle
        template.WithComponent(new InputState());
        template.WithComponent(new Collision(true)); // Player blocks movement

        cache.Register(template);
        logger?.LogDebug("Registered template: {TemplateId}", template.TemplateId);
    }

    /// <summary>
    ///     Register NPC entity templates using inheritance hierarchy.
    ///     Base template: npc/base
    ///     ├── npc/generic (standard movable NPC)
    ///     ├── npc/stationary (non-moving NPC, no GridMovement)
    ///     ├── npc/trainer (extends generic, for battles)
    ///     │   └── npc/gym-leader (extends trainer, special NPCs)
    ///     ├── npc/shop-keeper (extends stationary, for shops)
    ///     └── npc/fast (extends generic, faster movement)
    /// </summary>
    private static void RegisterNpcTemplates(TemplateCache cache, ILogger? logger = null)
    {
        // Base NPC template - common components for all NPCs
        var baseNpc = new EntityTemplate
        {
            TemplateId = "npc/base",
            Name = "Base NPC",
            Tag = "npc",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        baseNpc.WithComponent(new Position(0, 0)); // Default position
        baseNpc.WithComponent(new Sprite("npc-spritesheet") { Tint = Color.White, Scale = 1f });
        baseNpc.WithComponent(Direction.Down); // Face down by default
        baseNpc.WithComponent(new Animation("idle_down"));
        baseNpc.WithComponent(new Collision(true)); // NPCs block movement
        cache.Register(baseNpc);
        logger?.LogDebug("Registered base template: {TemplateId}", baseNpc.TemplateId);

        // Generic NPC - inherits from base, adds movement
        var genericNpc = new EntityTemplate
        {
            TemplateId = "npc/generic",
            Name = "Generic NPC",
            Tag = "npc",
            BaseTemplateId = "npc/base",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        genericNpc.WithComponent(new GridMovement(2.0f)); // NPCs move slower than player
        cache.Register(genericNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            genericNpc.TemplateId,
            genericNpc.BaseTemplateId
        );

        // Stationary NPC - inherits from base, no movement
        var stationaryNpc = new EntityTemplate
        {
            TemplateId = "npc/stationary",
            Name = "Stationary NPC",
            Tag = "npc",
            BaseTemplateId = "npc/base",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // No GridMovement component - can't move
        // Override sprite to use different texture
        stationaryNpc.WithComponent(
            new Sprite("npc-spritesheet") { Tint = Color.White, Scale = 1f }
        );
        cache.Register(stationaryNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            stationaryNpc.TemplateId,
            stationaryNpc.BaseTemplateId
        );

        // Trainer NPC - inherits from generic, for battles
        var trainerNpc = new EntityTemplate
        {
            TemplateId = "npc/trainer",
            Name = "Trainer NPC",
            Tag = "npc",
            BaseTemplateId = "npc/generic",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // Override sprite for trainers
        trainerNpc.WithComponent(new Sprite("npc-spritesheet") { Tint = Color.White, Scale = 1f });
#warning TODO: Add Trainer component when implemented
        // trainerNpc.WithComponent(new Trainer(...));
        cache.Register(trainerNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            trainerNpc.TemplateId,
            trainerNpc.BaseTemplateId
        );

        // Gym Leader - inherits from trainer, special NPCs
        var gymLeaderNpc = new EntityTemplate
        {
            TemplateId = "npc/gym-leader",
            Name = "Gym Leader",
            Tag = "npc",
            BaseTemplateId = "npc/trainer",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // Override sprite for gym leaders
        gymLeaderNpc.WithComponent(
            new Sprite("npc-spritesheet") { Tint = Color.White, Scale = 1f }
        );
#warning TODO: Add Badge component when implemented
        // gymLeaderNpc.WithComponent(new Badge(...));
        cache.Register(gymLeaderNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            gymLeaderNpc.TemplateId,
            gymLeaderNpc.BaseTemplateId
        );

        // Shop Keeper - inherits from stationary, for shops
        var shopKeeperNpc = new EntityTemplate
        {
            TemplateId = "npc/shop-keeper",
            Name = "Shop Keeper",
            Tag = "npc",
            BaseTemplateId = "npc/stationary",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // Override sprite for shop keepers
        shopKeeperNpc.WithComponent(
            new Sprite("npc-spritesheet") { Tint = Color.White, Scale = 1f }
        );
#warning TODO: Add Shop component when implemented
        // shopKeeperNpc.WithComponent(new Shop(...));
        cache.Register(shopKeeperNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            shopKeeperNpc.TemplateId,
            shopKeeperNpc.BaseTemplateId
        );

        // Fast NPC - inherits from generic, faster movement
        var fastNpc = new EntityTemplate
        {
            TemplateId = "npc/fast",
            Name = "Fast NPC",
            Tag = "npc",
            BaseTemplateId = "npc/generic",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // Override movement speed - same as player
        fastNpc.WithComponent(new GridMovement(4.0f));
        cache.Register(fastNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            fastNpc.TemplateId,
            fastNpc.BaseTemplateId
        );

        // Patrol NPC - inherits from generic, adds patrol behavior
        var patrolNpc = new EntityTemplate
        {
            TemplateId = "npc/patrol",
            Name = "Patrol NPC",
            Tag = "npc",
            BaseTemplateId = "npc/generic",
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = "TemplateRegistry.RegisterNpcTemplates",
            },
        };
        // Add NPCComponent (will be overridden at spawn with specific NPC data)
        patrolNpc.WithComponent(new NPCComponent("patrol_npc", "GUARD"));
        // Add PathComponent (will be overridden at spawn with actual waypoints)
        patrolNpc.WithComponent(
            new PathComponent(
                new[] { new Point(10, 10), new Point(15, 10), new Point(15, 15), new Point(10, 15) }
            )
        );
        // Add BehaviorComponent with patrol behavior
        patrolNpc.WithComponent(new BehaviorComponent("patrol"));
        cache.Register(patrolNpc);
        logger?.LogDebug(
            "Registered template: {TemplateId} (inherits from {BaseId})",
            patrolNpc.TemplateId,
            patrolNpc.BaseTemplateId
        );

        logger?.LogInformation("Registered NPC template hierarchy with {Count} templates", 8);
    }

    /// <summary>
    ///     Get all template IDs registered in the system.
    ///     Useful for debugging and tools.
    /// </summary>
    public static string[] GetAllTemplateIds()
    {
        return new[]
        {
            // Tile templates
            "tile/base",
            "tile/ground",
            "tile/wall",
            "tile/grass",
            "tile/ledge/down",
            "tile/ledge/up",
            "tile/ledge/left",
            "tile/ledge/right",
            // Player template
            "player",
            // NPC templates
            "npc/base",
            "npc/generic",
            "npc/stationary",
            "npc/trainer",
            "npc/gym-leader",
            "npc/shop-keeper",
            "npc/fast",
            "npc/patrol",
        };
    }
}
