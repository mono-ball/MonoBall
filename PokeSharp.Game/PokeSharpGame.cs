using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Input.Components;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Game.Diagnostics;

namespace PokeSharp.Game;

/// <summary>
/// Main game class for PokeSharp.
/// Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private World _world = null!;
    private SystemManager _systemManager = null!;
    private AssetManager _assetManager = null!;
    private MapLoader _mapLoader = null!;
    private AnimationLibrary _animationLibrary = null!;
    private RenderSystem _renderSystem = null!;

    /// <summary>
    /// Initializes a new instance of the PokeSharpGame class.
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
    /// Initializes the game, creating the ECS world and systems.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Create Arch ECS world
        _world = World.Create();

        // Create system manager
        _systemManager = new SystemManager();

        // Initialize AssetManager
        _assetManager = new AssetManager(GraphicsDevice, "Assets");

        // Load asset manifest
        try
        {
            _assetManager.LoadManifest("Assets/manifest.json");
            System.Console.WriteLine("✅ Asset manifest loaded successfully");

            // Run diagnostics
            AssetDiagnostics.PrintAssetManagerStatus(_assetManager);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"⚠️ Failed to load manifest: {ex.Message}");
            System.Console.WriteLine("Continuing with empty asset manager...");
        }

        // Create map loader
        _mapLoader = new MapLoader(_assetManager);

        // Create animation library with default player animations
        _animationLibrary = new AnimationLibrary(logger: null);
        System.Console.WriteLine($"✅ AnimationLibrary initialized with {_animationLibrary.Count} animations");

        // Create and register systems in priority order
        _systemManager.RegisterSystem(new InputSystem());

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        _systemManager.RegisterSystem(new CollisionSystem());

        _systemManager.RegisterSystem(new MovementSystem());

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        _systemManager.RegisterSystem(new AnimationSystem(_animationLibrary, logger: null));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and MapRender)
        _systemManager.RegisterSystem(new TileAnimationSystem());

        // Register MapRenderSystem before RenderSystem (MapRender priority: 900, Render priority: 1000)
        _systemManager.RegisterSystem(new MapRenderSystem(GraphicsDevice, _assetManager));

        // Render system needs graphics device and asset manager
        _renderSystem = new RenderSystem(GraphicsDevice, _assetManager);
        _systemManager.RegisterSystem(_renderSystem);

        // Register OverheadRenderSystem AFTER RenderSystem (Overhead priority: 1050, after sprites)
        _systemManager.RegisterSystem(new OverheadRenderSystem(GraphicsDevice, _assetManager));

        // Initialize all systems
        _systemManager.Initialize(_world);

        // Load test map and create map entity
        LoadTestMap();

        // Create test player entity
        CreateTestPlayer();
    }

    /// <summary>
    /// Loads game content.
    /// </summary>
    protected override void LoadContent()
    {
        // TODO: Load textures and assets here when content pipeline is set up
    }

    /// <summary>
    /// Updates game logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Clear the screen BEFORE systems render
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Update all systems (including rendering systems)
        _systemManager.Update(_world, deltaTime);

        base.Update(gameTime);
    }

    /// <summary>
    /// Renders the game.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Draw(GameTime gameTime)
    {
        // Rendering is handled by RenderSystem during Update
        // Clear happens in Update() before systems render to ensure correct order
        base.Draw(gameTime);
    }

    /// <summary>
    /// Loads the test map and creates a map entity.
    /// </summary>
    private void LoadTestMap()
    {
        try
        {
            // Load test map from JSON
            var tileMap = _mapLoader.LoadMap("Assets/Maps/test-map.json");
            var tileCollider = _mapLoader.LoadCollision("Assets/Maps/test-map.json");
            var animatedTiles = _mapLoader.LoadAnimatedTiles("Assets/Maps/test-map.json");

            // Store animated tiles directly in TileMap component
            tileMap.AnimatedTiles = animatedTiles;

            // Create map entity with TileMap and TileCollider components
            var mapEntity = _world.Create(tileMap, tileCollider);

            System.Console.WriteLine($"✅ Loaded test map: {tileMap.MapId} ({tileMap.Width}x{tileMap.Height} tiles)");
            System.Console.WriteLine($"   Map entity: {mapEntity}");
            System.Console.WriteLine($"   Animated tiles: {animatedTiles.Length}");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"⚠️ Failed to load test map: {ex.Message}");
            System.Console.WriteLine("Continuing without map...");
        }
    }

    /// <summary>
    /// Creates a test player entity for Week 1 demo.
    /// </summary>
    private void CreateTestPlayer()
    {
        // Create player entity with all required components
        var playerEntity = _world.Create(
            new Player(),
            new Position(10, 8), // Start at grid position (10, 8)
            new Sprite("player-spritesheet") // Use spritesheet for animations
            {
                Tint = Color.White,
                Scale = 1f
            },
            new GridMovement(4.0f), // 4 tiles per second movement speed
            Direction.Down, // Initial facing direction
            new PokeSharp.Core.Components.Animation("idle_down"), // Start with idle animation
            new InputState()
        );

        System.Console.WriteLine($"✅ Created player entity: {playerEntity}");
        System.Console.WriteLine("   Components: Player, Position, Sprite, GridMovement, Direction, Animation, InputState");
        System.Console.WriteLine("🎮 Use WASD or Arrow Keys to move!");
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _world?.Dispose();
        }

        base.Dispose(disposing);
    }
}
