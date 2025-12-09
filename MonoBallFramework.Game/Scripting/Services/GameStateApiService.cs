using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.GameState;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Flags;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Systems.Queries;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Game state management service implementation using ECS backing.
/// </summary>
/// <remarks>
///     This service stores flags and variables in ECS components for:
///     - Persistence support via Arch.Persistence
///     - Event-driven reactivity via FlagChangedEvent/VariableChangedEvent
///     - Unified data model with other game entities
/// </remarks>
public class GameStateApiService(
    ILogger<GameStateApiService> logger,
    World world,
    IEventBus eventBus,
    IGameStateService? gameStateService = null
) : IGameStateApi
{
    private readonly IEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly IGameStateService? _gameStateService = gameStateService;

    private readonly ILogger<GameStateApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    private Entity _gameStateEntity = Entity.Null;
    private bool _initialized;

    #region Service State

    /// <inheritdoc />
    public bool CollisionServiceEnabled
    {
        get => _gameStateService?.CollisionServiceEnabled ?? true;
        set
        {
            if (_gameStateService != null)
            {
                _gameStateService.CollisionServiceEnabled = value;
            }
            else
            {
                _logger.LogWarning("GameStateApiService: IGameStateService is null, cannot set CollisionServiceEnabled!");
            }
        }
    }

    #endregion

    public bool GetFlag(GameFlagId flagId)
    {
        if (flagId == null)
        {
            return false;
        }

        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();
        return flags.GetFlag(flagId);
    }

    public IGameStateApi SetFlag(GameFlagId flagId, bool value)
    {
        if (flagId == null)
        {
            throw new ArgumentNullException(nameof(flagId), "Flag ID cannot be null");
        }

        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();

        bool oldValue = flags.GetFlag(flagId);
        if (oldValue == value && flags.FlagExists(flagId))
        {
            return this; // No change
        }

        flags.SetFlag(flagId, value);
        _logger.LogDebug("Flag {FlagId} set to {Value}", flagId, value);

        // Publish event for reactive systems
        _eventBus.PublishPooled<FlagChangedEvent>(evt =>
        {
            evt.FlagId = flagId;
            evt.OldValue = oldValue;
            evt.NewValue = value;
        });

        return this;
    }

    public bool FlagExists(GameFlagId flagId)
    {
        if (flagId == null)
        {
            return false;
        }

        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();
        return flags.FlagExists(flagId);
    }

    public string? GetVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();
        return variables.GetVariable(key);
    }

    public IGameStateApi SetVariable(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Variable key cannot be null or empty", nameof(key));
        }

        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();

        string? oldValue = variables.GetVariable(key);
        if (oldValue == value)
        {
            return this; // No change
        }

        variables.SetVariable(key, value);
        _logger.LogDebug("Variable {Key} set to {Value}", key, value);

        // Publish event for reactive systems
        _eventBus.PublishPooled<VariableChangedEvent>(evt =>
        {
            evt.Key = key;
            evt.OldValue = oldValue;
            evt.NewValue = value;
        });

        return this;
    }

    public bool VariableExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();
        return variables.VariableExists(key);
    }

    public IGameStateApi DeleteVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return this;
        }

        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();

        string? oldValue = variables.GetVariable(key);
        if (oldValue == null)
        {
            return this; // Doesn't exist
        }

        variables.DeleteVariable(key);
        _logger.LogDebug("Variable {Key} deleted", key);

        // Publish event for reactive systems
        _eventBus.PublishPooled<VariableChangedEvent>(evt =>
        {
            evt.Key = key;
            evt.OldValue = oldValue;
            evt.NewValue = null;
        });

        return this;
    }

    public IEnumerable<string> GetActiveFlags()
    {
        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();
        return flags.GetActiveFlags();
    }

    public IEnumerable<string> GetVariableKeys()
    {
        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();
        return variables.GetVariableKeys();
    }

    public float Random()
    {
        return (float)System.Random.Shared.NextDouble();
    }

    public int RandomRange(int min, int max)
    {
        if (min >= max)
        {
            throw new ArgumentException("min must be less than max", nameof(min));
        }

        return System.Random.Shared.Next(min, max);
    }

    #region Batch Flag Operations

    /// <inheritdoc />
    public IGameStateApi SetFlags(params string[] flagIds)
    {
        foreach (var flagId in flagIds)
        {
            SetFlag(new GameFlagId(flagId), true);
        }

        return this;
    }

    /// <inheritdoc />
    public IGameStateApi ClearFlags(params string[] flagIds)
    {
        foreach (var flagId in flagIds)
        {
            SetFlag(new GameFlagId(flagId), false);
        }

        return this;
    }

    /// <inheritdoc />
    public bool CheckAllFlags(params string[] flagIds)
    {
        return flagIds.All(f => GetFlag(new GameFlagId(f)));
    }

    /// <inheritdoc />
    public bool CheckAnyFlag(params string[] flagIds)
    {
        return flagIds.Any(f => GetFlag(new GameFlagId(f)));
    }

    /// <inheritdoc />
    public IGameStateApi ToggleFlag(GameFlagId flagId)
    {
        SetFlag(flagId, !GetFlag(flagId));
        return this;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetFlagsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return [];
        }

        var prefix = category.EndsWith('/') ? category : category + "/";
        return GetActiveFlags().Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public int CountSetFlags(params string[] flagIds)
    {
        return flagIds.Count(f => GetFlag(new GameFlagId(f)));
    }

    #endregion

    /// <summary>
    ///     Ensures the GameState singleton entity exists, creating it if necessary.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized && _gameStateEntity.IsAlive())
        {
            return;
        }

        // Try to find existing GameState entity
        QueryDescription query = Queries.GameStateEntity;
        bool found = false;

        _world.Query(
            in query,
            (Entity entity, ref GameState _, ref GameFlags _, ref GameVariables _) =>
            {
                _gameStateEntity = entity;
                found = true;
            }
        );

        if (!found)
        {
            // Create the singleton entity
            _gameStateEntity = _world.Create(
                new GameState(),
                new GameFlags(),
                new GameVariables()
            );
            _logger.LogInformation("Created GameState singleton entity");
        }

        _initialized = true;
    }
}
