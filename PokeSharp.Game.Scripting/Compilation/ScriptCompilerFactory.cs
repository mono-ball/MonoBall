using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Scripting.Compilation;

/// <summary>
///     Factory for creating IScriptCompiler instances.
///     Provides a centralized way to configure and instantiate script compilers.
/// </summary>
public static class ScriptCompilerFactory
{
    /// <summary>
    ///     Create a default RoslynScriptCompiler instance.
    /// </summary>
    /// <param name="logger">Logger for compiler diagnostics.</param>
    /// <returns>Configured IScriptCompiler instance.</returns>
    public static IScriptCompiler CreateRoslynCompiler(ILogger<RoslynScriptCompiler> logger)
    {
        return new RoslynScriptCompiler(logger);
    }

    /// <summary>
    ///     Create a default RoslynScriptCompiler with a generic logger.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <returns>Configured IScriptCompiler instance.</returns>
    public static IScriptCompiler CreateRoslynCompiler(ILoggerFactory loggerFactory)
    {
        ILogger<RoslynScriptCompiler> logger = loggerFactory.CreateLogger<RoslynScriptCompiler>();
        return new RoslynScriptCompiler(logger);
    }
}
