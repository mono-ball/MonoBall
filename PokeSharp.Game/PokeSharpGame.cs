using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Input.Components;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Rendering.Components;
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
    private ZOrderRenderSystem _renderSystem = null!;

    // Keyboard state for zoom controls
    private KeyboardState _previousKeyboardState;

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
        // InputSystem with Pokemon-style input buffering (5 inputs, 200ms timeout)
        _systemManager.RegisterSystem(new InputSystem(maxBufferSize: 5, bufferTimeout: 0.2f));

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        _systemManager.RegisterSystem(new CollisionSystem());

        _systemManager.RegisterSystem(new MovementSystem());

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        _systemManager.RegisterSystem(new AnimationSystem(_animationLibrary, logger: null));

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

        // Handle zoom controls
        HandleZoomControls(deltaTime);

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
        // Rendering is handled by ZOrderRenderSystem during Update
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
            // Load test map entity with all components (single file parse - 4x faster!)
            var mapEntity = _mapLoader.LoadMapEntity(_world, "Assets/Maps/test-map.json");

            // Set camera bounds from map dimensions
            var mapQuery = new QueryDescription().WithAll<TileMap>();
            _world.Query(in mapQuery, (ref TileMap tileMap) =>
            {
                SetCameraMapBounds(tileMap.Width, tileMap.Height);
            });
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
        // Create camera component with viewport and initial settings
        var viewport = new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
        var camera = new Camera(viewport, smoothingSpeed: 0.2f, leadDistance: 1.5f)
        {
            Zoom = 3.0f,
            TargetZoom = 3.0f,
            ZoomTransitionSpeed = 0.1f,
            Position = new Vector2(10 * 16, 8 * 16) // Start at player's position (grid to pixels)
        };

        // Set map bounds on camera from the loaded tilemap
        var mapQuery = new QueryDescription().WithAll<TileMap>();
        _world.Query(in mapQuery, (ref TileMap tileMap) =>
        {
            const int tileSize = 16;
            camera.MapBounds = new Rectangle(0, 0, tileMap.Width * tileSize, tileMap.Height * tileSize);
        });

        // Create player entity with all required components including Camera
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
            new InputState(),
            camera // Add camera component for CameraFollowSystem
        );

        System.Console.WriteLine($"✅ Created player entity: {playerEntity}");
        System.Console.WriteLine("   Components: Player, Position, Sprite, GridMovement, Direction, Animation, InputState, Camera");
        System.Console.WriteLine("🎮 Use WASD or Arrow Keys to move!");
        System.Console.WriteLine("🔍 Zoom controls: +/- to zoom in/out, 1=GBA, 2=NDS, 3=Default");
    }

    /// <summary>
    /// Handles zoom control keyboard input.
    /// +/- keys for zoom in/out, number keys for presets.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    private void HandleZoomControls(float deltaTime)
    {
        var currentKeyboardState = Keyboard.GetState();

        var query = new QueryDescription().WithAll<Player, Camera>();
        _world.Query(in query, (ref Camera camera) =>
        {
            // Zoom in with + or = key (since + requires shift)
            if (IsKeyPressed(currentKeyboardState, Keys.OemPlus) || IsKeyPressed(currentKeyboardState, Keys.Add))
            {
                camera.SetZoomSmooth(camera.TargetZoom + 0.5f);
                System.Console.WriteLine($"🔍 Zoom: {camera.TargetZoom:F1}x");
            }

            // Zoom out with - key
            if (IsKeyPressed(currentKeyboardState, Keys.OemMinus) || IsKeyPressed(currentKeyboardState, Keys.Subtract))
            {
                camera.SetZoomSmooth(camera.TargetZoom - 0.5f);
                System.Console.WriteLine($"🔍 Zoom: {camera.TargetZoom:F1}x");
            }

            // Preset zoom levels
            if (IsKeyPressed(currentKeyboardState, Keys.D1))
            {
                var gbaZoom = camera.CalculateGbaZoom();
                camera.SetZoomSmooth(gbaZoom);
                System.Console.WriteLine($"🔍 GBA Preset: {gbaZoom:F1}x (240x160)");
            }

            if (IsKeyPressed(currentKeyboardState, Keys.D2))
            {
                var ndsZoom = camera.CalculateNdsZoom();
                camera.SetZoomSmooth(ndsZoom);
                System.Console.WriteLine($"🔍 NDS Preset: {ndsZoom:F1}x (256x192)");
            }

            if (IsKeyPressed(currentKeyboardState, Keys.D3))
            {
                camera.SetZoomSmooth(3.0f);
                System.Console.WriteLine($"🔍 Default Preset: 3.0x");
            }
        });

        _previousKeyboardState = currentKeyboardState;
    }

    /// <summary>
    /// Checks if a key was just pressed (not held).
    /// </summary>
    /// <param name="currentState">Current keyboard state.</param>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was just pressed this frame.</returns>
    private bool IsKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }

    /// <summary>
    /// Sets the camera map bounds based on tilemap dimensions.
    /// Prevents the camera from showing areas outside the map.
    /// </summary>
    /// <param name="mapWidthInTiles">Map width in tiles.</param>
    /// <param name="mapHeightInTiles">Map height in tiles.</param>
    private void SetCameraMapBounds(int mapWidthInTiles, int mapHeightInTiles)
    {
        const int tileSize = 16;
        var mapBounds = new Rectangle(0, 0, mapWidthInTiles * tileSize, mapHeightInTiles * tileSize);

        var query = new QueryDescription().WithAll<Player, Camera>();

        _world.Query(in query, (ref Camera camera) =>
        {
            camera.MapBounds = mapBounds;
        });

        System.Console.WriteLine($"🎥 Camera bounds set: {mapBounds.Width}x{mapBounds.Height} pixels");
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
