using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Scripting.Compilation;
using PokeSharp.Game.Scripting.Runtime;
using CompilationErrorException = Microsoft.CodeAnalysis.Scripting.CompilationErrorException;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Compiles C# scripts (.csx files) using Roslyn CSharpScript API.
///     Handles script creation, compilation, and validation.
/// </summary>
public class ScriptCompiler
{
    private readonly ScriptOptions _defaultOptions;
    private readonly ILogger<ScriptCompiler> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptCompiler" /> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ScriptCompiler(ILogger<ScriptCompiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultOptions = ScriptCompilationOptions.GetDefaultOptions();
    }

    /// <summary>
    ///     Compiles a script from source code.
    /// </summary>
    /// <param name="scriptCode">The C# script source code.</param>
    /// <param name="scriptPath">The script path (for logging purposes).</param>
    /// <returns>The compiled script, or null if compilation failed.</returns>
    public Script<object>? Compile(string scriptCode, string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptCode))
        {
            _logger.LogError("Script code is null or empty for {Path}", scriptPath);
            return null;
        }

        try
        {
            // Create script from source code
            Script<object>? script = CSharpScript.Create<object>(scriptCode, _defaultOptions);

            // Compile the script
            ImmutableArray<Diagnostic> diagnostics = script.Compile();

            // Check for errors (avoid LINQ allocation by iterating directly)
            bool hasErrors = false;
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    if (!hasErrors)
                    {
                        _logger.LogError("Script compilation failed for {Path}:", scriptPath);
                        hasErrors = true;
                    }

                    _logger.LogError("  {Message}", diagnostic.GetMessage());
                }
            }

            if (hasErrors)
            {
                return null;
            }

            return script;
        }
        catch (CompilationErrorException ex)
        {
            _logger.LogError(
                ex,
                "Script compilation error in {Path}: {Message}",
                scriptPath,
                ex.Message
            );
            return null;
        }
    }

    /// <summary>
    ///     Executes a compiled script and returns the instance.
    /// </summary>
    /// <param name="script">The compiled script.</param>
    /// <param name="scriptPath">The script path (for logging purposes).</param>
    /// <returns>The script instance, or null if execution failed.</returns>
    public async Task<object?> ExecuteAsync(Script<object> script, string scriptPath)
    {
        if (script == null)
        {
            _logger.LogError("Cannot execute null script for {Path}", scriptPath);
            return null;
        }

        try
        {
            // Execute script to get the instance
            ScriptState<object>? result = await script.RunAsync();
            object? instance = result.ReturnValue;

            if (instance == null)
            {
                _logger.LogError("Script {Path} did not return an instance", scriptPath);
                return null;
            }

            if (instance is not ScriptBase)
            {
                _logger.LogError(
                    "Script {Path} returned {Type}, expected ScriptBase",
                    scriptPath,
                    instance.GetType().Name
                );
                return null;
            }

            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing script {Path}: {Message}",
                scriptPath,
                ex.Message
            );
            return null;
        }
    }
}
