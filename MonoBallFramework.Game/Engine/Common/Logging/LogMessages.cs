using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Common.Logging;

/// <summary>
///     High-performance logging messages using source generators.
///     These methods generate zero-allocation logging code at compile time.
/// </summary>
public static partial class LogMessages
{
    // Movement System Messages
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Movement blocked: out of bounds ({X}, {Y}) for map {MapId}"
    )]
    public static partial void LogMovementBlocked(this ILogger logger, int x, int y, GameMapId mapId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Jump blocked: landing out of bounds ({X}, {Y})"
    )]
    public static partial void LogJumpBlocked(this ILogger logger, int x, int y);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Jump blocked: landing position blocked ({X}, {Y})"
    )]
    public static partial void LogJumpLandingBlocked(this ILogger logger, int x, int y);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Jump: ({StartX}, {StartY}) -> ({EndX}, {EndY}) direction: {Direction}"
    )]
    public static partial void LogJump(
        this ILogger logger,
        int startX,
        int startY,
        int endX,
        int endY,
        string direction
    );

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Trace,
        Message = "Movement blocked by collision at ({X}, {Y}) from direction {Direction}"
    )]
    public static partial void LogCollisionBlocked(
        this ILogger logger,
        int x,
        int y,
        string direction
    );

    // System Processing Messages
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Processing {EntityCount} entities in {SystemName}"
    )]
    public static partial void LogEntityProcessing(
        this ILogger logger,
        int entityCount,
        string systemName
    );

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Indexed {Count} static tiles into spatial hash"
    )]
    public static partial void LogSpatialHashIndexed(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Processing {Count} animated tiles"
    )]
    public static partial void LogAnimatedTilesProcessed(this ILogger logger, int count);

    // Performance Messages
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Warning,
        Message = "Slow frame: {FrameTimeMs:F2}ms (target: {TargetMs:F2}ms)"
    )]
    public static partial void LogSlowFrame(this ILogger logger, float frameTimeMs, float targetMs);

    // Note: LogSlowSystem is now a template method in LogTemplates.cs for rich formatting

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Performance: Avg frame time: {AvgMs:F2}ms ({Fps:F1} FPS) | Min: {MinMs:F2}ms | Max: {MaxMs:F2}ms"
    )]
    public static partial void LogFrameTimeStats(
        this ILogger logger,
        float avgMs,
        float fps,
        float minMs,
        float maxMs
    );

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "System {SystemName} - Avg: {AvgMs:F2}ms | Max: {MaxMs:F2}ms | Calls: {UpdateCount}"
    )]
    public static partial void LogSystemStats(
        this ILogger logger,
        string systemName,
        double avgMs,
        double maxMs,
        long updateCount
    );

    // Asset Loading Messages
    // Note: LogTextureLoaded and LogSlowTextureLoad moved to LogTemplates.cs for rich formatting

    // Memory Messages
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Information,
        Message = "Memory: {MemoryMb:F2}MB | GC Collections - Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}"
    )]
    public static partial void LogMemoryWithGc(
        this ILogger logger,
        double memoryMb,
        int gen0,
        int gen1,
        int gen2
    );

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Warning,
        Message = "High memory usage: {MemoryMb:F2}MB (threshold: {ThresholdMb:F2}MB)"
    )]
    public static partial void LogHighMemoryUsage(
        this ILogger logger,
        double memoryMb,
        double thresholdMb
    );

    // Initialization Messages
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "Initializing {Count} systems"
    )]
    public static partial void LogSystemsInitializing(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "Initializing system: {SystemName}"
    )]
    public static partial void LogSystemInitializing(this ILogger logger, string systemName);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "All systems initialized successfully"
    )]
    public static partial void LogSystemsInitialized(this ILogger logger);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Debug,
        Message = "Registered system: {SystemName} (Priority: {Priority})"
    )]
    public static partial void LogSystemRegistered(
        this ILogger logger,
        string systemName,
        int priority
    );
}
