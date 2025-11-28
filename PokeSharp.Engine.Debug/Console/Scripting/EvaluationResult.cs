namespace PokeSharp.Engine.Debug.Console.Scripting;

/// <summary>
///     Represents the result of a console script evaluation.
/// </summary>
public class EvaluationResult
{
    public bool IsSuccess { get; init; }
    public bool IsCompilationError { get; init; }
    public bool IsRuntimeError { get; init; }
    public string? Output { get; init; }
    public List<FormattedError>? Errors { get; init; }
    public string? SourceCode { get; init; }
    public Exception? RuntimeException { get; init; }

    /// <summary>
    ///     Creates a successful evaluation result.
    /// </summary>
    public static EvaluationResult Success(string output)
    {
        return new EvaluationResult { IsSuccess = true, Output = output };
    }

    /// <summary>
    ///     Creates an empty result (no output).
    /// </summary>
    public static EvaluationResult Empty()
    {
        return new EvaluationResult { IsSuccess = true, Output = string.Empty };
    }

    /// <summary>
    ///     Creates a compilation error result.
    /// </summary>
    public static EvaluationResult CompilationError(List<FormattedError> errors, string sourceCode)
    {
        return new EvaluationResult
        {
            IsCompilationError = true,
            Errors = errors,
            SourceCode = sourceCode,
        };
    }

    /// <summary>
    ///     Creates a runtime error result.
    /// </summary>
    public static EvaluationResult RuntimeError(Exception exception)
    {
        return new EvaluationResult
        {
            IsRuntimeError = true,
            RuntimeException = exception,
            Output = $"Runtime Error: {exception.GetType().Name}: {exception.Message}",
        };
    }
}
