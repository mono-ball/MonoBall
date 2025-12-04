namespace PokeSharp.Game.Components.NPCs;

/// <summary>
///     Component that references a behavior type.
///     Links an entity to a moddable behavior from the TypeRegistry.
///     Pure data component - no methods.
/// </summary>
public struct Behavior
{
    /// <summary>
    ///     Type identifier for the behavior (e.g., "patrol", "stationary", "trainer").
    ///     References a type in the BehaviorDefinition TypeRegistry.
    /// </summary>
    public string BehaviorTypeId { get; set; }

    /// <summary>
    ///     Whether this behavior is currently active and should execute its OnTick method.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Whether this behavior has been initialized (OnActivated called).
    ///     Used to ensure script initialization happens only once per entity.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    ///     Initializes a new behavior component with a type ID.
    /// </summary>
    public Behavior(string behaviorTypeId)
    {
        BehaviorTypeId = behaviorTypeId;
        IsActive = true;
        IsInitialized = false;
    }
}
