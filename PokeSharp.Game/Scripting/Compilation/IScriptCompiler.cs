using PokeSharp.Game.Scripting.HotReload.Compilation;

namespace PokeSharp.Game.Scripting.Compilation;

/// <summary>
///     Interface for script compilation (implemented by RoslynScriptCompiler).
/// </summary>
public interface IScriptCompiler
{
    /// <summary>
    ///     Compile a C# script file asynchronously.
    /// </summary>
    /// <param name="filePath">Full path to the .cs script file.</param>
    /// <returns>CompilationResult with success status, type, and diagnostics.</returns>
    Task<CompilationResult> CompileScriptAsync(string filePath);
}
