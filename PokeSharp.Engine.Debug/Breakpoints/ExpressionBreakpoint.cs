using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Console.Scripting;

namespace PokeSharp.Engine.Debug.Breakpoints;

/// <summary>
///     A breakpoint that evaluates a C# expression each frame and triggers when it becomes true.
/// </summary>
public class ExpressionBreakpoint : IBreakpoint
{
    private readonly ConsoleScriptEvaluator _evaluator;
    private readonly ConsoleGlobals _globals;
    private readonly ILogger? _logger;
    private bool _lastValue;

    public ExpressionBreakpoint(
        int id,
        string expression,
        ConsoleScriptEvaluator evaluator,
        ConsoleGlobals globals,
        ILogger? logger = null
    )
    {
        Id = id;
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _logger = logger;
    }

    public string Expression { get; }

    /// <summary>
    ///     If true, only triggers on transition from false to true.
    ///     If false, triggers every frame the expression is true.
    /// </summary>
    public bool TriggerOnChange { get; set; } = true;

    public int Id { get; }
    public BreakpointType Type => BreakpointType.Expression;
    public string Description => $"when {Expression}";
    public bool IsEnabled { get; set; } = true;
    public int HitCount { get; private set; }

    public async Task<bool> EvaluateAsync()
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            EvaluationResult result = await _evaluator.EvaluateAsync(Expression, _globals);

            if (!result.IsSuccess)
            {
                _logger?.LogDebug(
                    "Expression breakpoint #{Id} evaluation failed: {Expression}",
                    Id,
                    Expression
                );
                return false;
            }

            // Parse the result as boolean
            bool currentValue = ParseBoolResult(result.Output);

            if (TriggerOnChange)
            {
                // Only trigger on transition from false to true
                bool shouldTrigger = currentValue && !_lastValue;
                _lastValue = currentValue;
                return shouldTrigger;
            }

            _lastValue = currentValue;
            return currentValue;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(
                ex,
                "Error evaluating expression breakpoint #{Id}: {Expression}",
                Id,
                Expression
            );
            return false;
        }
    }

    public void OnHit()
    {
        HitCount++;
    }

    public void ResetHitCount()
    {
        HitCount = 0;
        _lastValue = false;
    }

    private static bool ParseBoolResult(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        string trimmed = output.Trim().ToLowerInvariant();
        return trimmed == "true" || trimmed == "1";
    }
}
