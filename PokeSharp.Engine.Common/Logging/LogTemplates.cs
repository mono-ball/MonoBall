using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Reusable logging templates with rich Spectre.Console formatting.
///     Provides consistent visual patterns for common logging scenarios.
/// </summary>
public static partial class LogTemplates
{
    private static readonly Dictionary<LogAccent, (string Glyph, string Color)> AccentStyles = new()
    {
        { LogAccent.Initialization, ("▶", "skyblue1") },
        { LogAccent.Asset, ("A", "aqua") },
        { LogAccent.Map, ("M", "springgreen1") },
        { LogAccent.Performance, ("P", "plum1") },
        { LogAccent.Memory, ("MEM", "lightsteelblue1") },
        { LogAccent.Render, ("R", "mediumorchid1") },
        { LogAccent.Entity, ("E", "gold1") },
        { LogAccent.Input, ("I", "deepskyblue3") },
        { LogAccent.Workflow, ("WF", "steelblue1") },
        { LogAccent.System, ("SYS", "orange3") },
        { LogAccent.Script, ("S", "deepskyblue1") },
    };

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
        string detailsFormatted = FormatDetails(details);
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(systemName)}[/] initialized{detailsFormatted}";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Initialization, body))
        );
    }

    /// <summary>
    ///     Logs component initialization with count.
    /// </summary>
    public static void LogComponentInitialized(this ILogger logger, string componentName, int count)
    {
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(componentName)}[/] ready [grey]|[/] [yellow]{count}[/] items";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Initialization, body))
        );
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
        string detailsFormatted = FormatDetails(details);
        string body =
            $"[green]✓[/] Loaded [cyan]{EscapeMarkup(resourceType)}[/] [yellow]'{EscapeMarkup(resourceId)}'[/]{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
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
        string body =
            $"[green]✓[/] [yellow]{EscapeMarkup(entityType)}[/] [grey]#{entityId}[/] from [cyan]'{EscapeMarkup(templateId)}'[/] at [magenta]({x}, {y})[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
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
        string componentList = FormatComponents(components);
        string body =
            $"[green]✓[/] [yellow]{EscapeMarkup(entityType)}[/] [grey]#{entityId}[/]{componentList}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Asset Loading Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs asset loading started.
    /// </summary>
    public static void LogAssetLoadingStarted(this ILogger logger, string assetType, int count)
    {
        string body = $"Loading [yellow]{count}[/] [cyan]{EscapeMarkup(assetType)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
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
        string timeColor = timeMs > 100 ? "yellow" : "green";
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(assetId)}[/] [{timeColor}]{timeMs:F1}ms[/] [grey]({width}x{height}px)[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs a generic asset status line with optional metrics.
    /// </summary>
    public static void LogAssetStatus(
        this ILogger logger,
        string message,
        params (string key, object value)[] details
    )
    {
        string detailsFormatted = FormatDetails(details);
        string body = $"[cyan]{EscapeMarkup(message)}[/]{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs a workflow/process status message with optional details.
    /// </summary>
    public static void LogWorkflowStatus(
        this ILogger logger,
        string message,
        params (string key, object value)[] details
    )
    {
        string detailsFormatted = FormatDetails(details);
        string body = $"[cyan]{EscapeMarkup(message)}[/]{detailsFormatted}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
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
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(mapName)}[/] [grey]{width}x{height}[/] [grey]|[/] [yellow]{tiles}[/] tiles [grey]|[/] [magenta]{objects}[/] objects";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
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
        string fpsColor =
            fps >= 60 ? "green"
            : fps >= 30 ? "yellow"
            : "red";
        string body =
            $"[cyan]{avgMs:F1}ms[/] avg [{fpsColor}]{fps:F1} FPS[/] [grey]|[/] [aqua]{minMs:F1}ms[/] min [orange1]{maxMs:F1}ms[/] peak";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
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
        string avgColor =
            avgMs > 1.67 ? "red"
            : avgMs > 0.84 ? "yellow"
            : "green";
        string peakColor =
            maxMs > 2.0 ? "red1"
            : maxMs > 1.0 ? "orange1"
            : "aqua";
        string systemDisplay = PadRightInvariant(EscapeMarkup(systemName), 22);
        string avgText = avgMs.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6);
        string maxText = maxMs.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(6);
        string callsText = calls.ToString("N0", CultureInfo.InvariantCulture);
        string body =
            $"[cyan]{systemDisplay}[/] [{avgColor}]{avgText}ms[/] avg [{peakColor}]{maxText}ms[/] peak [grey]|[/] [grey]{callsText} calls[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
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
        string memColor =
            memoryMb > 500 ? "red"
            : memoryMb > 250 ? "yellow"
            : "green";
        string body =
            $"[{memColor}]{memoryMb:F1}MB[/] in use [grey]|[/] [grey]G0:[/] [yellow]{gen0}[/] [grey]G1:[/] [yellow]{gen1}[/] [grey]G2:[/] [yellow]{gen2}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Memory, body)));
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
        string body =
            $"[orange3]⚠[/] Slow operation [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] took [red]{timeMs:F1}ms[/] [grey](>{thresholdMs:F1}ms)[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
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

        string message =
            $"{icon} {label} [cyan bold]{EscapeMarkup(systemName)}[/] [{timeColor}]{timeMs:F2}ms[/] "
            + $"[grey]│[/] [{percentColor}]{percent:F1}%[/] of frame";
        logger.LogWarning(LogFormatting.FormatTemplate(message));
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
        string body =
            $"[orange3]⚠[/] [cyan]{EscapeMarkup(resourceType)}[/] [red]'{EscapeMarkup(resourceId)}'[/] not found";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs operation skipped with reason.
    /// </summary>
    public static void LogOperationSkipped(this ILogger logger, string operation, string reason)
    {
        string body =
            $"[orange3]⚠[/] Skipped [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [grey]({EscapeMarkup(reason)})[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
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
        string body =
            $"[red]✗[/] Failed [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [grey]→[/] [yellow]{EscapeMarkup(recovery)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs critical error.
    /// </summary>
    public static void LogCriticalError(this ILogger logger, Exception ex, string operation)
    {
        string body =
            $"[red bold]✗ CRITICAL[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [grey]→[/] [red]{EscapeMarkup(ex.GetType().Name)}[/]: {EscapeMarkup(ex.Message)}";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Scripting & API Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs when a dependent system is not ready or unavailable.
    /// </summary>
    public static void LogSystemUnavailable(
        this ILogger logger,
        string systemName,
        string reason,
        bool isCritical = false
    )
    {
        string label = isCritical
            ? "[red bold]✗ SYSTEM OFFLINE[/]"
            : "[orange3]⚠[/] System not ready";
        string body =
            $"{label} [grey]|[/] [cyan]{EscapeMarkup(systemName)}[/] [grey]→[/] [orange1]{EscapeMarkup(reason)}[/]";
        string formatted = LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body));
        if (isCritical)
        {
            logger.LogError(formatted);
        }
        else
        {
            logger.LogWarning(formatted);
        }
    }

    /// <summary>
    ///     Logs when a system is missing a required dependency.
    /// </summary>
    public static void LogSystemDependencyMissing(
        this ILogger logger,
        string systemName,
        string dependencyName,
        bool isCritical = false
    )
    {
        string severity = isCritical ? "red bold" : "yellow";
        string icon = isCritical ? "[red]✗[/]" : "[orange3]⚠[/]";
        string body =
            $"{icon} [{severity}]Dependency missing[/] [grey]|[/] [cyan]{EscapeMarkup(systemName)}[/] needs [yellow]{EscapeMarkup(dependencyName)}[/]";
        string formatted = LogFormatting.FormatTemplate(WithAccent(LogAccent.System, body));
        if (isCritical)
        {
            logger.LogError(formatted);
        }
        else
        {
            logger.LogWarning(formatted);
        }
    }

    /// <summary>
    ///     Logs when an entity is missing a required component for an operation.
    /// </summary>
    public static void LogEntityMissingComponent(
        this ILogger logger,
        string entityLabel,
        string componentName,
        string context
    )
    {
        string body =
            $"[orange3]⚠[/] [yellow]{EscapeMarkup(entityLabel)}[/] missing [red]{EscapeMarkup(componentName)}[/] [grey]→[/] [orange1]{EscapeMarkup(context)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when an entity is not found for an operation.
    /// </summary>
    public static void LogEntityNotFound(this ILogger logger, string entityLabel, string context)
    {
        string body =
            $"[orange3]⚠[/] [yellow]{EscapeMarkup(entityLabel)}[/] not found [grey]→[/] [orange1]{EscapeMarkup(context)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when an entity operation is invalid or skipped.
    /// </summary>
    public static void LogEntityOperationInvalid(
        this ILogger logger,
        string entityLabel,
        string operation,
        string reason
    )
    {
        string body =
            $"[orange3]⚠[/] [yellow]{EscapeMarkup(entityLabel)}[/] [grey]→[/] [cyan]{EscapeMarkup(operation)}[/] [orange1]skipped[/] [grey]({EscapeMarkup(reason)})[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Entity, body)));
    }

    /// <summary>
    ///     Logs when a template is missing.
    /// </summary>
    public static void LogTemplateMissing(this ILogger logger, string templateId)
    {
        string body =
            $"[red bold]✗ Template missing[/] [grey]|[/] [yellow]'{EscapeMarkup(templateId)}'[/]";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    /// <summary>
    ///     Logs when a template compiler is missing for a given type.
    /// </summary>
    public static void LogTemplateCompilerMissing(this ILogger logger, string entityTypeName)
    {
        string body =
            $"[red bold]✗ Compiler missing[/] [grey]|[/] [yellow]{EscapeMarkup(entityTypeName)}[/]";
        logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    private static string EscapeMarkup(string text)
    {
        return LogFormatting.EscapeMarkup(text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Progress Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs batch operation started.
    /// </summary>
    public static void LogBatchStarted(this ILogger logger, string operation, int total)
    {
        string body =
            $"[cyan]{EscapeMarkup(operation)}[/] started [grey]|[/] [yellow]{total}[/] items";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
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
        string successColor = failed == 0 ? "green" : "yellow";
        string timeColor = failed == 0 ? "aqua" : "yellow";
        string body =
            $"[green]✓ Completed[/] [grey]|[/] [cyan]{EscapeMarkup(operation)}[/] [{successColor}]{successful} OK[/] [grey]{failed} failed[/] [grey]|[/] [{timeColor}]{timeMs:F1}ms[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Workflow, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Input/Interaction Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs input controls hint.
    /// </summary>
    public static void LogControlsHint(this ILogger logger, string hint)
    {
        string body = $"Controls [grey]|[/] [cyan]{EscapeMarkup(hint)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Input, body)));
    }

    /// <summary>
    ///     Logs zoom change.
    /// </summary>
    public static void LogZoomChanged(this ILogger logger, string preset, float zoom)
    {
        string body = $"[cyan]{EscapeMarkup(preset)}[/] zoom [yellow]{zoom:F1}x[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Input, body)));
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
        string callsText = calls.ToString("N0", CultureInfo.InvariantCulture);
        string body =
            $"[cyan bold]{totalEntities}[/] entities [grey]|[/] [yellow]{tiles}[/] tiles [grey]|[/] [magenta]{sprites}[/] sprites [grey]|[/] [grey]{callsText} calls[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Diagnostic Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs diagnostic header.
    /// </summary>
    public static void LogDiagnosticHeader(this ILogger logger, string title)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate(
                "[blue bold]╔══════════════════════════════════════════╗[/]"
            )
        );
        string headerLine = LogFormatting.FormatTemplate(
            $"[blue bold]║[/]  [cyan bold]{title, -38}[/]  [blue bold]║[/]"
        );
        logger.LogInformation(headerLine);
        logger.LogInformation(
            LogFormatting.FormatTemplate(
                "[blue bold]╚══════════════════════════════════════════╝[/]"
            )
        );
    }

    /// <summary>
    ///     Logs diagnostic info line.
    /// </summary>
    public static void LogDiagnosticInfo(this ILogger logger, string label, object value)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate($"[grey]→[/] [cyan]{label}:[/] [yellow]{value}[/]")
        );
    }

    /// <summary>
    ///     Logs diagnostic separator.
    /// </summary>
    public static void LogDiagnosticSeparator(this ILogger logger)
    {
        logger.LogInformation(
            LogFormatting.FormatTemplate("[grey]═══════════════════════════════════════════[/]")
        );
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendering & Sprite Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs render tile size change.
    /// </summary>
    public static void LogRenderTileSizeSet(this ILogger logger, int tileSize)
    {
        string body = $"[green]✓[/] Tile size set to [yellow]{tileSize}px[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    /// <summary>
    ///     Logs sprite texture loader registration.
    /// </summary>
    public static void LogSpriteLoaderRegistered(this ILogger logger)
    {
        string body = "[green]✓ Sprite texture loader[/] registered for lazy loading";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    /// <summary>
    ///     Logs detailed profiling state change.
    /// </summary>
    public static void LogDetailedProfilingChanged(this ILogger logger, bool enabled)
    {
        string state = enabled ? "[green]enabled[/]" : "[grey]disabled[/]";
        string body = $"[cyan]Detailed profiling[/] {state}";
        logger.LogInformation(
            LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body))
        );
    }

    /// <summary>
    ///     Logs sprite texture loaded with format.
    /// </summary>
    public static void LogSpriteTextureLoaded(this ILogger logger, string textureKey, object format)
    {
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(textureKey)}[/] loaded [grey]|[/] [yellow]{format}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs lazy-loaded sprite texture with format.
    /// </summary>
    public static void LogSpriteTextureLazyLoaded(
        this ILogger logger,
        string textureKey,
        object format
    )
    {
        string body =
            $"[green]✓ Lazy-loaded[/] [cyan]{EscapeMarkup(textureKey)}[/] [grey]|[/] [yellow]{format}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture registered.
    /// </summary>
    public static void LogSpriteTextureRegisteredDebug(this ILogger logger, string textureKey)
    {
        string body = $"[green]✓ Registered[/] [cyan]{EscapeMarkup(textureKey)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture unregistered.
    /// </summary>
    public static void LogSpriteTextureUnregistered(this ILogger logger, string textureKey)
    {
        string body = $"[yellow]Unregistered[/] [cyan]{EscapeMarkup(textureKey)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite manifest loaded.
    /// </summary>
    public static void LogSpriteManifestsFound(this ILogger logger, int count)
    {
        string body = $"Found [yellow]{count}[/] sprite manifests to load";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite loading progress.
    /// </summary>
    public static void LogSpriteLoadingProgress(
        this ILogger logger,
        int current,
        int total,
        string category,
        string name
    )
    {
        string body =
            $"Loading sprite [yellow]{current}[/][grey]/[/][yellow]{total}[/] [grey]|[/] [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(name)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture with dimensions.
    /// </summary>
    public static void LogSpriteTextureWithDimensions(
        this ILogger logger,
        string category,
        string name,
        object format,
        int width,
        int height
    )
    {
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(name)}[/] [yellow]{format}[/] [grey]({width}x{height}px)[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite loading complete with statistics.
    /// </summary>
    public static void LogSpriteLoadingComplete(this ILogger logger, int loaded, int failed)
    {
        string successColor = failed == 0 ? "green" : "yellow";
        string body =
            $"[green]✓ Sprite loading complete[/] [grey]|[/] [{successColor}]{loaded} loaded[/] [grey]{failed} failed[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprites loaded for map.
    /// </summary>
    public static void LogSpritesLoadedForMap(
        this ILogger logger,
        int loadedCount,
        int mapId,
        int skippedCount
    )
    {
        string body =
            $"[green]✓ Loaded[/] [yellow]{loadedCount}[/] new sprites for map [cyan]{mapId}[/] [grey]|[/] [grey]{skippedCount} already loaded[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs sprites unloaded for map.
    /// </summary>
    public static void LogSpritesUnloadedForMap(this ILogger logger, int count, int mapId)
    {
        string body = $"Unloaded [yellow]{count}[/] sprites for map [cyan]{mapId}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs texture loaded with timing (AssetManager specific).
    /// </summary>
    public static void LogTextureLoaded(
        this ILogger logger,
        string textureId,
        double timeMs,
        int width,
        int height
    )
    {
        string timeColor = timeMs > 100 ? "yellow" : "green";
        string body =
            $"[green]✓[/] [cyan]{EscapeMarkup(textureId)}[/] [{timeColor}]{timeMs:F1}ms[/] [grey]({width}x{height}px)[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs slow texture load warning.
    /// </summary>
    public static void LogSlowTextureLoad(this ILogger logger, string textureId, double timeMs)
    {
        string body =
            $"[orange3]⚠[/] Slow load [cyan]{EscapeMarkup(textureId)}[/] [red]{timeMs:F1}ms[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
    }

    /// <summary>
    ///     Logs sprite sheet not found warning.
    /// </summary>
    public static void LogSpriteSheetNotFound(this ILogger logger, string category, string name)
    {
        string body =
            $"[orange3]⚠[/] Sprite sheet not found for [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(name)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture loaded on-demand (debug level).
    /// </summary>
    public static void LogSpriteLoadedOnDemand(this ILogger logger, string textureKey)
    {
        string body = $"Loaded sprite sheet on-demand: [cyan]{EscapeMarkup(textureKey)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprites required for map (debug level).
    /// </summary>
    public static void LogSpritesRequiredForMap(this ILogger logger, int mapId, int count)
    {
        string body =
            $"Loading sprites for map [yellow]{mapId}[/] [grey]|[/] [grey]{count} sprites required[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs invalid sprite ID format warning.
    /// </summary>
    public static void LogInvalidSpriteIdFormat(this ILogger logger, string spriteId)
    {
        string body = $"[orange3]⚠[/] Invalid sprite ID format: [red]{EscapeMarkup(spriteId)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture file not found warning.
    /// </summary>
    public static void LogSpriteTextureFileNotFound(
        this ILogger logger,
        string category,
        string spriteName
    )
    {
        string body =
            $"[orange3]⚠[/] Sprite texture file not found for [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(spriteName)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite texture loaded (debug level).
    /// </summary>
    public static void LogSpriteTextureLoadedDebug(this ILogger logger, string textureKey)
    {
        string body = $"Loaded sprite texture: [cyan]{EscapeMarkup(textureKey)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs no sprites tracked for map (debug level).
    /// </summary>
    public static void LogNoSpritesForMap(this ILogger logger, int mapId)
    {
        string body = $"No sprites tracked for map [yellow]{mapId}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs render breakdown with detailed timings (debug level).
    /// </summary>
    public static void LogRenderBreakdown(
        this ILogger logger,
        double setupMs,
        double beginMs,
        double tilesMs,
        double spritesMs,
        double endMs
    )
    {
        string body =
            $"[cyan]Setup[/]=[yellow]{setupMs:F2}ms[/] [cyan]Begin[/]=[yellow]{beginMs:F2}ms[/] [cyan]Tiles[/]=[yellow]{tilesMs:F2}ms[/] [cyan]Sprites[/]=[yellow]{spritesMs:F2}ms[/] [cyan]End[/]=[yellow]{endMs:F2}ms[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Performance, body)));
    }

    /// <summary>
    ///     Logs image layers rendered count (debug level).
    /// </summary>
    public static void LogImageLayersRendered(this ILogger logger, int count)
    {
        string body = $"[cyan]Image Layers Rendered:[/] [yellow]{count}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    /// <summary>
    ///     Logs sprite manifest not found warning.
    /// </summary>
    public static void LogSpriteManifestNotFound(this ILogger logger, string spriteName)
    {
        string body =
            $"[orange3]⚠[/] Sprite manifest not found for [red]{EscapeMarkup(spriteName)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs failed to load sprite manifest warning.
    /// </summary>
    public static void LogSpriteManifestLoadFailedForAnimation(
        this ILogger logger,
        Exception ex,
        string category,
        string spriteName
    )
    {
        string body =
            $"[orange3]⚠[/] Failed to load sprite manifest for [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(spriteName)}[/]";
        logger.LogWarning(ex, LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs animation not found in sprite warning.
    /// </summary>
    public static void LogAnimationNotFoundInSprite(
        this ILogger logger,
        string animationName,
        string category,
        string spriteName
    )
    {
        string body =
            $"[orange3]⚠[/] Animation [red]'{EscapeMarkup(animationName)}'[/] not found in sprite [cyan]{EscapeMarkup(category)}[/][grey]/[/][cyan]{EscapeMarkup(spriteName)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Render, body)));
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════

    private static string WithAccent(LogAccent accent, string message)
    {
        (string, string) style = AccentStyles.TryGetValue(
            accent,
            out (string Glyph, string Color) value
        )
            ? value
            : ("•", "grey");
        string prefix = $"[{style.Item2}]{style.Item1.PadRight(3)}[/]";
        return $"{prefix} {message}";
    }

    private static string PadRightInvariant(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new string(' ', width);
        }

        if (value.Length >= width)
        {
            return value.Length == width ? value : value[..width];
        }

        return value + new string(' ', width - value.Length);
    }

    private static string FormatDetails(params (string key, object value)[] details)
    {
        if (details == null || details.Length == 0)
        {
            return "";
        }

        string formatted = string.Join(
            ", ",
            details.Select(d =>
                $"[grey]{EscapeMarkup(d.key)}:[/] [cyan]{EscapeMarkup(d.value?.ToString() ?? "")}[/]"
            )
        );
        return $" [grey]|[/] {formatted}";
    }

    private static string FormatComponents(params (string key, object value)[] components)
    {
        if (components == null || components.Length == 0)
        {
            return "";
        }

        string formatted = string.Join(
            "[grey],[/] ",
            components.Select(c => $"[cyan]{EscapeMarkup(c.key)}[/]")
        );
        return $" [grey][[{formatted}]][/]";
    }

    // ═══════════════════════════════════════════════════════════════
    // Data Loading Templates
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     Logs game data loading started.
    /// </summary>
    public static void LogGameDataLoadingStarted(this ILogger logger, string path)
    {
        string body = $"Loading game data from [cyan]{EscapeMarkup(path)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs game data loading completed with summary.
    /// </summary>
    public static void LogGameDataLoaded(this ILogger logger, Dictionary<string, int> loadedCounts)
    {
        IEnumerable<string> parts = loadedCounts.Select(kvp =>
            $"[cyan]{EscapeMarkup(kvp.Key)}:[/] [yellow]{kvp.Value}[/]"
        );
        string summary = string.Join("[grey], [/]", parts);
        string body = $"[green]✓ Game data loaded:[/] {summary}";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs directory not found warning.
    /// </summary>
    public static void LogDirectoryNotFound(this ILogger logger, string directoryType, string path)
    {
        string body =
            $"[orange3]⚠[/] [cyan]{EscapeMarkup(directoryType)}[/] directory not found: [yellow]{EscapeMarkup(path)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs NPCs loaded in bulk.
    /// </summary>
    public static void LogNpcsLoaded(this ILogger logger, int count)
    {
        string body = $"[green]✓ Loaded[/] [yellow]{count}[/] [cyan]NPCs[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs individual NPC loaded (debug level).
    /// </summary>
    public static void LogNpcLoaded(this ILogger logger, string npcId)
    {
        string body = $"Loaded NPC: [yellow]{EscapeMarkup(npcId)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs NPC load failed.
    /// </summary>
    public static void LogNpcLoadFailed(this ILogger logger, string file, Exception? ex = null)
    {
        string body = $"[red]✗ Error loading NPC from[/] [cyan]{EscapeMarkup(file)}[/]";
        if (ex != null)
        {
            logger.LogError(ex, LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
        else
        {
            logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
    }

    /// <summary>
    ///     Logs NPC deserialization failed.
    /// </summary>
    public static void LogNpcDeserializeFailed(this ILogger logger, string file)
    {
        string body = $"[orange3]⚠[/] Failed to deserialize NPC from [cyan]{EscapeMarkup(file)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs NPC missing required field.
    /// </summary>
    public static void LogNpcMissingField(this ILogger logger, string file, string fieldName)
    {
        string body =
            $"[orange3]⚠[/] NPC in [cyan]{EscapeMarkup(file)}[/] missing [red]{fieldName}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs trainers loaded in bulk.
    /// </summary>
    public static void LogTrainersLoaded(this ILogger logger, int count)
    {
        string body = $"[green]✓ Loaded[/] [yellow]{count}[/] [cyan]trainers[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs individual trainer loaded (debug level).
    /// </summary>
    public static void LogTrainerLoaded(this ILogger logger, string trainerId)
    {
        string body = $"Loaded Trainer: [yellow]{EscapeMarkup(trainerId)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs trainer load failed.
    /// </summary>
    public static void LogTrainerLoadFailed(this ILogger logger, string file, Exception? ex = null)
    {
        string body = $"[red]✗ Error loading Trainer from[/] [cyan]{EscapeMarkup(file)}[/]";
        if (ex != null)
        {
            logger.LogError(ex, LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
        else
        {
            logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
    }

    /// <summary>
    ///     Logs trainer deserialization failed.
    /// </summary>
    public static void LogTrainerDeserializeFailed(this ILogger logger, string file)
    {
        string body =
            $"[orange3]⚠[/] Failed to deserialize Trainer from [cyan]{EscapeMarkup(file)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs trainer missing required field.
    /// </summary>
    public static void LogTrainerMissingField(this ILogger logger, string file, string fieldName)
    {
        string body =
            $"[orange3]⚠[/] Trainer in [cyan]{EscapeMarkup(file)}[/] missing [red]{fieldName}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs maps loaded in bulk.
    /// </summary>
    public static void LogMapsLoaded(this ILogger logger, int count)
    {
        string body = $"[green]✓ Loaded[/] [yellow]{count}[/] [cyan]maps[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs individual map loaded (debug level).
    /// </summary>
    public static void LogMapLoadedFromFile(
        this ILogger logger,
        string mapId,
        string displayName,
        string relativePath
    )
    {
        string body =
            $"Loaded Map: [yellow]{EscapeMarkup(mapId)}[/] [grey]([/][cyan]{EscapeMarkup(displayName)}[/][grey]) from[/] [cyan]{EscapeMarkup(relativePath)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs map override (mod overriding base game map).
    /// </summary>
    public static void LogMapOverridden(this ILogger logger, string mapId, string displayName)
    {
        string body =
            $"[orange3]⚠[/] Overriding Map: [cyan]{EscapeMarkup(mapId)}[/] [grey]([/][yellow]{EscapeMarkup(displayName)}[/][grey]) with mod data[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs map load failed.
    /// </summary>
    public static void LogMapLoadFailed(this ILogger logger, string file, Exception? ex = null)
    {
        string body = $"[red]✗ Error loading Map from[/] [cyan]{EscapeMarkup(file)}[/]";
        if (ex != null)
        {
            logger.LogError(ex, LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
        }
        else
        {
            logger.LogError(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
        }
    }

    /// <summary>
    ///     Logs Tiled JSON parse failed.
    /// </summary>
    public static void LogTiledParseFailed(this ILogger logger, string file)
    {
        string body =
            $"[orange3]⚠[/] Failed to parse Tiled JSON from [cyan]{EscapeMarkup(file)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    /// <summary>
    ///     Logs sprite manifests scanning started.
    /// </summary>
    public static void LogSpriteScanningStarted(this ILogger logger, string path)
    {
        string body = $"Scanning for sprite manifests in [cyan]{EscapeMarkup(path)}[/]";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprites loaded in bulk.
    /// </summary>
    public static void LogSpritesLoaded(this ILogger logger, int count)
    {
        string body =
            $"[green]✓ Loaded[/] [yellow]{count}[/] [cyan]total sprites[/] from all categories";
        logger.LogInformation(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite registered (debug level).
    /// </summary>
    public static void LogSpriteRegistered(this ILogger logger, string lookupKey, string path)
    {
        string body =
            $"Registered sprite: [yellow]{EscapeMarkup(lookupKey)}[/] [grey]→[/] [cyan]{EscapeMarkup(path)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite manifest load failed.
    /// </summary>
    public static void LogSpriteManifestLoadFailed(
        this ILogger logger,
        string path,
        Exception? ex = null
    )
    {
        string body = $"[orange3]⚠[/] Failed to load manifest from [cyan]{EscapeMarkup(path)}[/]";
        if (ex != null)
        {
            logger.LogWarning(ex, LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
        else
        {
            logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
        }
    }

    /// <summary>
    ///     Logs sprite not found warning.
    /// </summary>
    public static void LogSpriteNotFound(this ILogger logger, string spriteName)
    {
        string body =
            $"[orange3]⚠[/] Sprite [red]{EscapeMarkup(spriteName)}[/] not found in manifest";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite path not found warning.
    /// </summary>
    public static void LogSpritePathNotFound(this ILogger logger, string lookupKey)
    {
        string body = $"[orange3]⚠[/] Sprite path not found for [red]{EscapeMarkup(lookupKey)}[/]";
        logger.LogWarning(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs sprite cache cleared.
    /// </summary>
    public static void LogSpriteCacheCleared(this ILogger logger)
    {
        string body = "Sprite manifest cache cleared";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs specific sprite cleared from cache.
    /// </summary>
    public static void LogSpriteClearedFromCache(this ILogger logger, string spriteKey)
    {
        string body = $"Cleared sprite manifest from cache: [yellow]{EscapeMarkup(spriteKey)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs NPC definition cached.
    /// </summary>
    public static void LogNpcCached(this ILogger logger, string npcId)
    {
        string body = $"Cached NPC definition: [yellow]{EscapeMarkup(npcId)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs trainer definition cached.
    /// </summary>
    public static void LogTrainerCached(this ILogger logger, string trainerId)
    {
        string body = $"Cached Trainer definition: [yellow]{EscapeMarkup(trainerId)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Asset, body)));
    }

    /// <summary>
    ///     Logs map definition cached.
    /// </summary>
    public static void LogMapCached(this ILogger logger, string mapId)
    {
        string body = $"Cached map definition: [yellow]{EscapeMarkup(mapId)}[/]";
        logger.LogDebug(LogFormatting.FormatTemplate(WithAccent(LogAccent.Map, body)));
    }

    private enum LogAccent
    {
        Initialization,
        Asset,
        Map,
        Performance,
        Memory,
        Render,
        Entity,
        Input,
        Workflow,
        System,
        Script,
    }
}
