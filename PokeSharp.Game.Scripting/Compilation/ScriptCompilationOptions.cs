using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Game.Scripting.Compilation;

/// <summary>
///     Configuration options for Roslyn script compilation.
///     Defines assembly references, imports, and compiler settings.
/// </summary>
public static class ScriptCompilationOptions
{
    /// <summary>
    ///     Get default script options for compiling behavior scripts.
    ///     Includes necessary assembly references and namespace imports.
    /// </summary>
    public static ScriptOptions GetDefaultOptions()
    {
        ScriptOptions options = ScriptOptions
            .Default
            // Add assembly references
            .AddReferences(
                typeof(object).Assembly, // System
                typeof(Console).Assembly, // System.Console
                typeof(Enumerable).Assembly, // System.Linq
                typeof(List<>).Assembly, // System.Collections.Generic
                typeof(World).Assembly, // Arch.Core
                typeof(Point).Assembly, // MonoGame.Framework
                typeof(TypeScriptBase).Assembly, // PokeSharp.Game.Scripting.Runtime
                typeof(Direction).Assembly, // PokeSharp.Game.Components (Movement)
                typeof(Npc).Assembly, // PokeSharp.Game.Components (NPCs)
                typeof(ILogger).Assembly // Microsoft.Extensions.Logging.Abstractions
            )
            // Add namespace imports
            .AddImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "Arch.Core",
                "Microsoft.Xna.Framework",
                "Microsoft.Extensions.Logging",
                "PokeSharp.Game.Scripting.Runtime",
                "PokeSharp.Game.Scripting.Api",
                "PokeSharp.Game.Components.Maps",
                "PokeSharp.Game.Components.Movement",
                "PokeSharp.Game.Components.NPCs",
                "PokeSharp.Game.Components.NPCs.States",
                "PokeSharp.Game.Components.Common",
                "PokeSharp.Game.Components.Player",
                "PokeSharp.Game.Components.Rendering",
                "PokeSharp.Game.Components.Tiles",
                "PokeSharp.Engine.Core.Types"
            )
            // Optimization settings
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithCheckOverflow(false)
            .WithAllowUnsafe(false);

        return options;
    }

    /// <summary>
    ///     Get script options with additional custom assemblies and imports.
    /// </summary>
    /// <param name="additionalAssemblies">Additional assemblies to reference.</param>
    /// <param name="additionalImports">Additional namespaces to import.</param>
    public static ScriptOptions GetCustomOptions(
        IEnumerable<Type>? additionalAssemblies = null,
        IEnumerable<string>? additionalImports = null
    )
    {
        ScriptOptions options = GetDefaultOptions();

        if (additionalAssemblies != null)
        {
            options = options.AddReferences(additionalAssemblies.Select(t => t.Assembly));
        }

        if (additionalImports != null)
        {
            options = options.AddImports(additionalImports);
        }

        return options;
    }
}
