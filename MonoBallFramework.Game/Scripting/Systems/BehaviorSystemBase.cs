using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Configuration;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Systems;

/// <summary>
///     Abstract base class for behavior systems (NPC and Tile) that provides common infrastructure
///     for script execution, logging, and performance tracking.
/// </summary>
/// <remarks>
///     This base class extracts duplicated code from NPCBehaviorSystem and TileBehaviorSystem:
///     - Script logger caching with memory limits
///     - Performance metric tracking (tick counter, behavior counts)
///     - Event bus and API provider management
///     - Common logging patterns
/// </remarks>
public abstract class BehaviorSystemBase : SystemBase
{
    /// <summary>
    ///     Cache of loggers for individual script instances.
    ///     Key format is typically "{BehaviorTypeId}.{EntityId}" or "{BehaviorTypeId}.{NpcId}".
    /// </summary>
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();

    /// <summary>
    ///     API provider for script access to game systems.
    /// </summary>
    protected readonly IScriptingApiProvider Apis;

    /// <summary>
    ///     Performance configuration for cache limits and logging thresholds.
    /// </summary>
    protected readonly PerformanceConfiguration Config;

    /// <summary>
    ///     Event bus for publishing and subscribing to game events.
    /// </summary>
    protected readonly IEventBus? EventBus;

    /// <summary>
    ///     Logger factory for creating per-script loggers.
    /// </summary>
    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    ///     Last count of behaviors executed, used to detect changes for logging.
    /// </summary>
    private int _lastBehaviorSummaryCount;

    /// <summary>
    ///     Tick counter for periodic logging.
    /// </summary>
    private int _tickCounter;

    /// <summary>
    ///     Initializes the behavior system base with required dependencies.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="apis">API provider for script access.</param>
    /// <param name="eventBus">Optional event bus for event-driven behaviors.</param>
    /// <param name="config">Optional performance configuration.</param>
    protected BehaviorSystemBase(
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis,
        IEventBus? eventBus = null,
        PerformanceConfiguration? config = null)
    {
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Apis = apis ?? throw new ArgumentNullException(nameof(apis));
        EventBus = eventBus;
        Config = config ?? PerformanceConfiguration.Default;
    }

    /// <summary>
    ///     Gets the logger prefix for script loggers (e.g., "Script" or "TileBehavior").
    /// </summary>
    protected abstract string LoggerPrefix { get; }

    /// <summary>
    ///     Gets the system logger for derived class logging.
    /// </summary>
    protected abstract ILogger SystemLogger { get; }

    /// <summary>
    ///     Gets the total number of cached loggers.
    /// </summary>
    protected int CachedLoggerCount => _scriptLoggerCache.Count;

    /// <summary>
    ///     Gets or creates a logger for a specific behavior instance.
    ///     Implements size limit to prevent unbounded memory growth.
    /// </summary>
    /// <param name="key">Logger key (e.g., behavior type + entity/NPC ID)</param>
    /// <returns>Cached or newly created logger</returns>
    protected ILogger GetOrCreateLogger(string key)
    {
        return _scriptLoggerCache.GetOrAdd(
            key,
            k =>
            {
                // Check if we've hit the cache limit
                if (_scriptLoggerCache.Count >= Config.MaxCachedLoggers)
                {
                    SystemLogger.LogWarning(
                        "Script logger cache limit reached ({Limit}). Consider increasing limit or checking for leaks.",
                        Config.MaxCachedLoggers);
                }

                return LoggerFactory.CreateLogger($"{LoggerPrefix}.{k}");
            });
    }

    /// <summary>
    ///     Removes a logger from the cache when a behavior is deactivated.
    /// </summary>
    /// <param name="key">Logger key to remove</param>
    protected void RemoveLogger(string key)
    {
        _scriptLoggerCache.TryRemove(key, out _);
    }

    /// <summary>
    ///     Removes a logger from the cache using component-based key construction.
    /// </summary>
    /// <param name="behaviorTypeId">Behavior type ID</param>
    /// <param name="entityId">Entity or NPC ID</param>
    protected void RemoveLogger(string behaviorTypeId, string entityId)
    {
        string key = $"{behaviorTypeId}.{entityId}";
        _scriptLoggerCache.TryRemove(key, out _);
    }

    /// <summary>
    ///     Logs performance metrics periodically or when significant changes occur.
    /// </summary>
    /// <param name="behaviorCount">Number of behaviors executed this tick.</param>
    /// <param name="errorCount">Number of errors encountered this tick.</param>
    /// <param name="summaryLabel">Label for the summary log (e.g., "Behavior tick summary").</param>
    protected void LogPerformanceMetrics(int behaviorCount, int errorCount, string summaryLabel)
    {
        _tickCounter++;

        bool shouldLogSummary =
            errorCount > 0
            || (_tickCounter % 60 == 0 && behaviorCount > 0)
            || (behaviorCount > 0 && behaviorCount != _lastBehaviorSummaryCount);

        if (shouldLogSummary)
        {
            SystemLogger.LogWorkflowStatus(
                summaryLabel,
                ("executed", behaviorCount),
                ("errors", errorCount));
        }

        if (behaviorCount > 0)
        {
            _lastBehaviorSummaryCount = behaviorCount;
        }
    }

    /// <summary>
    ///     Validates that the event bus is available, throwing if required.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when EventBus is null.</exception>
    protected IEventBus RequireEventBus()
    {
        return EventBus ?? throw new InvalidOperationException("EventBus is required for ScriptContext");
    }

    /// <summary>
    ///     Clears all cached loggers. Use during cleanup or testing.
    /// </summary>
    protected void ClearLoggerCache()
    {
        _scriptLoggerCache.Clear();
    }
}
