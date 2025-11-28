using System.Collections;
using System.Reflection;
using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Engine.Debug.Console.Scripting;

/// <summary>
///     Evaluates C# code snippets using Roslyn scripting for the console.
///     Maintains script state between evaluations for persistent variables.
/// </summary>
public class ConsoleScriptEvaluator
{
    private readonly ILogger _logger;
    private readonly ScriptOptions _scriptOptions;

    /// <summary>
    ///     Initializes a new instance of the console script evaluator.
    /// </summary>
    public ConsoleScriptEvaluator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure script options with all necessary references and imports
        _scriptOptions = ScriptOptions
            .Default.AddReferences(GetDefaultReferences())
            .AddImports(GetDefaultImports());

        _logger.LogInformation("Console script evaluator initialized");
    }

    /// <summary>
    ///     Gets the current script state (for auto-completion tracking).
    /// </summary>
    public ScriptState<object>? CurrentState { get; private set; }

    /// <summary>
    ///     Evaluates a C# code snippet and returns the result.
    /// </summary>
    /// <param name="code">The C# code to evaluate.</param>
    /// <param name="globals">Global variables available to the script.</param>
    /// <returns>The result of the evaluation.</returns>
    public async Task<EvaluationResult> EvaluateAsync(string code, ConsoleGlobals globals)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return EvaluationResult.Empty();
        }

        try
        {
            _logger.LogDebug("Evaluating code: {Code}", code);

            if (CurrentState == null)
            {
                // First execution - create new script state
                CurrentState = await CSharpScript.RunAsync(code, _scriptOptions, globals);
            }
            else
            {
                // Continue from previous state (preserves variables)
                CurrentState = await CurrentState.ContinueWithAsync(code);
            }

            object? result = CurrentState.ReturnValue;
            return EvaluationResult.Success(FormatResult(result));
        }
        catch (CompilationErrorException ex)
        {
            _logger.LogWarning(ex, "Compilation error in console script");
            List<FormattedError> errors = ErrorFormatter.FormatErrors(ex.Diagnostics, code);
            return EvaluationResult.CompilationError(errors, code);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Runtime error in console script");
            return EvaluationResult.RuntimeError(ex);
        }
    }

    /// <summary>
    ///     Resets the script state, clearing all variables.
    /// </summary>
    public void Reset()
    {
        CurrentState = null;
        _logger.LogDebug("Console script state reset");
    }

    /// <summary>
    ///     Checks if the given code is syntactically complete.
    ///     Used to determine if multi-line input should continue.
    /// </summary>
    /// <param name="code">The code to check.</param>
    /// <returns>True if the code is complete, false if more input is needed.</returns>
    public bool IsCodeComplete(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        // Quick check for obviously incomplete code
        string trimmed = code.Trim();

        // Check for unclosed braces/brackets/parens
        int braceCount = 0;
        int bracketCount = 0;
        int parenCount = 0;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;
        char prevChar = '\0';

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];

            // Handle string literals
            if (c == '"' && !inChar)
            {
                if (i > 0 && code[i - 1] == '@' && !inString)
                {
                    inVerbatimString = !inVerbatimString;
                }
                else if (!inVerbatimString && prevChar != '\\')
                {
                    inString = !inString;
                }
                else if (inVerbatimString && i + 1 < code.Length && code[i + 1] == '"')
                {
                    i++; // Skip escaped quote in verbatim string
                }
                else if (inVerbatimString)
                {
                    inVerbatimString = false;
                }
            }
            else if (c == '\'' && !inString && !inVerbatimString)
            {
                if (prevChar != '\\')
                {
                    inChar = !inChar;
                }
            }

            // Only count braces outside of strings
            if (!inString && !inVerbatimString && !inChar)
            {
                switch (c)
                {
                    case '{':
                        braceCount++;
                        break;
                    case '}':
                        braceCount--;
                        break;
                    case '[':
                        bracketCount++;
                        break;
                    case ']':
                        bracketCount--;
                        break;
                    case '(':
                        parenCount++;
                        break;
                    case ')':
                        parenCount--;
                        break;
                }
            }

            prevChar = c;
        }

        // If any brackets are unclosed, code is incomplete
        if (braceCount > 0 || bracketCount > 0 || parenCount > 0)
        {
            return false;
        }

        // If we're still inside a string, code is incomplete
        if (inString || inVerbatimString || inChar)
        {
            return false;
        }

        // Check for lines ending with operators that expect continuation
        string[] lines = code.Split('\n');
        string lastLine = lines.LastOrDefault()?.Trim() ?? "";

        // Common patterns that indicate incomplete statements
        if (
            lastLine.EndsWith("=>")
            || lastLine.EndsWith("&&")
            || lastLine.EndsWith("||")
            || lastLine.EndsWith("+")
            || lastLine.EndsWith("-")
            || lastLine.EndsWith("*")
            || lastLine.EndsWith("/")
            || lastLine.EndsWith(",")
            || lastLine.EndsWith("(")
            || lastLine.EndsWith("{")
            || lastLine.EndsWith("[")
        )
        {
            return false;
        }

        // Use Roslyn to parse and check for incomplete syntax
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            code,
            CSharpParseOptions.Default.WithKind(SourceCodeKind.Script)
        );
        IEnumerable<Diagnostic> diagnostics = tree.GetDiagnostics();

        // Check for "expected" errors which indicate incomplete code
        foreach (Diagnostic diagnostic in diagnostics)
        {
            // CS1733: Expected expression (common for incomplete for loops etc)
            // CS1026: ) expected
            // CS1513: } expected
            // CS1002: ; expected (at end of incomplete statement)
            if (
                diagnostic.Id == "CS1733"
                || diagnostic.Id == "CS1026"
                || diagnostic.Id == "CS1513"
                || (
                    diagnostic.Id == "CS1002"
                    && diagnostic.Location.SourceSpan.End >= code.Length - 1
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets all user-defined variables from the script state.
    /// </summary>
    public IEnumerable<(string Name, string TypeName, Func<object?> ValueGetter)> GetVariables()
    {
        if (CurrentState == null)
        {
            yield break;
        }

        foreach (ScriptVariable? variable in CurrentState.Variables)
        {
            string? varName = variable.Name;
            Type? varType = variable.Type;

            // Create a closure to get the current value
            yield return (varName, varType.Name, () => GetVariableValue(varName));
        }
    }

    /// <summary>
    ///     Gets the current value of a variable by name.
    /// </summary>
    public object? GetVariableValue(string name)
    {
        if (CurrentState == null)
        {
            return null;
        }

        ScriptVariable? variable = CurrentState.Variables.FirstOrDefault(v => v.Name == name);
        return variable?.Value;
    }

    /// <summary>
    ///     Formats the result of a script evaluation.
    /// </summary>
    private static string FormatResult(object? result)
    {
        if (result == null)
        {
            return "null";
        }

        // Handle common MonoGame types
        if (result is Vector2 v2)
        {
            return $"Vector2({v2.X:F2}, {v2.Y:F2})";
        }

        if (result is Point p)
        {
            return $"Point({p.X}, {p.Y})";
        }

        if (result is Rectangle rect)
        {
            return $"Rectangle(X:{rect.X}, Y:{rect.Y}, W:{rect.Width}, H:{rect.Height})";
        }

        if (result is Color color)
        {
            return $"Color(R:{color.R}, G:{color.G}, B:{color.B}, A:{color.A})";
        }

        // Handle Entity
        if (result is Entity entity)
        {
            return $"Entity(Id: {entity.Id})";
        }

        // Handle collections
        if (result is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object>().Take(10).ToList();
            bool moreItems = enumerable.Cast<object>().Count() > 10;
            string itemsStr = string.Join(", ", items.Select(FormatResult));
            return moreItems ? $"[{itemsStr}, ...]" : $"[{itemsStr}]";
        }

        return result.ToString() ?? "null";
    }

    /// <summary>
    ///     Gets the default assembly references for console scripts.
    /// </summary>
    public static IEnumerable<Assembly> GetDefaultReferences()
    {
        return new[]
        {
            typeof(object).Assembly, // System.Private.CoreLib
            typeof(System.Console).Assembly, // System.Console
            typeof(Enumerable).Assembly, // System.Linq
            typeof(List<>).Assembly, // System.Collections
            typeof(World).Assembly, // Arch.Core
            typeof(Entity).Assembly, // Arch.Core
            typeof(Point).Assembly, // MonoGame.Framework
            typeof(Vector2).Assembly, // MonoGame.Framework
            typeof(Direction).Assembly, // PokeSharp.Game.Components
            typeof(IScriptingApiProvider).Assembly, // PokeSharp.Game.Scripting
            typeof(ILogger).Assembly, // Microsoft.Extensions.Logging.Abstractions
        };
    }

    /// <summary>
    ///     Gets the default namespace imports for console scripts.
    /// </summary>
    public static IEnumerable<string> GetDefaultImports()
    {
        return new[]
        {
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "Arch.Core",
            "Microsoft.Xna.Framework",
            "Microsoft.Extensions.Logging",
            "PokeSharp.Game.Components.Movement",
            "PokeSharp.Game.Components.Player",
            "PokeSharp.Game.Components.Rendering",
            "PokeSharp.Game.Scripting.Api",
        };
    }
}
