namespace PokeSharp.Engine.Core.Types;

/// <summary>
///     Interface for types that can have custom Roslyn C# script behaviors.
///     Extends ITypeDefinition with scripting support for dynamic, moddable logic.
/// </summary>
/// <remarks>
///     Types implementing this interface can reference .csx script files that will be
///     compiled at runtime using Roslyn. Scripts enable modders to create custom behaviors
///     without recompiling the game engine.
/// </remarks>
public interface IScriptedType : ITypeDefinition
{
    /// <summary>
    ///     Path to Roslyn .csx script file (optional).
    ///     Relative to the game's Scripts directory.
    /// </summary>
    /// <example>
    ///     "behaviors/patrol_behavior.csx"
    ///     "weather/acid_rain_behavior.csx"
    ///     "triggers/gym_entrance.csx"
    /// </example>
    string? BehaviorScript { get; }
}
