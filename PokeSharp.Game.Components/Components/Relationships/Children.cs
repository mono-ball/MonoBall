using Arch.Core;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Component representing multiple child entities.
///     Attached to the parent entity to track all its children.
/// </summary>
/// <remarks>
///     <para>
///         This component maintains a collection of child entities that belong to
///         a parent entity. Common uses include:
///         - Trainer's Pokémon team
///         - Container's items
///         - Parent entity's sub-components
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Add children to parent
/// var trainer = world.Create(new Children {
///     Values = new List&lt;Entity&gt;()
/// });
///
/// var pokemon1 = world.Create();
/// var pokemon2 = world.Create();
///
/// ref var children = ref trainer.Get&lt;Children&gt;();
/// children.Values.Add(pokemon1);
/// children.Values.Add(pokemon2);
///
/// // Or use extension method
/// foreach (var child in trainer.GetChildren(world)) {
///     // Process each child
/// }
/// </code>
///     </para>
///     <para>
///         <b>Important:</b> The RelationshipSystem automatically removes destroyed
///         entities from the Values list during validation passes.
///     </para>
/// </remarks>
public struct Children
{
    /// <summary>
    ///     List of child entity references.
    /// </summary>
    /// <remarks>
    ///     This list should be initialized when the component is created.
    ///     Null-safe Count property is provided for convenience.
    ///     Invalid (destroyed) entities are automatically cleaned up by RelationshipSystem.
    /// </remarks>
    public List<Entity> Values;

    /// <summary>
    ///     Gets the number of children, safely handling null Values.
    /// </summary>
    /// <remarks>
    ///     Returns 0 if Values is null, otherwise returns the actual count.
    ///     This count may include destroyed entities between system updates.
    /// </remarks>
    public readonly int Count => Values?.Count ?? 0;
}
