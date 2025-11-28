using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Engine.Systems.Queries;
using PokeSharp.Game.Components.Relationships;

namespace PokeSharp.Engine.Systems.Extensions;

/// <summary>
///     Extension methods for easy manipulation of entity relationships.
/// </summary>
/// <remarks>
///     These extensions provide a clean, fluent API for working with parent-child
///     and owner-owned relationships without directly manipulating components.
/// </remarks>
public static class RelationshipExtensions
{
    #region Parent-Child Relationships

    /// <summary>
    ///     Sets the parent of a child entity, establishing a parent-child relationship.
    /// </summary>
    /// <param name="child">The child entity.</param>
    /// <param name="parent">The parent entity.</param>
    /// <param name="world">The world containing both entities.</param>
    /// <exception cref="ArgumentException">Thrown if either entity is not alive.</exception>
    /// <remarks>
    ///     <para>
    ///         This method:
    ///         1. Validates both entities are alive
    ///         2. Removes any existing parent relationship from the child
    ///         3. Adds Parent component to child
    ///         4. Adds or updates Children component on parent
    ///     </para>
    ///     <para>
    ///         <b>Example:</b>
    ///         <code>
    /// var trainer = world.Create();
    /// var pokemon = world.Create();
    /// pokemon.SetParent(trainer, world);
    /// </code>
    ///     </para>
    /// </remarks>
    public static void SetParent(this Entity child, Entity parent, World world)
    {
        if (!world.IsAlive(child))
        {
            throw new ArgumentException("Child entity is not alive", nameof(child));
        }

        if (!world.IsAlive(parent))
        {
            throw new ArgumentException("Parent entity is not alive", nameof(parent));
        }

        // Remove existing parent if present
        if (child.Has<Parent>())
        {
            Entity oldParent = child.Get<Parent>().Value;
            if (world.IsAlive(oldParent) && oldParent.Has<Children>())
            {
                ref Children oldChildren = ref oldParent.Get<Children>();
                oldChildren.Values?.Remove(child);
            }

            child.Remove<Parent>();
        }

        // Set new parent
        child.Add(new Parent { Value = parent, EstablishedAt = DateTime.UtcNow });

        // Add to parent's children list
        if (!parent.Has<Children>())
        {
            parent.Add(new Children { Values = new List<Entity>() });
        }

        ref Children children = ref parent.Get<Children>();
        if (children.Values == null)
        {
            children.Values = new List<Entity>();
        }

        if (!children.Values.Contains(child))
        {
            children.Values.Add(child);
        }
    }

    /// <summary>
    ///     Removes the parent relationship from a child entity.
    /// </summary>
    /// <param name="child">The child entity.</param>
    /// <param name="world">The world containing the entity.</param>
    /// <remarks>
    ///     This method safely removes the Parent component and updates the
    ///     parent's Children component to remove this child reference.
    /// </remarks>
    public static void RemoveParent(this Entity child, World world)
    {
        if (!world.IsAlive(child) || !child.Has<Parent>())
        {
            return;
        }

        Entity parent = child.Get<Parent>().Value;

        // Remove from parent's children list
        if (world.IsAlive(parent) && parent.Has<Children>())
        {
            ref Children children = ref parent.Get<Children>();
            children.Values?.Remove(child);
        }

        // Remove parent component
        child.Remove<Parent>();
    }

    /// <summary>
    ///     Gets the parent entity of a child, if it exists and is valid.
    /// </summary>
    /// <param name="child">The child entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>The parent entity if it exists and is alive, otherwise null.</returns>
    /// <remarks>
    ///     <b>Example:</b>
    ///     <code>
    /// var parent = pokemon.GetParent(world);
    /// if (parent.HasValue) {
    ///     Console.WriteLine($"Pokemon's trainer: {parent.Value}");
    /// }
    /// </code>
    /// </remarks>
    public static Entity? GetParent(this Entity child, World world)
    {
        if (!world.IsAlive(child) || !child.Has<Parent>())
        {
            return null;
        }

        Entity parent = child.Get<Parent>().Value;
        return world.IsAlive(parent) ? parent : null;
    }

    /// <summary>
    ///     Gets all children of a parent entity.
    /// </summary>
    /// <param name="parent">The parent entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>An enumerable of all valid (alive) children.</returns>
    /// <remarks>
    ///     This method automatically filters out any destroyed entities,
    ///     ensuring you only get valid child references.
    ///     <para>
    ///         <b>Example:</b>
    ///         <code>
    /// foreach (var pokemon in trainer.GetChildren(world)) {
    ///     Console.WriteLine($"Trainer has: {pokemon}");
    /// }
    /// </code>
    ///     </para>
    /// </remarks>
    public static IEnumerable<Entity> GetChildren(this Entity parent, World world)
    {
        if (!world.IsAlive(parent) || !parent.Has<Children>())
        {
            return Enumerable.Empty<Entity>();
        }

        Children children = parent.Get<Children>();
        if (children.Values == null)
        {
            return Enumerable.Empty<Entity>();
        }

        return children.Values.Where(child => world.IsAlive(child));
    }

    /// <summary>
    ///     Gets the count of valid children for a parent entity.
    /// </summary>
    /// <param name="parent">The parent entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>The number of alive children.</returns>
    public static int GetChildCount(this Entity parent, World world)
    {
        return parent.GetChildren(world).Count();
    }

    #endregion

    #region Owner-Owned Relationships

    /// <summary>
    ///     Sets the owner of an owned entity, establishing an ownership relationship.
    /// </summary>
    /// <param name="owned">The entity to be owned.</param>
    /// <param name="owner">The owner entity.</param>
    /// <param name="world">The world containing both entities.</param>
    /// <param name="ownershipType">The type of ownership (default: Permanent).</param>
    /// <exception cref="ArgumentException">Thrown if either entity is not alive.</exception>
    /// <remarks>
    ///     <para>
    ///         This method establishes bidirectional ownership:
    ///         1. Adds Owned component to the owned entity
    ///         2. Adds Owner component to the owner entity
    ///     </para>
    ///     <para>
    ///         <b>Example:</b>
    ///         <code>
    /// var player = world.Create();
    /// var item = world.Create();
    /// item.SetOwner(player, world, OwnershipType.Permanent);
    /// </code>
    ///     </para>
    /// </remarks>
    public static void SetOwner(
        this Entity owned,
        Entity owner,
        World world,
        OwnershipType ownershipType = OwnershipType.Permanent
    )
    {
        if (!world.IsAlive(owned))
        {
            throw new ArgumentException("Owned entity is not alive", nameof(owned));
        }

        if (!world.IsAlive(owner))
        {
            throw new ArgumentException("Owner entity is not alive", nameof(owner));
        }

        // Remove existing ownership if present
        if (owned.Has<Owned>())
        {
            owned.Remove<Owned>();
        }

        // Set new owner
        owned.Add(new Owned { OwnerEntity = owner, AcquiredAt = DateTime.UtcNow });

        // Set owner relationship
        if (!owner.Has<Owner>() || owner.Get<Owner>().Value != owned)
        {
            owner.Set(new Owner { Value = owned, Type = ownershipType });
        }
    }

    /// <summary>
    ///     Removes the owner relationship from an owned entity.
    /// </summary>
    /// <param name="owned">The owned entity.</param>
    /// <param name="world">The world containing the entity.</param>
    /// <remarks>
    ///     This removes both the Owned component from the entity and the Owner
    ///     component from the owner entity.
    /// </remarks>
    public static void RemoveOwner(this Entity owned, World world)
    {
        if (!world.IsAlive(owned) || !owned.Has<Owned>())
        {
            return;
        }

        Entity owner = owned.Get<Owned>().OwnerEntity;

        // Remove owner component from owner entity
        if (world.IsAlive(owner) && owner.Has<Owner>())
        {
            Owner ownerComp = owner.Get<Owner>();
            if (ownerComp.Value == owned)
            {
                owner.Remove<Owner>();
            }
        }

        // Remove owned component
        owned.Remove<Owned>();
    }

    /// <summary>
    ///     Gets the owner entity of an owned entity, if it exists and is valid.
    /// </summary>
    /// <param name="owned">The owned entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>The owner entity if it exists and is alive, otherwise null.</returns>
    /// <remarks>
    ///     <b>Example:</b>
    ///     <code>
    /// var owner = item.GetOwner(world);
    /// if (owner.HasValue) {
    ///     Console.WriteLine($"Item owned by: {owner.Value}");
    /// }
    /// </code>
    /// </remarks>
    public static Entity? GetOwner(this Entity owned, World world)
    {
        if (!world.IsAlive(owned) || !owned.Has<Owned>())
        {
            return null;
        }

        Entity owner = owned.Get<Owned>().OwnerEntity;
        return world.IsAlive(owner) ? owner : null;
    }

    /// <summary>
    ///     Gets all entities owned by an owner entity.
    /// </summary>
    /// <param name="owner">The owner entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>An enumerable of all entities owned by this owner.</returns>
    /// <remarks>
    ///     This queries all Owned components in the world and filters for those
    ///     referencing this owner. For better performance with many owned entities,
    ///     consider using the Children component pattern instead.
    /// </remarks>
    public static IEnumerable<Entity> GetOwnedEntities(this Entity owner, World world)
    {
        if (!world.IsAlive(owner))
        {
            return Enumerable.Empty<Entity>();
        }

        var ownedEntities = new List<Entity>();
        // Use centralized relationship query
        QueryDescription query = RelationshipQueries.AllOwned;

        world.Query(
            in query,
            (Entity entity, ref Owned owned) =>
            {
                if (owned.OwnerEntity == owner && world.IsAlive(entity))
                {
                    ownedEntities.Add(entity);
                }
            }
        );

        return ownedEntities;
    }

    /// <summary>
    ///     Gets the ownership type for an owned entity.
    /// </summary>
    /// <param name="owned">The owned entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>The ownership type if the entity has a valid owner, otherwise null.</returns>
    public static OwnershipType? GetOwnershipType(this Entity owned, World world)
    {
        Entity? owner = owned.GetOwner(world);
        if (!owner.HasValue || !owner.Value.Has<Owner>())
        {
            return null;
        }

        return owner.Value.Get<Owner>().Type;
    }

    #endregion
}
