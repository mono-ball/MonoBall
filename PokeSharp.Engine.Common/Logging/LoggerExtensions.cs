using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     Extension methods for enhanced logging capabilities.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    ///     Logs an exception with additional context information.
    ///     Includes thread ID, timestamp, machine name, and exception type.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">The log message.</param>
    /// <param name="args">Message format arguments.</param>
    public static void LogExceptionWithContext(
        this ILogger logger,
        Exception ex,
        string message,
        params object?[] args
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

        string contextString = string.Join(
            ", ",
            contextData.Select(kvp => $"[cyan]{kvp.Key}[/]=[yellow]{kvp.Value}[/]")
        );
        string fullMessage = $"{message} | Context: {contextString}";

        logger.LogError(ex, fullMessage, args);
    }

    /// <summary>
    ///     Logs current memory statistics including GC collection counts.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="includeGcStats">Whether to include GC collection statistics (default: true).</param>
    public static void LogMemoryStats(this ILogger logger, bool includeGcStats = true)
    {
        long totalMemoryBytes = GC.GetTotalMemory(false);
        double totalMemoryMb = totalMemoryBytes / 1024.0 / 1024.0;

        if (includeGcStats)
        {
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            logger.LogInformation(
                "[lightsteelblue1]MEM[/] Memory: [yellow]{MemoryMb:F2}MB[/] | GC Collections - Gen0: [yellow]{Gen0}[/], Gen1: [yellow]{Gen1}[/], Gen2: [yellow]{Gen2}[/]",
                totalMemoryMb,
                gen0,
                gen1,
                gen2
            );
        }
        else
        {
            logger.LogInformation(
                "[lightsteelblue1]MEM[/] Memory: [yellow]{MemoryMb:F2}MB[/]",
                totalMemoryMb
            );
        }
    }

    /// <summary>
    ///     Logs memory statistics with a forced garbage collection.
    ///     Use sparingly as it can impact performance.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public static void LogMemoryStatsWithCollection(this ILogger logger)
    {
        double beforeMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        double afterMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        double freedMemory = beforeMemory - afterMemory;

        logger.LogInformation(
            "[lightsteelblue1]MEM[/] Memory after GC: [yellow]{AfterMb:F2}MB[/] (freed [green]{FreedMb:F2}MB[/] from [yellow]{BeforeMb:F2}MB[/])",
            afterMemory,
            freedMemory,
            beforeMemory
        );
    }

    /// <summary>
    ///     Logs a timed operation, executing the action and measuring elapsed time.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operationName">Name of the operation being timed.</param>
    /// <param name="action">The action to execute and time.</param>
    /// <param name="warnThresholdMs">Log warning if operation exceeds this threshold (default: 100ms).</param>
    public static void LogTimed(
        this ILogger logger,
        string operationName,
        Action action,
        double warnThresholdMs = 100.0
    )
    {
        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            double elapsedMs = sw.Elapsed.TotalMilliseconds;

            if (elapsedMs > warnThresholdMs)
            {
                logger.LogWarning(
                    "[plum1]P[/] [orange3]⚠[/] [cyan]{OperationName}[/] took [yellow]{TimeMs:F2}ms[/] (threshold: [yellow]{ThresholdMs:F2}ms[/])",
                    operationName,
                    elapsedMs,
                    warnThresholdMs
                );
            }
            else
            {
                logger.LogDebug(
                    "[plum1]P[/] [green]✓[/] [cyan]{OperationName}[/] completed in [yellow]{TimeMs:F2}ms[/]",
                    operationName,
                    elapsedMs
                );
            }
        }
    }

    /// <summary>
    ///     Logs a timed operation with a return value, executing the function and measuring elapsed time.
    /// </summary>
    /// <typeparam name="T">Return type of the function.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operationName">Name of the operation being timed.</param>
    /// <param name="func">The function to execute and time.</param>
    /// <param name="warnThresholdMs">Log warning if operation exceeds this threshold (default: 100ms).</param>
    /// <returns>The result of the function.</returns>
    public static T LogTimed<T>(
        this ILogger logger,
        string operationName,
        Func<T> func,
        double warnThresholdMs = 100.0
    )
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return func();
        }
        finally
        {
            sw.Stop();
            double elapsedMs = sw.Elapsed.TotalMilliseconds;

            if (elapsedMs > warnThresholdMs)
            {
                logger.LogWarning(
                    "[plum1]P[/] [orange3]⚠[/] [cyan]{OperationName}[/] took [yellow]{TimeMs:F2}ms[/] (threshold: [yellow]{ThresholdMs:F2}ms[/])",
                    operationName,
                    elapsedMs,
                    warnThresholdMs
                );
            }
            else
            {
                logger.LogDebug(
                    "[plum1]P[/] [green]✓[/] [cyan]{OperationName}[/] completed in [yellow]{TimeMs:F2}ms[/]",
                    operationName,
                    elapsedMs
                );
            }
        }
    }
}
