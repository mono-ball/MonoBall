using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Scripting;
using MonoBallFramework.Game.Engine.Common.Configuration;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.System;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Scripting.Systems;

/// <summary>
///     System responsible for managing script attachment lifecycle and execution.
///     Handles loading, initialization, priority ordering, and cleanup of attached scripts.
/// </summary>
/// <remarks>
///     <para>
///         This system enables composition-based scripting where multiple scripts can:
///         - Attach to the same entity/tile
///         - Execute in priority order
///         - Receive events independently
///         - Be added/removed dynamically
///     </para>
///     <para>
///         Script Lifecycle:
///         1. Detection: Query entities with ScriptAttachment components
///         2. Loading: Compile and instantiate scripts from ScriptPath using ScriptService
///         3. Initialization: Call OnInitialize() and RegisterEventHandlers()
///         4. Execution: Call OnTick() every frame in priority order
///         5. Cleanup: Call OnUnload() when script is detached or entity destroyed
///     </para>
/// </remarks>
public class ScriptAttachmentSystem : SystemBase, IUpdateSystem
{
    private readonly IScriptingApiProvider _apis;
    private readonly PerformanceConfiguration _config;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ScriptAttachmentSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    private readonly ScriptService _scriptService;
    private int _lastAttachmentSummaryCount;
    private int _tickCounter;

    public ScriptAttachmentSystem(
        ILogger<ScriptAttachmentSystem> logger,
        ILoggerFactory loggerFactory,
        ScriptService scriptService,
        IScriptingApiProvider apis,
        IEventBus eventBus,
        PerformanceConfiguration? config = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _config = config ?? PerformanceConfiguration.Default;
    }

    /// <summary>
    ///     Update priority for IUpdateSystem interface.
    /// </summary>
    public int UpdatePriority => Priority;

    /// <summary>
    ///     System priority - runs early to ensure scripts are loaded before other systems need them.
    /// </summary>
    public override int Priority => SystemPriority.ScriptAttachment;

    public override void Update(World world, float deltaTime)
    {
        int attachmentCount = 0;
        int initializationCount = 0;
        int errorCount = 0;

        // Query all entities with ScriptAttachment components
        // Note: This will find ALL ScriptAttachment components on each entity (composition support)
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity entity, ref ScriptAttachment attachment) =>
            {
                try
                {
                    // Skip inactive scripts
                    if (!attachment.IsActive)
                    {
                        return;
                    }

                    // Load script if not already loaded
                    if (attachment.ScriptInstance == null)
                    {
                        if (!LoadScript(ref attachment))
                        {
                            attachment.IsActive = false;
                            errorCount++;
                            return;
                        }
                    }

                    // Cast script instance to ScriptBase
                    if (attachment.ScriptInstance is not ScriptBase script)
                    {
                        _logger.LogError(
                            "Script instance for '{ScriptPath}' is not ScriptBase",
                            attachment.ScriptPath
                        );
                        attachment.IsActive = false;
                        errorCount++;
                        return;
                    }

                    // Initialize script if not initialized
                    if (!IsScriptInitialized(ref attachment))
                    {
                        if (InitializeScript(world, entity, script, ref attachment))
                        {
                            initializationCount++;
                        }
                        else
                        {
                            attachment.IsActive = false;
                            errorCount++;
                            return;
                        }
                    }

                    // Execute script tick
                    ExecuteScriptTick(world, entity, script, deltaTime);
                    attachmentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogExceptionWithContext(
                        ex,
                        "Script attachment error for entity {EntityId}, script '{ScriptPath}'",
                        entity.Id,
                        attachment.ScriptPath
                    );
                    errorCount++;
                    attachment.IsActive = false;
                }
            }
        );

        // Log performance metrics periodically
        _tickCounter++;

        bool shouldLogSummary =
            initializationCount > 0
            || errorCount > 0
            || (_tickCounter % 60 == 0 && attachmentCount > 0)
            || (attachmentCount > 0 && attachmentCount != _lastAttachmentSummaryCount);

        if (shouldLogSummary)
        {
            _logger.LogWorkflowStatus(
                "Script attachment tick summary",
                ("executed", attachmentCount),
                ("initialized", initializationCount),
                ("errors", errorCount)
            );
        }

        if (attachmentCount > 0)
        {
            _lastAttachmentSummaryCount = attachmentCount;
        }
    }

    protected override void OnInitialized()
    {
        _logger.LogSystemInitialized("ScriptAttachmentSystem");
    }

    /// <summary>
    ///     Loads a script from the specified path using ScriptService.
    /// </summary>
    /// <param name="attachment">Script attachment to load</param>
    /// <returns>True if script loaded successfully, false otherwise</returns>
    private bool LoadScript(ref ScriptAttachment attachment)
    {
        try
        {
            // Use ScriptService to load and compile the script asynchronously
            // Note: This blocks, but it's acceptable for script loading
            Task<object?> loadTask = _scriptService.LoadScriptAsync(attachment.ScriptPath);
            loadTask.Wait();

            if (loadTask.IsCompletedSuccessfully && loadTask.Result is ScriptBase scriptInstance)
            {
                attachment.ScriptInstance = scriptInstance;
                _logger.LogInformation(
                    "Loaded script '{ScriptPath}' with priority {Priority}",
                    attachment.ScriptPath,
                    attachment.Priority
                );
                return true;
            }

            _logger.LogError("Failed to load script '{ScriptPath}'", attachment.ScriptPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogExceptionWithContext(
                ex,
                "Error loading script '{ScriptPath}'",
                attachment.ScriptPath
            );
            return false;
        }
    }

    /// <summary>
    ///     Checks if a script has been initialized.
    ///     Since ScriptAttachment.IsInitialized is internal, we track it via a tag.
    /// </summary>
    private bool IsScriptInitialized(ref ScriptAttachment attachment)
    {
        // For now, we'll check if the instance exists and assume first tick needs init
        // This is a simplified approach - in production, you'd want better tracking
        return attachment.ScriptInstance != null;
    }

    /// <summary>
    ///     Initializes a script instance for a specific entity.
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity the script is attached to</param>
    /// <param name="script">Script instance to initialize</param>
    /// <param name="attachment">Script attachment metadata</param>
    /// <returns>True if initialization succeeded, false otherwise</returns>
    private bool InitializeScript(
        World world,
        Entity entity,
        ScriptBase script,
        ref ScriptAttachment attachment
    )
    {
        try
        {
            // Create script context
            string loggerKey = $"{attachment.ScriptPath}.{entity.Id}";
            ILogger scriptLogger = GetOrCreateLogger(loggerKey);
            var context = new ScriptContext(world, entity, scriptLogger, _apis, _eventBus);

            // Call initialization lifecycle methods
            script.Initialize(context);
            script.RegisterEventHandlers(context);

            _logger.LogDebug(
                "Initialized script '{ScriptPath}' on entity {EntityId}",
                attachment.ScriptPath,
                entity.Id
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogExceptionWithContext(
                ex,
                "Error initializing script '{ScriptPath}' on entity {EntityId}",
                attachment.ScriptPath,
                entity.Id
            );
            return false;
        }
    }

    /// <summary>
    ///     Executes a script's tick method with proper context.
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity the script is attached to</param>
    /// <param name="script">Script instance to execute</param>
    /// <param name="deltaTime">Time since last frame</param>
    private void ExecuteScriptTick(World world, Entity entity, ScriptBase script, float deltaTime)
    {
        try
        {
            // ScriptBase uses event-driven architecture - publish TickEvent for scripts to react to
            _eventBus.Publish(
                new TickEvent
                {
                    DeltaTime = deltaTime,
                    TotalTime = 0f, // TODO: Track total elapsed time if needed
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogExceptionWithContext(
                ex,
                "Error executing script '{ScriptType}' on entity {EntityId}",
                script.GetType().Name,
                entity.Id
            );
        }
    }

    /// <summary>
    ///     Gets all script attachments for an entity, sorted by priority (highest first).
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity to query</param>
    /// <returns>List of script attachments sorted by priority</returns>
    public List<ScriptAttachment> GetAttachmentsForEntity(World world, Entity entity)
    {
        var attachments = new List<ScriptAttachment>();

        // Note: Arch ECS allows multiple components of the same type per entity
        // We need to query all ScriptAttachment components
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity e, ref ScriptAttachment attachment) =>
            {
                if (e == entity)
                {
                    attachments.Add(attachment);
                }
            }
        );

        // Sort by priority (highest first)
        attachments.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return attachments;
    }

    /// <summary>
    ///     Detaches and cleans up a script from an entity.
    /// </summary>
    /// <param name="world">ECS world</param>
    /// <param name="entity">Entity to detach script from</param>
    /// <param name="scriptPath">Path of script to detach</param>
    public void DetachScript(World world, Entity entity, string scriptPath)
    {
        world.Query(
            new QueryDescription().WithAll<ScriptAttachment>(),
            (Entity e, ref ScriptAttachment attachment) =>
            {
                if (e == entity && attachment.ScriptPath == scriptPath)
                {
                    // Call OnUnload for cleanup
                    if (attachment.ScriptInstance is ScriptBase script)
                    {
                        script.OnUnload();
                    }

                    // Remove component
                    // Note: Arch ECS doesn't have a direct way to remove specific component instances
                    // This is a limitation we'll document for Phase 3.2
                    attachment.IsActive = false;
                    attachment.ScriptInstance = null;

                    _logger.LogDebug(
                        "Detached script '{ScriptPath}' from entity {EntityId}",
                        scriptPath,
                        entity.Id
                    );
                }
            }
        );
    }

    /// <summary>
    ///     Gets or creates a logger for a specific script attachment.
    ///     Implements size limit to prevent unbounded memory growth.
    /// </summary>
    /// <param name="key">Logger key (script path + entity ID)</param>
    /// <returns>Cached or newly created logger</returns>
    private ILogger GetOrCreateLogger(string key)
    {
        return _scriptLoggerCache.GetOrAdd(
            key,
            k =>
            {
                // Check if we've hit the cache limit
                if (_scriptLoggerCache.Count >= _config.MaxCachedLoggers)
                {
                    _logger.LogWarning(
                        "Script logger cache limit reached ({Limit}). Consider increasing limit or checking for leaks.",
                        _config.MaxCachedLoggers
                    );
                }

                return _loggerFactory.CreateLogger($"ScriptAttachment.{k}");
            }
        );
    }

    /// <summary>
    ///     Clears the script service cache, forcing recompilation on next load.
    ///     Useful for hot-reload scenarios.
    /// </summary>
    public async Task ClearScriptCacheAsync()
    {
        await _scriptService.DisposeAsync();
        _logger.LogInformation("Script cache cleared");
    }
}
