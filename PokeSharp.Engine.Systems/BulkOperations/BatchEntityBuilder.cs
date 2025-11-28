using System.Reflection;
using Arch.Core;

namespace PokeSharp.Engine.Systems.BulkOperations;

/// <summary>
///     Fluent API for building multiple entities with the same configuration.
///     Provides a clean, readable way to create batches of entities with shared
///     and per-entity components.
/// </summary>
/// <remarks>
///     <para>
///         This builder is designed for scenarios where you need many entities
///         with similar configurations but some variation. It optimizes for:
///         - Readability through fluent API
///         - Performance by batching creation
///         - Flexibility with factory functions
///     </para>
///     <example>
///         <code>
/// // Create 100 projectiles with shared and unique properties
/// var projectiles = new BatchEntityBuilder(world)
///     .WithCount(100)
///     .WithSharedComponent(new ProjectileTag())
///     .WithSharedComponent(new Damage { Amount = 10 })
///     .WithComponentFactory(i => new Position(
///         playerPos.X + Random.Shared.Next(-10, 10),
///         playerPos.Y + Random.Shared.Next(-10, 10)
///     ))
///     .WithComponentFactory(i => new Velocity(
///         MathF.Cos(i * MathF.PI * 2 / 100) * 5,
///         MathF.Sin(i * MathF.PI * 2 / 100) * 5
///     ))
///     .Build();
/// </code>
///     </example>
/// </remarks>
public sealed class BatchEntityBuilder
{
    private readonly List<(Type type, Delegate factory)> _componentFactories = new();
    private readonly List<(Type type, object component)> _sharedComponents = new();
    private readonly World _world;
    private int _count = 1;

    /// <summary>
    ///     Creates a new batch entity builder for the specified world.
    /// </summary>
    /// <param name="world">The Arch ECS world to create entities in</param>
    public BatchEntityBuilder(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    ///     Set the number of entities to create in the batch.
    /// </summary>
    /// <param name="count">Number of entities (must be positive)</param>
    /// <returns>This builder for chaining</returns>
    /// <example>
    ///     <code>
    /// var entities = new BatchEntityBuilder(world)
    ///     .WithCount(50)
    ///     .WithSharedComponent(new EnemyTag())
    ///     .Build();
    /// </code>
    /// </example>
    public BatchEntityBuilder WithCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        _count = count;
        return this;
    }

    /// <summary>
    ///     Add a component that will be identical for all entities.
    ///     Use this for components that don't vary between entities.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="component">Component value to share</param>
    /// <returns>This builder for chaining</returns>
    /// <example>
    ///     <code>
    /// builder
    ///     .WithSharedComponent(new Health { MaxHP = 100, CurrentHP = 100 })
    ///     .WithSharedComponent(new Speed { Value = 2.5f });
    /// </code>
    /// </example>
    public BatchEntityBuilder WithSharedComponent<T>(T component)
        where T : struct
    {
        _sharedComponents.Add((typeof(T), component));
        return this;
    }

    /// <summary>
    ///     Add a component with different values per entity using a factory function.
    ///     The factory receives the entity index (0 to count-1).
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="factory">Factory function that creates component per index</param>
    /// <returns>This builder for chaining</returns>
    /// <example>
    ///     <code>
    /// // Create entities in a grid pattern
    /// builder.WithComponentFactory&lt;Position&gt;(i => new Position(
    ///     (i % 10) * 32,  // X: 10 columns
    ///     (i / 10) * 32   // Y: rows
    /// ));
    /// </code>
    /// </example>
    public BatchEntityBuilder WithComponentFactory<T>(Func<int, T> factory)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(factory);
        _componentFactories.Add((typeof(T), factory));
        return this;
    }

    /// <summary>
    ///     Build all entities at once with the configured components.
    /// </summary>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// var enemies = new BatchEntityBuilder(world)
    ///     .WithCount(20)
    ///     .WithSharedComponent(new Enemy { Type = EnemyType.Slime })
    ///     .WithComponentFactory&lt;Position&gt;(i => RandomPosition())
    ///     .Build();
    /// </code>
    /// </example>
    public Entity[] Build()
    {
        var entities = new Entity[_count];

        for (int i = 0; i < _count; i++)
        {
            // Create entity
            Entity entity = _world.Create();

            // Add shared components (same for all entities)
            foreach ((Type type, object component) in _sharedComponents)
            {
                AddComponentDynamic(entity, type, component);
            }

            // Add factory-generated components (unique per entity)
            foreach ((Type type, Delegate factory) in _componentFactories)
            {
                object? component = factory.DynamicInvoke(i);
                if (component != null)
                {
                    AddComponentDynamic(entity, type, component);
                }
            }

            entities[i] = entity;
        }

        return entities;
    }

    /// <summary>
    ///     Build all entities and apply a custom configuration action to each.
    ///     Useful for additional per-entity setup that doesn't fit the factory pattern.
    /// </summary>
    /// <param name="configure">Action to apply to each entity with its index</param>
    /// <returns>Array of created entities</returns>
    /// <example>
    ///     <code>
    /// var entities = new BatchEntityBuilder(world)
    ///     .WithCount(10)
    ///     .WithSharedComponent(new NPC())
    ///     .Build((entity, index) =>
    ///     {
    ///         // Custom per-entity logic
    ///         if (index == 0)
    ///             entity.Add(new LeaderTag());
    ///     });
    /// </code>
    /// </example>
    public Entity[] Build(Action<Entity, int> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        Entity[] entities = Build();

        for (int i = 0; i < entities.Length; i++)
        {
            configure(entities[i], i);
        }

        return entities;
    }

    /// <summary>
    ///     Clear all configuration and reset to initial state.
    ///     Allows builder reuse for different batches.
    /// </summary>
    /// <returns>This builder for chaining</returns>
    public BatchEntityBuilder Clear()
    {
        _count = 1;
        _sharedComponents.Clear();
        _componentFactories.Clear();
        return this;
    }

    /// <summary>
    ///     Add component to entity dynamically using reflection.
    ///     Required because Arch needs compile-time generic types.
    /// </summary>
    private void AddComponentDynamic(Entity entity, Type componentType, object component)
    {
        MethodInfo? addMethod = typeof(Entity)
            .GetMethods()
            .Where(m => m.Name == "Add" && m.IsGenericMethod)
            .FirstOrDefault(m =>
            {
                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType.IsByRef;
            });

        if (addMethod == null)
        {
            throw new InvalidOperationException(
                $"Could not find Entity.Add<T> method for component type {componentType.Name}"
            );
        }

        MethodInfo genericAdd = addMethod.MakeGenericMethod(componentType);
        genericAdd.Invoke(entity, [component]);
    }
}
