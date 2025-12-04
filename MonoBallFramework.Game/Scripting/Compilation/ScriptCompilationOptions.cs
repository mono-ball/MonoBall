using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Scripting.Runtime;

namespace MonoBallFramework.Game.Scripting.Compilation;

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
                typeof(TypeScriptBase).Assembly, // MonoBallFramework.Game.Scripting.Runtime
                typeof(Direction).Assembly, // MonoBallFramework.Game.Components (Movement)
                typeof(Npc).Assembly, // MonoBallFramework.Game.Components (NPCs)
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
                "MonoBallFramework.Game.Scripting.Runtime",
                "MonoBallFramework.Game.Scripting.Api",
                "MonoBallFramework.Game.Ecs.Components.Maps",
                "MonoBallFramework.Game.Ecs.Components.Movement",
                "MonoBallFramework.Game.Ecs.Components.NPCs",
                "MonoBallFramework.Game.Ecs.Components.NPCs.States",
                "MonoBallFramework.Game.Ecs.Components.Common",
                "MonoBallFramework.Game.Ecs.Components.Player",
                "MonoBallFramework.Game.Ecs.Components.Rendering",
                "MonoBallFramework.Game.Ecs.Components.Tiles",
                "MonoBallFramework.Game.Engine.Core.Types",
                "MonoBallFramework.Game.Engine.Core.Events.System",
                "MonoBallFramework.Game.GameSystems.Events",
                "Arch.Core.Extensions"
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
