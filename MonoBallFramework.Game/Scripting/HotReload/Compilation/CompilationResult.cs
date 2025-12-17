namespace MonoBallFramework.Game.Scripting.HotReload.Compilation;

/// <summary>
///     Enhanced compilation result with detailed diagnostics for automatic rollback.
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }
    public Type? CompiledType { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<CompilationDiagnostic> Diagnostics { get; init; } = [];

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
            return Diagnostics.Count > 0
                ? Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)
                : Errors.Count;
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

        return Errors.Count > 0
            ? string.Join("\n  ", Errors)
            : "No errors";
    }
}
