namespace PokeSharp.Scripting.HotReload;

/// <summary>
///     Detailed compilation diagnostic information for error reporting and rollback decisions.
/// </summary>
public class CompilationDiagnostic
{
    public DiagnosticSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Code { get; init; }
    public string? FilePath { get; init; }

    public override string ToString()
    {
        return $"[{Severity}] Line {Line}, Col {Column}: {Message} ({Code})";
    }
}

public enum DiagnosticSeverity
{
    Hidden,
    Info,
    Warning,
    Error,
}

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
                return Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
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
            var errors = Diagnostics
                .FindAll(d => d.Severity == DiagnosticSeverity.Error)
                .ConvertAll(d => d.ToString());

            return string.Join("\n  ", errors);
        }

        if (Errors.Count > 0)
            return string.Join("\n  ", Errors);

        return "No errors";
    }
}
