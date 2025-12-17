using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Initialization.Factories;
using MonoBallFramework.Game.Input;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game;

/// <summary>
///     Options for configuring MonoBallFrameworkGame.
///     Groups related dependencies to reduce constructor parameter count.
/// </summary>
public sealed class MonoBallFrameworkGameOptions
{
    /// <summary>
    ///     Gets or sets the ECS world.
    /// </summary>
    public World World { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the system manager.
    /// </summary>
    public SystemManager SystemManager { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the script service.
    /// </summary>
    public ScriptService ScriptService { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the behavior registry.
    /// </summary>
    public TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the tile behavior registry.
    /// </summary>
    public TypeRegistry<TileBehaviorDefinition> TileBehaviorRegistry { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the scripting API provider.
    /// </summary>
    public IScriptingApiProvider ApiProvider { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the performance monitor.
    /// </summary>
    public PerformanceMonitor PerformanceMonitor { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the input manager.
    /// </summary>
    public InputManager InputManager { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the player factory.
    /// </summary>
    public PlayerFactory PlayerFactory { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the game time service.
    /// </summary>
    public IGameTimeService GameTime { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the entity pool manager.
    /// </summary>
    /// <summary>
    ///     Gets or sets the game data loader.
    /// </summary>
    public GameDataLoader DataLoader { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the map definition service.
    /// </summary>
    public MapEntityService MapEntityService { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the sprite registry.
    /// </summary>
    public SpriteRegistry SpriteRegistry { get; init; } = null!;
}
