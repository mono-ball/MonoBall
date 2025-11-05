using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Core.Components;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Templates;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Templates;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Components;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;

namespace PokeSharp.Game;

/// <summary>
///     Main game class for PokeSharp.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private AnimationLibrary _animationLibrary = null!;
    private AssetManager _assetManager = null!;
    private IEntityFactoryService _entityFactory = null!;
    private MapLoader _mapLoader = null!;

    // Keyboard state for zoom controls
    private KeyboardState _previousKeyboardState;
    private ZOrderRenderSystem _renderSystem = null!;
    private SystemManager _systemManager = null!;
    private World _world = null!;

    /// <summary>
    ///     Initializes a new instance of the PokeSharpGame class.
    /// </summary>
    public PokeSharpGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Set window properties
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        Window.Title = "PokeSharp - Week 1 Demo";
    }

    /// <summary>
    ///     Initializes the game, creating the ECS world and systems.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Create Arch ECS world
        _world = World.Create();

        // Create system manager
        _systemManager = new SystemManager();

        // Initialize AssetManager
        _assetManager = new AssetManager(GraphicsDevice);

        // Load asset manifest
        try
        {
            _assetManager.LoadManifest();
            Console.WriteLine("✅ Asset manifest loaded successfully");

            // Run diagnostics
            AssetDiagnostics.PrintAssetManagerStatus(_assetManager);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to load manifest: {ex.Message}");
            Console.WriteLine("Continuing with empty asset manager...");
        }

        // Initialize entity factory with template system (must be before MapLoader)
        var templateCache = new TemplateCache();
        var factoryLogger = ConsoleLoggerFactory.Create<EntityFactoryService>(LogLevel.Debug);
        TemplateRegistry.RegisterAllTemplates(templateCache);
        _entityFactory = new EntityFactoryService(templateCache, factoryLogger);
        Console.WriteLine("✅ Entity factory initialized with template system");

        // Create map loader with entity factory for template-based tile creation
        _mapLoader = new MapLoader(_assetManager, _entityFactory);
        Console.WriteLine("✅ MapLoader initialized with template support");

        // Create animation library with default player animations
        _animationLibrary = new AnimationLibrary();
        Console.WriteLine(
            $"✅ AnimationLibrary initialized with {_animationLibrary.Count} animations"
        );

        // Create and register systems in priority order
        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        _spatialHashSystem = new SpatialHashSystem();
        _systemManager.RegisterSystem(_spatialHashSystem);

        // InputSystem with Pokemon-style input buffering (5 inputs, 200ms timeout)
        var inputSystem = new InputSystem();
        _systemManager.RegisterSystem(inputSystem);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(_spatialHashSystem);
        _systemManager.RegisterSystem(movementSystem);

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        var collisionSystem = new CollisionSystem();
        collisionSystem.SetSpatialHashSystem(_spatialHashSystem);
        _systemManager.RegisterSystem(collisionSystem);

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        _systemManager.RegisterSystem(new AnimationSystem(_animationLibrary));

        // Register CameraFollowSystem (Priority: 825, after Animation, before TileAnimation)
        _systemManager.RegisterSystem(new CameraFollowSystem());

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        _systemManager.RegisterSystem(new TileAnimationSystem());

        // Register ZOrderRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        // Renders everything (tiles, sprites, objects) based on Y position for authentic Pokemon-style depth
        _renderSystem = new ZOrderRenderSystem(GraphicsDevice, _assetManager);
        _systemManager.RegisterSystem(_renderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

        // Load test map and create map entity
        LoadTestMap();

        // Create test player entity
        CreateTestPlayer();

        // Create test NPCs to demonstrate template system
        CreateTestNpcs();
    }

    /// <summary>
    ///     Loads game content.
    /// </summary>
    protected override void LoadContent()
    {
        // TODO: Load textures and assets here when content pipeline is set up
    }

    /// <summary>
    ///     Updates game logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Handle zoom controls
        HandleZoomControls(deltaTime);

        // Clear the screen BEFORE systems render
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Update all systems (including rendering systems)
        _systemManager.Update(_world, deltaTime);

        base.Update(gameTime);
    }

    /// <summary>
    ///     Renders the game.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Draw(GameTime gameTime)
    {
        // Rendering is handled by ZOrderRenderSystem during Update
        // Clear happens in Update() before systems render to ensure correct order
        base.Draw(gameTime);
    }

    private SpatialHashSystem? _spatialHashSystem;

    /// <summary>
    ///     Loads the test map using the new entity-based tile system.
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    private void LoadTestMap()
    {
        try
        {
            // Load map as tile entities (new ECS-based approach)
            var mapInfoEntity = _mapLoader.LoadMapEntities(_world, "Assets/Maps/test-map.json");

            // Invalidate spatial hash to reindex static tiles
            _spatialHashSystem?.InvalidateStaticTiles();

            // Set camera bounds from MapInfo
            var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
            _world.Query(
                in mapInfoQuery,
                (ref MapInfo mapInfo) =>
                {
                    SetCameraMapBounds(mapInfo.Width, mapInfo.Height);
                    Console.WriteLine(
                        $"✅ Camera bounds set to {mapInfo.PixelWidth}x{mapInfo.PixelHeight} pixels"
                    );
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to load test map: {ex.Message}");
            Console.WriteLine("Continuing without map...");
        }
    }

    /// <summary>
    ///     Creates a test player entity for Week 1 demo.
    ///     Uses the entity factory to spawn from template.
    /// </summary>
    private void CreateTestPlayer()
    {
        // Create camera component with viewport and initial settings
        var viewport = new Rectangle(
            0,
            0,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight
        );
        var camera = new Camera(viewport)
        {
            Zoom = 3.0f,
            TargetZoom = 3.0f,
            ZoomTransitionSpeed = 0.1f,
            Position = new Vector2(10 * 16, 8 * 16), // Start at player's position (grid to pixels)
        };

        // Set map bounds on camera from MapInfo
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        _world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                camera.MapBounds = new Rectangle(0, 0, mapInfo.PixelWidth, mapInfo.PixelHeight);
            }
        );

        // Spawn player entity from template with position override
        var playerEntity = _entityFactory
            .SpawnFromTemplateAsync(
                "player",
                _world,
                builder =>
                {
                    builder.OverrideComponent(new Position(10, 8));
                }
            )
            .GetAwaiter()
            .GetResult();

        // Add Camera component (not in template as it's created per-instance)
        _world.Add(playerEntity, camera);

        Console.WriteLine($"✅ Created player entity from template: {playerEntity}");
        Console.WriteLine(
            "   Components: Player, Position, Sprite, GridMovement, Direction, Animation, InputState, Camera"
        );
        Console.WriteLine("   Template: player");
        Console.WriteLine("🎮 Use WASD or Arrow Keys to move!");
        Console.WriteLine("🔍 Zoom controls: +/- to zoom in/out, 1=GBA, 2=NDS, 3=Default");
    }

    /// <summary>
    ///     Creates test NPCs to demonstrate the entity factory system with template inheritance.
    ///     Spawns NPCs at various positions using different templates that inherit from each other.
    /// </summary>
    private void CreateTestNpcs()
    {
        Console.WriteLine("\n📦 Spawning test NPCs from templates (with inheritance)...");

        // Spawn a generic NPC (inherits from npc/base)
        var genericNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/generic",
                _world,
                builder => builder.OverrideComponent(new Position(15, 8))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Generic NPC spawned at (15, 8) - inherits: npc/base");

        // Spawn a stationary NPC (inherits from npc/base, no movement)
        var stationaryNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/stationary",
                _world,
                builder => builder.OverrideComponent(new Position(18, 8))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Stationary NPC spawned at (18, 8) - inherits: npc/base (no GridMovement)");

        // Spawn a trainer NPC (inherits from npc/generic)
        var trainerNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/trainer",
                _world,
                builder => builder.OverrideComponent(new Position(12, 10))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Trainer NPC spawned at (12, 10) - inherits: npc/generic → npc/base");

        // Spawn a gym leader NPC (inherits from npc/trainer → npc/generic → npc/base)
        var gymLeaderNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/gym-leader",
                _world,
                builder => builder.OverrideComponent(new Position(10, 12))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Gym Leader spawned at (10, 12) - inherits: npc/trainer → npc/generic → npc/base");

        // Spawn a shop keeper NPC (inherits from npc/stationary)
        var shopKeeperNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/shop-keeper",
                _world,
                builder => builder.OverrideComponent(new Position(14, 12))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Shop Keeper spawned at (14, 12) - inherits: npc/stationary → npc/base");

        // Spawn a fast NPC (inherits from npc/generic with overridden speed)
        var fastNpc = _entityFactory
            .SpawnFromTemplateAsync(
                "npc/fast",
                _world,
                builder => builder.OverrideComponent(new Position(16, 12))
            )
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"✅ Fast NPC spawned at (16, 12) - inherits: npc/generic → npc/base (overrides speed)");

        Console.WriteLine($"\n🎯 Total NPCs created: 6");
        Console.WriteLine("   All NPCs created using template inheritance hierarchy!");
        Console.WriteLine("   Template inheritance system working! 🎉\n");
    }

    /// <summary>
    ///     Handles zoom control keyboard input.
    ///     +/- keys for zoom in/out, number keys for presets.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    private void HandleZoomControls(float deltaTime)
    {
        var currentKeyboardState = Keyboard.GetState();

        var query = new QueryDescription().WithAll<Player, Camera>();
        _world.Query(
            in query,
            (ref Camera camera) =>
            {
                // Zoom in with + or = key (since + requires shift)
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemPlus)
                    || IsKeyPressed(currentKeyboardState, Keys.Add)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom + 0.5f);
                    Console.WriteLine($"🔍 Zoom: {camera.TargetZoom:F1}x");
                }

                // Zoom out with - key
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemMinus)
                    || IsKeyPressed(currentKeyboardState, Keys.Subtract)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom - 0.5f);
                    Console.WriteLine($"🔍 Zoom: {camera.TargetZoom:F1}x");
                }

                // Preset zoom levels
                if (IsKeyPressed(currentKeyboardState, Keys.D1))
                {
                    var gbaZoom = camera.CalculateGbaZoom();
                    camera.SetZoomSmooth(gbaZoom);
                    Console.WriteLine($"🔍 GBA Preset: {gbaZoom:F1}x (240x160)");
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D2))
                {
                    var ndsZoom = camera.CalculateNdsZoom();
                    camera.SetZoomSmooth(ndsZoom);
                    Console.WriteLine($"🔍 NDS Preset: {ndsZoom:F1}x (256x192)");
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D3))
                {
                    camera.SetZoomSmooth(3.0f);
                    Console.WriteLine("🔍 Default Preset: 3.0x");
                }
            }
        );

        _previousKeyboardState = currentKeyboardState;
    }

    /// <summary>
    ///     Checks if a key was just pressed (not held).
    /// </summary>
    /// <param name="currentState">Current keyboard state.</param>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was just pressed this frame.</returns>
    private bool IsKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }

    /// <summary>
    ///     Sets the camera map bounds based on tilemap dimensions.
    ///     Prevents the camera from showing areas outside the map.
    /// </summary>
    /// <param name="mapWidthInTiles">Map width in tiles.</param>
    /// <param name="mapHeightInTiles">Map height in tiles.</param>
    private void SetCameraMapBounds(int mapWidthInTiles, int mapHeightInTiles)
    {
        const int tileSize = 16;
        var mapBounds = new Rectangle(
            0,
            0,
            mapWidthInTiles * tileSize,
            mapHeightInTiles * tileSize
        );

        var query = new QueryDescription().WithAll<Player, Camera>();

        _world.Query(
            in query,
            (ref Camera camera) =>
            {
                camera.MapBounds = mapBounds;
            }
        );

        Console.WriteLine($"🎥 Camera bounds set: {mapBounds.Width}x{mapBounds.Height} pixels");
    }

    /// <summary>
    ///     Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _world?.Dispose();

        base.Dispose(disposing);
    }
}
