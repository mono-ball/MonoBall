using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.GameState;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Flags;
using MonoBallFramework.Game.Engine.Systems.Queries;
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
    IEventBus eventBus
) : IGameStateApi
{
    private readonly IEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly ILogger<GameStateApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    private Entity _gameStateEntity = Entity.Null;
    private bool _initialized;

    public bool GetFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return false;
        }

        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();
        return flags.GetFlag(flagId);
    }

    public void SetFlag(string flagId, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            throw new ArgumentException("Flag ID cannot be null or empty", nameof(flagId));
        }

        EnsureInitialized();
        ref GameFlags flags = ref _gameStateEntity.Get<GameFlags>();

        bool oldValue = flags.GetFlag(flagId);
        if (oldValue == value && flags.FlagExists(flagId))
        {
            return; // No change
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
    }

    public bool FlagExists(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
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

    public void SetVariable(string key, string value)
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
            return; // No change
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

    public void DeleteVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        EnsureInitialized();
        ref GameVariables variables = ref _gameStateEntity.Get<GameVariables>();

        string? oldValue = variables.GetVariable(key);
        if (oldValue == null)
        {
            return; // Doesn't exist
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
