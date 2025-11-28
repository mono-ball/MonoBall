using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Game-specific logging extensions for common patterns in PokeSharp.
///     Uses structured logging with proper scopes and enrichment.
/// </summary>
public static class GameLoggingExtensions
{
    /// <summary>
    ///     Logs entity creation with structured data.
    /// </summary>
    public static void LogEntityCreated(
        this ILogger logger,
        int entityId,
        string entityType,
        Dictionary<string, object>? additionalData = null
    )
    {
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object> { ["EntityId"] = entityId, ["EntityType"] = entityType }
        );

        string message =
            "[gold1]E[/] [green]✓[/] Entity created | id: [cyan]{EntityId}[/], type: [cyan]{EntityType}[/]";
        var args = new List<object> { entityId, entityType };

        if (additionalData != null)
        {
            foreach (KeyValuePair<string, object> kvp in additionalData)
            {
                message += $", {kvp.Key}: [cyan]{{{kvp.Key}}}[/]";
                args.Add(kvp.Value);
            }
        }

        logger.LogInformation(message, args.ToArray());
    }

    /// <summary>
    ///     Logs component addition to an entity.
    /// </summary>
    public static void LogComponentAdded(
        this ILogger logger,
        int entityId,
        string componentType,
        object? componentData = null
    )
    {
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["EntityId"] = entityId,
                ["ComponentType"] = componentType,
            }
        );

        if (componentData != null)
        {
            logger.LogDebug(
                "[gold1]E[/] [green]✓[/] Component added | entity: [cyan]{EntityId}[/], component: [cyan]{ComponentType}[/], data: {@ComponentData}",
                entityId,
                componentType,
                componentData
            );
        }
        else
        {
            logger.LogDebug(
                "[gold1]E[/] [green]✓[/] Component added | entity: [cyan]{EntityId}[/], component: [cyan]{ComponentType}[/]",
                entityId,
                componentType
            );
        }
    }

    /// <summary>
    ///     Logs system execution with timing.
    /// </summary>
    public static void LogSystemExecution(
        this ILogger logger,
        string systemName,
        double executionTimeMs,
        int entitiesProcessed = 0
    )
    {
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["SystemName"] = systemName,
                ["ExecutionTimeMs"] = executionTimeMs,
                ["EntitiesProcessed"] = entitiesProcessed,
            }
        );

        if (executionTimeMs > 16.67) // More than one frame at 60fps
        {
            logger.LogWarning(
                "[orange3]SYS[/] [orange3]⚠[/] System execution slow | system: [cyan]{SystemName}[/], time: [yellow]{ExecutionTimeMs:F2}ms[/], entities: [yellow]{EntitiesProcessed}[/]",
                systemName,
                executionTimeMs,
                entitiesProcessed
            );
        }
        else
        {
            logger.LogDebug(
                "[orange3]SYS[/] [green]✓[/] System executed | system: [cyan]{SystemName}[/], time: [yellow]{ExecutionTimeMs:F2}ms[/], entities: [yellow]{EntitiesProcessed}[/]",
                systemName,
                executionTimeMs,
                entitiesProcessed
            );
        }
    }

    /// <summary>
    ///     Logs workflow status with structured data (replaces LogWorkflowStatus).
    /// </summary>
    public static void LogWorkflow(
        this ILogger logger,
        string workflowName,
        string status,
        Dictionary<string, object>? metadata = null
    )
    {
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object> { ["WorkflowName"] = workflowName, ["Status"] = status }
        );

        string message = "[steelblue1]WF[/] [cyan]{WorkflowName}[/] | status: [cyan]{Status}[/]";
        var args = new List<object> { workflowName, status };

        if (metadata != null)
        {
            foreach (KeyValuePair<string, object> kvp in metadata)
            {
                message += $", {kvp.Key}: [cyan]{{{kvp.Key}}}[/]";
                args.Add(kvp.Value);
            }
        }

        logger.LogInformation(message, args.ToArray());
    }

    /// <summary>
    ///     Begins a timed operation scope that automatically logs elapsed time on disposal.
    /// </summary>
    public static IDisposable BeginTimedOperation(
        this ILogger logger,
        string operationName,
        double warnThresholdMs = 100.0
    )
    {
        return new TimedOperationScope(logger, operationName, warnThresholdMs);
    }

    /// <summary>
    ///     Logs an exception with comprehensive context using structured logging.
    /// </summary>
    public static void LogExceptionWithContext(
        this ILogger logger,
        Exception ex,
        string message,
        Dictionary<string, object>? additionalContext = null
    )
    {
        var contextData = new Dictionary<string, object>
        {
            ["ThreadId"] = Environment.CurrentManagedThreadId,
            ["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            ["MachineName"] = Environment.MachineName,
            ["ExceptionType"] = ex.GetType().Name,
            ["ExceptionSource"] = ex.Source ?? "Unknown",
        };

        if (additionalContext != null)
        {
            foreach (KeyValuePair<string, object> kvp in additionalContext)
            {
                contextData[kvp.Key] = kvp.Value;
            }
        }

        using IDisposable? scope = logger.BeginScope(contextData);
        logger.LogError(ex, message);
    }

    /// <summary>
    ///     Logs asset loading with structured data.
    /// </summary>
    public static void LogAssetLoaded(
        this ILogger logger,
        string assetPath,
        string assetType,
        double loadTimeMs
    )
    {
        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["AssetPath"] = assetPath,
                ["AssetType"] = assetType,
                ["LoadTimeMs"] = loadTimeMs,
            }
        );

        if (loadTimeMs > 50.0)
        {
            logger.LogWarning(
                "[aqua]A[/] [orange3]⚠[/] Asset loaded (slow) | path: [cyan]{AssetPath}[/], type: [cyan]{AssetType}[/], time: [yellow]{LoadTimeMs:F2}ms[/]",
                assetPath,
                assetType,
                loadTimeMs
            );
        }
        else
        {
            logger.LogDebug(
                "[aqua]A[/] [green]✓[/] Asset loaded | path: [cyan]{AssetPath}[/], type: [cyan]{AssetType}[/], time: [yellow]{LoadTimeMs:F2}ms[/]",
                assetPath,
                assetType,
                loadTimeMs
            );
        }
    }

    /// <summary>
    ///     Helper class for timed operation logging.
    /// </summary>
    private sealed class TimedOperationScope : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly IDisposable? _scope;
        private readonly Stopwatch _stopwatch;
        private readonly double _warnThresholdMs;

        public TimedOperationScope(ILogger logger, string operationName, double warnThresholdMs)
        {
            _logger = logger;
            _operationName = operationName;
            _warnThresholdMs = warnThresholdMs;
            _stopwatch = Stopwatch.StartNew();

            _scope = logger.BeginScope(
                new Dictionary<string, object> { ["OperationName"] = operationName }
            );

            logger.LogDebug(
                "[orange3]SYS[/] Operation started | operation: [cyan]{OperationName}[/]",
                operationName
            );
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            double elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

            if (elapsedMs > _warnThresholdMs)
            {
                _logger.LogWarning(
                    "[orange3]SYS[/] [orange3]⚠[/] Operation completed (slow) | operation: [cyan]{OperationName}[/], time: [yellow]{ElapsedMs:F2}ms[/], threshold: [yellow]{ThresholdMs:F2}ms[/]",
                    _operationName,
                    elapsedMs,
                    _warnThresholdMs
                );
            }
            else
            {
                _logger.LogDebug(
                    "[orange3]SYS[/] [green]✓[/] Operation completed | operation: [cyan]{OperationName}[/], time: [yellow]{ElapsedMs:F2}ms[/]",
                    _operationName,
                    elapsedMs
                );
            }

            _scope?.Dispose();
        }
    }
}
