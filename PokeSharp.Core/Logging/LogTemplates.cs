using Microsoft.Extensions.Logging;

namespace PokeSharp.Core.Logging;

/// <summary>
///     Reusable logging templates with rich Spectre.Console formatting.
///     Provides consistent visual patterns for common logging scenarios.
/// </summary>
public static class LogTemplates
{
    // ═══════════════════════════════════════════════════════════════
    // Initialization Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs system initialization with success indicator.
    /// </summary>
    public static void LogSystemInitialized(
        this ILogger logger,
        string systemName,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var message = $"[green]{systemName} initialized{detailsFormatted}[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs component initialization with count.
    /// </summary>
    public static void LogComponentInitialized(this ILogger logger, string componentName, int count)
    {
        var message = $"[green]{componentName} initialized with [cyan]{count}[/] items[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs resource loaded successfully.
    /// </summary>
    public static void LogResourceLoaded(
        this ILogger logger,
        string resourceType,
        string resourceId,
        params (string key, object value)[] details
    )
    {
        var detailsFormatted = FormatDetails(details);
        var message = $"[green]Loaded {resourceType} '[cyan]{resourceId}[/]'{detailsFormatted}[/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Entity Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs entity spawned from template.
    /// </summary>
    public static void LogEntitySpawned(
        this ILogger logger,
        string entityType,
        int entityId,
        string templateId,
        int x,
        int y
    )
    {
        var message =
            $"[green]Spawned [yellow]{entityType}[/] [dim]#{entityId}[/] from template '[cyan]{templateId}[/]' at [magenta]({x}, {y})[/][/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs entity created.
    /// </summary>
    public static void LogEntityCreated(
        this ILogger logger,
        string entityType,
        int entityId,
        params (string key, object value)[] components
    )
    {
        var componentList = FormatComponents(components);
        var message =
            $"[green]Created [yellow]{entityType}[/] [dim]#{entityId}[/]{componentList}[/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Asset Loading Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs asset loading started.
    /// </summary>
    public static void LogAssetLoadingStarted(this ILogger logger, string assetType, int count)
    {
        var message = $"[blue]→[/] Loading [cyan]{count}[/] {assetType}...";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs asset loaded with timing.
    /// </summary>
    public static void LogAssetLoadedWithTiming(
        this ILogger logger,
        string assetId,
        double timeMs,
        int width,
        int height
    )
    {
        var timeColor = timeMs > 100 ? "yellow" : "green";
        var message =
            $"[cyan]{assetId}[/] [{timeColor}]{timeMs:F1}ms[/] [dim]({width}x{height}px)[/]";
        logger.LogDebug(message);
    }

    /// <summary>
    ///     Logs map loaded with statistics.
    /// </summary>
    public static void LogMapLoaded(
        this ILogger logger,
        string mapName,
        int width,
        int height,
        int tiles,
        int objects
    )
    {
        var message =
            $"[green]Map '[cyan]{mapName}[/]' loaded [dim]{width}x{height}[/] | [yellow]{tiles}[/] tiles, [magenta]{objects}[/] objects[/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Performance Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs frame performance statistics.
    /// </summary>
    public static void LogFramePerformance(
        this ILogger logger,
        float avgMs,
        float fps,
        float minMs,
        float maxMs
    )
    {
        var fpsColor =
            fps >= 60 ? "green"
            : fps >= 30 ? "yellow"
            : "red";
        var message =
            $"[blue]⚡[/] Performance: [cyan]{avgMs:F1}ms[/] [{fpsColor}]{fps:F1} FPS[/] [dim]| Min: {minMs:F1}ms | Max: {maxMs:F1}ms[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs system performance statistics.
    /// </summary>
    public static void LogSystemPerformance(
        this ILogger logger,
        string systemName,
        double avgMs,
        double maxMs,
        long calls
    )
    {
        var avgColor =
            avgMs > 1.67 ? "red"
            : avgMs > 0.84 ? "yellow"
            : "green";
        var message =
            $"[blue]│[/] [cyan]{systemName, -25}[/] [{avgColor}]{avgMs, 6:F2}ms[/] [dim]avg[/] [yellow]{maxMs, 6:F2}ms[/] [dim]max[/] [grey]│[/] [dim]{calls} calls[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs memory statistics with GC info.
    /// </summary>
    public static void LogMemoryStatistics(
        this ILogger logger,
        double memoryMb,
        int gen0,
        int gen1,
        int gen2
    )
    {
        var memColor =
            memoryMb > 500 ? "red"
            : memoryMb > 250 ? "yellow"
            : "green";
        var message =
            $"[blue]Memory: [{memColor}]{memoryMb:F1}MB[/] [dim]|[/] GC: [grey]G0:{gen0}[/] [grey]G1:{gen1}[/] [grey]G2:{gen2}[/][/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Warning Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs slow operation warning.
    /// </summary>
    public static void LogSlowOperation(
        this ILogger logger,
        string operation,
        double timeMs,
        double thresholdMs
    )
    {
        var message =
            $"[yellow]Slow operation: [cyan]{operation}[/] took [red]{timeMs:F1}ms[/] [dim](threshold: {thresholdMs:F1}ms)[/][/]";
        logger.LogWarning(message);
    }

    /// <summary>
    ///     Logs slow system warning with enhanced formatting.
    /// </summary>
    public static void LogSlowSystem(
        this ILogger logger,
        string systemName,
        double timeMs,
        double percent
    )
    {
        // Color code based on severity levels:
        // >50% = Critical (red bold, double warning)
        // >20% = High (red, double warning)
        // >10% = Medium (orange/yellow, single warning)
        string icon,
            timeColor,
            percentColor,
            label;

        if (percent > 50)
        {
            icon = "[red bold on yellow]!!![/]";
            timeColor = "red bold";
            percentColor = "red bold";
            label = "[red bold]CRITICAL:[/]";
        }
        else if (percent > 20)
        {
            icon = "[red bold]!![/]";
            timeColor = "red";
            percentColor = "red bold";
            label = "[red]SLOW:[/]";
        }
        else
        {
            icon = "[yellow]![/]";
            timeColor = "yellow";
            percentColor = "orange1";
            label = "[yellow]Slow:[/]";
        }

        var message =
            $"{icon} {label} [cyan bold]{systemName}[/] [{timeColor}]{timeMs:F2}ms[/] [dim]│[/] [{percentColor}]{percent:F1}%[/] [dim]of frame[/]";
        logger.LogWarning(message);
    }

    /// <summary>
    ///     Logs resource not found warning.
    /// </summary>
    public static void LogResourceNotFound(
        this ILogger logger,
        string resourceType,
        string resourceId
    )
    {
        var message = $"[yellow]{resourceType} '[red]{resourceId}[/]' not found, skipping[/]";
        logger.LogWarning(message);
    }

    /// <summary>
    ///     Logs operation skipped with reason.
    /// </summary>
    public static void LogOperationSkipped(this ILogger logger, string operation, string reason)
    {
        var message = $"[yellow]Skipped: [cyan]{operation}[/] [dim]({reason})[/][/]";
        logger.LogWarning(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Error Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs operation failed with recovery action.
    /// </summary>
    public static void LogOperationFailedWithRecovery(
        this ILogger logger,
        string operation,
        string recovery
    )
    {
        var message = $"[red]✗[/] Failed: [cyan]{operation}[/] [dim]→[/] [yellow]{recovery}[/]";
        logger.LogWarning(message);
    }

    /// <summary>
    ///     Logs critical error.
    /// </summary>
    public static void LogCriticalError(this ILogger logger, Exception ex, string operation)
    {
        var message =
            $"[red bold]CRITICAL: [cyan]{operation}[/] failed [dim]→[/] [red]{ex.GetType().Name}: {EscapeMarkup(ex.Message)}[/][/]";
        logger.LogError(message);
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    // ═══════════════════════════════════════════════════════════════
    // Progress Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs batch operation started.
    /// </summary>
    public static void LogBatchStarted(this ILogger logger, string operation, int total)
    {
        var message = $"[blue]▶[/] Starting: [cyan]{operation}[/] [dim]({total} items)[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs batch operation completed.
    /// </summary>
    public static void LogBatchCompleted(
        this ILogger logger,
        string operation,
        int successful,
        int failed,
        double timeMs
    )
    {
        var successColor = failed == 0 ? "green" : "yellow";
        var message =
            $"[green]Completed: [cyan]{operation}[/] [{successColor}]{successful} OK[/] [dim]{failed} failed[/] [grey]in {timeMs:F1}ms[/][/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Input/Interaction Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs input controls hint.
    /// </summary>
    public static void LogControlsHint(this ILogger logger, string hint)
    {
        var message = $"[grey]Controls: [dim]{hint}[/][/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs zoom change.
    /// </summary>
    public static void LogZoomChanged(this ILogger logger, string preset, float zoom)
    {
        var message = $"[blue]Zoom: [cyan]{preset}[/] [yellow]{zoom:F1}x[/][/]";
        logger.LogDebug(message);
    }

    /// <summary>
    ///     Logs render statistics.
    /// </summary>
    public static void LogRenderStats(
        this ILogger logger,
        int totalEntities,
        int tiles,
        int sprites,
        ulong calls
    )
    {
        var message =
            $"[blue]Rendered [cyan bold]{totalEntities}[/] entities [dim]│[/] [yellow]{tiles}[/] tiles [dim]+[/] [magenta]{sprites}[/] sprites [dim]│[/] [grey]{calls} calls[/][/]";
        logger.LogInformation(message);
    }

    // ═══════════════════════════════════════════════════════════════
    // Diagnostic Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs diagnostic header.
    /// </summary>
    public static void LogDiagnosticHeader(this ILogger logger, string title)
    {
        logger.LogInformation("[blue bold]╔══════════════════════════════════════════╗[/]");
        var headerLine = $"[blue bold]║[/]  [cyan bold]{title, -38}[/]  [blue bold]║[/]";
        logger.LogInformation(headerLine);
        logger.LogInformation("[blue bold]╚══════════════════════════════════════════╝[/]");
    }

    /// <summary>
    ///     Logs diagnostic info line.
    /// </summary>
    public static void LogDiagnosticInfo(this ILogger logger, string label, object value)
    {
        var message = $"[grey]→[/] [cyan]{label}:[/] [yellow]{value}[/]";
        logger.LogInformation(message);
    }

    /// <summary>
    ///     Logs diagnostic separator.
    /// </summary>
    public static void LogDiagnosticSeparator(this ILogger logger)
    {
        logger.LogInformation("[dim]═══════════════════════════════════════════[/]");
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════

    private static string FormatDetails(params (string key, object value)[] details)
    {
        if (details == null || details.Length == 0)
            return "";

        var formatted = string.Join(
            ", ",
            details.Select(d =>
                $"[dim]{EscapeMarkup(d.key)}:[/] [grey]{EscapeMarkup(d.value.ToString() ?? "")}[/]"
            )
        );
        return $" [dim]|[/] {formatted}";
    }

    private static string FormatComponents(params (string key, object value)[] components)
    {
        if (components == null || components.Length == 0)
            return "";

        var formatted = string.Join(
            "[dim],[/] ",
            components.Select(c => $"[grey]{EscapeMarkup(c.key)}[/]")
        );
        return $" [dim][[{formatted}]][/]";
    }
}
