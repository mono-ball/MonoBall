namespace MonoBallFramework.Game.Ecs.Components.Tiles;

/// <summary>
///     Component that references a tile behavior type.
///     Links a tile entity to a moddable behavior from the TypeRegistry.
///     Pure data component - no methods.
/// </summary>
public struct TileBehavior
{
    /// <summary>
    ///     Type identifier for the behavior (e.g., "jump_south", "impassable_east", "ice").
    ///     References a type in the TileBehaviorDefinition TypeRegistry.
    /// </summary>
    public string BehaviorTypeId { get; set; }

    /// <summary>
    ///     Whether this behavior is currently active and should execute its logic.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Whether this behavior has been initialized (OnActivated called).
    ///     Used to ensure script initialization happens only once per entity.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    ///     Initializes a new tile behavior component with a type ID.
    /// </summary>
    public TileBehavior(string behaviorTypeId)
    {
        BehaviorTypeId = behaviorTypeId;
        IsActive = true;
        IsInitialized = false;
    }
}
