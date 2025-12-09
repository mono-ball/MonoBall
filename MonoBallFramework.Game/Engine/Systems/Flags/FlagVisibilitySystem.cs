using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.GameState;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Flags;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Engine.Systems.Flags;

/// <summary>
///     Event-driven system that reacts to flag changes and updates entity visibility.
///     Subscribes to <see cref="FlagChangedEvent"/> and toggles the <see cref="Visible"/>
///     component on entities with matching <see cref="VisibilityFlag"/> components.
/// </summary>
/// <remarks>
///     <para>
///         This system enables pokeemerald-style flag-based NPC visibility:
///         <list type="bullet">
///             <item>FLAG_HIDE_* pattern: NPC hidden when flag is true</item>
///             <item>FLAG_SHOW_* pattern: NPC shown when flag is true</item>
///         </list>
///     </para>
///     <para>
///         <b>Performance Notes:</b>
///         <list type="bullet">
///             <item>Only processes when flags actually change (event-driven)</item>
///             <item>Uses cached query for efficient entity lookup</item>
///             <item>Minimal per-frame overhead (no Update loop)</item>
///         </list>
///     </para>
/// </remarks>
public sealed class FlagVisibilitySystem : EventDrivenSystemBase, IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IGameStateApi _gameStateApi;
    private readonly ILogger<FlagVisibilitySystem>? _logger;
    private IDisposable? _subscription;

    /// <summary>
    ///     Query for entities with visibility flag components.
    /// </summary>
    private static readonly QueryDescription VisibilityFlagQuery = new QueryDescription()
        .WithAll<VisibilityFlag>();

    public FlagVisibilitySystem(
        IEventBus eventBus,
        IGameStateApi gameStateApi,
        ILogger<FlagVisibilitySystem>? logger = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _gameStateApi = gameStateApi ?? throw new ArgumentNullException(nameof(gameStateApi));
        _logger = logger;
    }

    /// <inheritdoc />
    public override int Priority => 50; // Early priority for flag handling

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Subscribe to flag change events
        _subscription = _eventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        _logger?.LogDebug("FlagVisibilitySystem initialized and subscribed to FlagChangedEvent");
    }

    /// <summary>
    ///     Handles flag change events by updating visibility of affected entities.
    /// </summary>
    private void OnFlagChanged(FlagChangedEvent evt)
    {
        if (!Enabled || evt.FlagId == null)
        {
            return;
        }

        GameFlagId changedFlagId = evt.FlagId;
        bool newValue = evt.NewValue;

        _logger?.LogTrace(
            "Processing flag change: {FlagId} = {NewValue}",
            changedFlagId, newValue);

        int affected = 0;

        // Query all entities with VisibilityFlag and check if they match
        World.Query(in VisibilityFlagQuery, (Entity entity, ref VisibilityFlag visFlag) =>
        {
            // Check if this entity is controlled by the changed flag
            if (visFlag.FlagId.Value != changedFlagId.Value)
            {
                return;
            }

            bool shouldBeVisible = visFlag.ShouldBeVisible(newValue);
            bool hasVisible = entity.Has<Visible>();

            if (shouldBeVisible && !hasVisible)
            {
                // Add Visible component to show entity
                entity.Add(new Visible());
                affected++;
                _logger?.LogDebug(
                    "Entity shown due to flag {FlagId} = {Value}",
                    changedFlagId, newValue);
            }
            else if (!shouldBeVisible && hasVisible)
            {
                // Remove Visible component to hide entity
                entity.Remove<Visible>();
                affected++;
                _logger?.LogDebug(
                    "Entity hidden due to flag {FlagId} = {Value}",
                    changedFlagId, newValue);
            }
        });

        if (affected > 0)
        {
            _logger?.LogDebug(
                "Flag {FlagId} change affected {Count} entities",
                changedFlagId, affected);
        }
    }

    /// <summary>
    ///     Synchronizes visibility for all entities with VisibilityFlag components.
    ///     Call this after loading a map or save file to ensure visibility matches flag state.
    /// </summary>
    public void SynchronizeAllVisibility()
    {
        EnsureInitialized();

        int synchronized = 0;

        World.Query(in VisibilityFlagQuery, (Entity entity, ref VisibilityFlag visFlag) =>
        {
            bool flagValue = _gameStateApi.GetFlag(visFlag.FlagId);
            bool shouldBeVisible = visFlag.ShouldBeVisible(flagValue);
            bool hasVisible = entity.Has<Visible>();

            if (shouldBeVisible && !hasVisible)
            {
                entity.Add(new Visible());
                synchronized++;
            }
            else if (!shouldBeVisible && hasVisible)
            {
                entity.Remove<Visible>();
                synchronized++;
            }
        });

        _logger?.LogDebug("Synchronized visibility for {Count} entities", synchronized);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}
