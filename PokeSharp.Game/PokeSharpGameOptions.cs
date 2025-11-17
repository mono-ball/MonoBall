using Arch.Core;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game;

/// <summary>
///     Options for configuring PokeSharpGame.
///     Groups related dependencies to reduce constructor parameter count.
/// </summary>
public sealed class PokeSharpGameOptions
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
    ///     Gets or sets the entity factory service.
    /// </summary>
    public IEntityFactoryService EntityFactory { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the script service.
    /// </summary>
    public ScriptService ScriptService { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the behavior registry.
    /// </summary>
    public TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; init; } = null!;

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
    public EntityPoolManager PoolManager { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the game data loader.
    /// </summary>
    public GameDataLoader DataLoader { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the NPC definition service.
    /// </summary>
    public NpcDefinitionService NpcDefinitionService { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the map definition service.
    /// </summary>
    public MapDefinitionService MapDefinitionService { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the sprite loader.
    /// </summary>
    public SpriteLoader SpriteLoader { get; init; } = null!;

    /// <summary>
    ///     Gets or sets the template cache initializer.
    /// </summary>
    public TemplateCacheInitializer TemplateCacheInitializer { get; init; } = null!;
}

