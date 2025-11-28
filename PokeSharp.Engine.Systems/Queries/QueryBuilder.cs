using Arch.Core;

namespace PokeSharp.Engine.Systems.Queries;

/// <summary>
///     Fluent builder for constructing ECS queries at runtime.
///     Provides a type-safe, composable API for building QueryDescription instances.
/// </summary>
/// <remarks>
///     <para>
///         Use this builder when you need to construct queries dynamically at runtime.
///         For static, commonly-used queries, prefer the centralized <see cref="Queries" /> class
///         to eliminate per-frame allocations.
///     </para>
///     <para>
///         <b>Usage Example:</b>
///     </para>
///     <code>
///         var query = new QueryBuilder()
///             .WithAll&lt;Position, Sprite&gt;()
///             .WithNone&lt;Player&gt;()
///             .Build();
///
///         world.Query(in query, (ref Position pos, ref Sprite sprite) => { });
///     </code>
/// </remarks>
public class QueryBuilder
{
    private QueryDescription _query = new();

    /// <summary>
    ///     Requires entity to have component T1.
    /// </summary>
    /// <typeparam name="T1">Component type to require.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithAll<T1>()
        where T1 : struct
    {
        _query = _query.WithAll<T1>();
        return this;
    }

    /// <summary>
    ///     Requires entity to have components T1 and T2.
    /// </summary>
    /// <typeparam name="T1">First component type to require.</typeparam>
    /// <typeparam name="T2">Second component type to require.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithAll<T1, T2>()
        where T1 : struct
        where T2 : struct
    {
        _query = _query.WithAll<T1, T2>();
        return this;
    }

    /// <summary>
    ///     Requires entity to have components T1, T2, and T3.
    /// </summary>
    /// <typeparam name="T1">First component type to require.</typeparam>
    /// <typeparam name="T2">Second component type to require.</typeparam>
    /// <typeparam name="T3">Third component type to require.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithAll<T1, T2, T3>()
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        _query = _query.WithAll<T1, T2, T3>();
        return this;
    }

    /// <summary>
    ///     Requires entity to have components T1, T2, T3, and T4.
    /// </summary>
    /// <typeparam name="T1">First component type to require.</typeparam>
    /// <typeparam name="T2">Second component type to require.</typeparam>
    /// <typeparam name="T3">Third component type to require.</typeparam>
    /// <typeparam name="T4">Fourth component type to require.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithAll<T1, T2, T3, T4>()
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
    {
        _query = _query.WithAll<T1, T2, T3, T4>();
        return this;
    }

    /// <summary>
    ///     Requires entity to NOT have component T.
    ///     Useful for excluding certain entity types from queries.
    /// </summary>
    /// <typeparam name="T">Component type to exclude.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithNone<T>()
        where T : struct
    {
        _query = _query.WithNone<T>();
        return this;
    }

    /// <summary>
    ///     Requires entity to NOT have component T1 or T2.
    /// </summary>
    /// <typeparam name="T1">First component type to exclude.</typeparam>
    /// <typeparam name="T2">Second component type to exclude.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithNone<T1, T2>()
        where T1 : struct
        where T2 : struct
    {
        _query = _query.WithNone<T1, T2>();
        return this;
    }

    /// <summary>
    ///     Requires entity to have at least one of: T1, T2, or T3.
    /// </summary>
    /// <typeparam name="T1">First component type option.</typeparam>
    /// <typeparam name="T2">Second component type option.</typeparam>
    /// <typeparam name="T3">Third component type option.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder WithAny<T1, T2, T3>()
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        _query = _query.WithAny<T1, T2, T3>();
        return this;
    }

    /// <summary>
    ///     Builds the final QueryDescription.
    ///     Can be called multiple times to create multiple query instances from same builder.
    /// </summary>
    /// <returns>The constructed QueryDescription.</returns>
    public QueryDescription Build()
    {
        return _query;
    }

    /// <summary>
    ///     Resets the builder to its initial state.
    ///     Useful for reusing the same builder instance for multiple queries.
    /// </summary>
    /// <returns>This builder for method chaining.</returns>
    public QueryBuilder Reset()
    {
        _query = new QueryDescription();
        return this;
    }
}
