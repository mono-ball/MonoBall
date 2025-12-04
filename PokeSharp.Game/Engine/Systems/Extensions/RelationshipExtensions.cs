using Arch.Core;
using Arch.Relationships;
using PokeSharp.Game.Components.Relationships;

namespace PokeSharp.Game.Engine.Systems.Extensions;

/// <summary>
///     Extension methods for easy manipulation of entity relationships using Arch.Relationships.
/// </summary>
/// <remarks>
///     These extensions provide a clean, fluent API for working with parent-child
///     and owner-owned relationships using the Arch.Relationships API.
/// </remarks>
public static class RelationshipExtensions
{
    #region Parent-Child Relationships

    /// <summary>
    ///     Sets the parent of a child entity, establishing a parent-child relationship using Arch.Relationships.
    /// </summary>
    /// <param name="child">The child entity.</param>
    /// <param name="parent">The parent entity.</param>
    /// <param name="world">The world containing both entities.</param>
    /// <exception cref="ArgumentException">Thrown if either entity is not alive.</exception>
    /// <remarks>
    ///     <para>
    ///         This method uses Arch.Relationships API to create a bidirectional relationship
    ///         automatically. The relationship is stored efficiently and can be queried directly.
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

        // Use Arch.Relationships API - this automatically creates bidirectional relationship
        parent.AddRelationship(child, new ParentOf());
    }

    /// <summary>
    ///     Removes the parent relationship from a child entity.
    /// </summary>
    /// <param name="child">The child entity.</param>
    /// <param name="world">The world containing the entity.</param>
    /// <remarks>
    ///     This method uses Arch.Relationships API to remove the relationship.
    ///     The bidirectional relationship is automatically cleaned up.
    /// </remarks>
    public static void RemoveParent(this Entity child, World world)
    {
        if (!world.IsAlive(child))
        {
            return;
        }

        // Find parent by iterating through all entities that have this child as a relationship
        // This is less efficient but works with Arch.Relationships API
        Entity? parent = GetParent(child, world);
        if (parent.HasValue && world.IsAlive(parent.Value))
        {
            parent.Value.RemoveRelationship<ParentOf>(child);
        }
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
        if (!world.IsAlive(child))
        {
            return null;
        }

        // Find parent by querying all entities that might have ParentOf relationships
        Entity? parentEntity = null;
        world.Query(
            new QueryDescription(),
            entity =>
            {
                if (entity.HasRelationship<ParentOf>(child))
                {
                    parentEntity = entity;
                }
            }
        );

        return parentEntity;
    }

    /// <summary>
    ///     Gets all children of a parent entity using Arch.Relationships.
    /// </summary>
    /// <param name="parent">The parent entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>An enumerable of all valid (alive) children.</returns>
    /// <remarks>
    ///     This method uses Arch.Relationships API to directly iterate children.
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
        if (!world.IsAlive(parent) || !parent.HasRelationship<ParentOf>())
        {
            return Enumerable.Empty<Entity>();
        }

        // Use Arch.Relationships API to iterate children
        ref Relationship<ParentOf> relationships = ref parent.GetRelationships<ParentOf>();
        var children = new List<Entity>();

        foreach (KeyValuePair<Entity, ParentOf> kvp in relationships)
        {
            Entity child = kvp.Key;
            if (world.IsAlive(child))
            {
                children.Add(child);
            }
        }

        return children;
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
    ///     Sets the owner of an owned entity, establishing an ownership relationship using Arch.Relationships.
    /// </summary>
    /// <param name="owned">The entity to be owned.</param>
    /// <param name="owner">The owner entity.</param>
    /// <param name="world">The world containing both entities.</param>
    /// <param name="ownershipType">The type of ownership (default: Permanent).</param>
    /// <exception cref="ArgumentException">Thrown if either entity is not alive.</exception>
    /// <remarks>
    ///     <para>
    ///         This method uses Arch.Relationships API to create a bidirectional relationship
    ///         automatically. The relationship is stored efficiently and can be queried directly.
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

        // Use Arch.Relationships API - this automatically creates bidirectional relationship
        owner.AddRelationship(owned, new OwnerOf(ownershipType));
    }

    /// <summary>
    ///     Removes the owner relationship from an owned entity.
    /// </summary>
    /// <param name="owned">The owned entity.</param>
    /// <param name="world">The world containing the entity.</param>
    /// <remarks>
    ///     This uses Arch.Relationships API to remove the bidirectional relationship.
    /// </remarks>
    public static void RemoveOwner(this Entity owned, World world)
    {
        if (!world.IsAlive(owned))
        {
            return;
        }

        // Find owner and remove relationship
        Entity? owner = GetOwner(owned, world);
        if (owner.HasValue && world.IsAlive(owner.Value))
        {
            owner.Value.RemoveRelationship<OwnerOf>(owned);
        }
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
        if (!world.IsAlive(owned))
        {
            return null;
        }

        // Find owner by querying all entities that might have OwnerOf relationships
        Entity? ownerEntity = null;
        world.Query(
            new QueryDescription(),
            entity =>
            {
                if (entity.HasRelationship<OwnerOf>(owned))
                {
                    ownerEntity = entity;
                }
            }
        );

        return ownerEntity;
    }

    /// <summary>
    ///     Gets all entities owned by an owner entity using Arch.Relationships.
    /// </summary>
    /// <param name="owner">The owner entity.</param>
    /// <param name="world">The world containing the entities.</param>
    /// <returns>An enumerable of all entities owned by this owner.</returns>
    /// <remarks>
    ///     This uses Arch.Relationships API to directly iterate owned entities.
    /// </remarks>
    public static IEnumerable<Entity> GetOwnedEntities(this Entity owner, World world)
    {
        if (!world.IsAlive(owner) || !owner.HasRelationship<OwnerOf>())
        {
            return Enumerable.Empty<Entity>();
        }

        // Use Arch.Relationships API to iterate owned entities
        ref Relationship<OwnerOf> relationships = ref owner.GetRelationships<OwnerOf>();
        var ownedEntities = new List<Entity>();

        foreach (KeyValuePair<Entity, OwnerOf> kvp in relationships)
        {
            Entity entity = kvp.Key;
            if (world.IsAlive(entity))
            {
                ownedEntities.Add(entity);
            }
        }

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
        if (!owner.HasValue)
        {
            return null;
        }

        // Get the relationship data
        if (!owner.Value.HasRelationship<OwnerOf>(owned))
        {
            return null;
        }

        OwnerOf ownerOfData = owner.Value.GetRelationship<OwnerOf>(owned);
        return ownerOfData.Type;
    }

    #endregion
}
