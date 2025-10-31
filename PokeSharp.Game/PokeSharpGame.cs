using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Input.Components;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Systems;

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

        // Create and register systems in priority order
        _systemManager.RegisterSystem(new InputSystem());
        _systemManager.RegisterSystem(new MovementSystem());

        // Render system needs graphics device
        _renderSystem = new RenderSystem(GraphicsDevice);
        _systemManager.RegisterSystem(_renderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

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

        // Update all systems
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
        // This is intentional to keep all system logic in the ECS Update loop
        base.Draw(gameTime);
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
            new Sprite("pixel")
            {
                Tint = Color.LimeGreen,
                Scale = 16f // Scale up the 1x1 pixel to 16x16
            },
            new GridMovement(4.0f), // 4 tiles per second movement speed
            new InputState()
        );

        System.Console.WriteLine($"Created player entity: {playerEntity}");
        System.Console.WriteLine("Use WASD or Arrow Keys to move the green square!");
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
