namespace PokeSharp.Core.ScriptingApi;

/// <summary>
///     Composed World API interface that provides access to all domain-specific APIs.
///     This is the main interface exposed to scripts as a global variable.
/// </summary>
/// <remarks>
///     Available as global variable 'WorldApi' in all scripts:
///     - BehaviorScripts: NPC movement behaviors
///     - DialogueScripts: Conditional conversations
///     - TriggerScripts: Map events and cutscenes
/// </remarks>
public interface IWorldApi : IPlayerApi, IMapApi, INPCApi, IGameStateApi
{
    // This interface composes all domain APIs
    // No additional methods needed - it inherits from all domains
}