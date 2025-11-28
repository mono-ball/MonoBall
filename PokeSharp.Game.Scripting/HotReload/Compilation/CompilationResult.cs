namespace PokeSharp.Game.Scripting.HotReload.Compilation;

/// <summary>
///     Enhanced compilation result with detailed diagnostics for automatic rollback.
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }
    public Type? CompiledType { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<CompilationDiagnostic> Diagnostics { get; init; } = new();

    /// <summary>
    ///     Returns true if compilation has any error-level diagnostics.
    /// </summary>
    public bool HasErrors =>
        Diagnostics.Count > 0 && Diagnostics.Exists(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    ///     Get count of errors.
    /// </summary>
    public int ErrorCount
    {
        get
        {
            if (Diagnostics.Count > 0)
            {
                return Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            }

            return Errors.Count;
        }
    }

    /// <summary>
    ///     Get count of warnings.
    /// </summary>
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

    /// <summary>
    ///     Get formatted error summary for logging and UI display.
    /// </summary>
    public string GetErrorSummary()
    {
        if (Diagnostics.Count > 0 && HasErrors)
        {
            List<string> errors = Diagnostics
                .FindAll(d => d.Severity == DiagnosticSeverity.Error)
                .ConvertAll(d => d.ToString());

            return string.Join("\n  ", errors);
        }

        if (Errors.Count > 0)
        {
            return string.Join("\n  ", Errors);
        }

        return "No errors";
    }
}
