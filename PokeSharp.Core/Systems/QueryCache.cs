using System.Collections.Concurrent;
using Arch.Core;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Centralized cache for QueryDescription instances to avoid repeated allocation.
///     Uses a thread-safe ConcurrentDictionary for lookup.
/// </summary>
/// <remarks>
///     QueryDescriptions are immutable once created, making them perfect for caching.
///     This eliminates the need for every system to maintain its own query fields.
/// </remarks>
public static class QueryCache
{
    private static readonly ConcurrentDictionary<string, QueryDescription> _cache = new();

    /// <summary>
    ///     Gets the count of cached queries.
    /// </summary>
    public static int Count => _cache.Count;

    /// <summary>
    ///     Gets or creates a query description with one component type.
    /// </summary>
    public static QueryDescription Get<T1>()
        where T1 : struct
    {
        var key = typeof(T1).FullName!;
        return _cache.GetOrAdd(key, _ => new QueryDescription().WithAll<T1>());
    }

    /// <summary>
    ///     Gets or creates a query description with two component types.
    /// </summary>
    public static QueryDescription Get<T1, T2>()
        where T1 : struct
        where T2 : struct
    {
        var key = $"{typeof(T1).FullName},{typeof(T2).FullName}";
        return _cache.GetOrAdd(key, _ => new QueryDescription().WithAll<T1, T2>());
    }

    /// <summary>
    ///     Gets or creates a query description with three component types.
    /// </summary>
    public static QueryDescription Get<T1, T2, T3>()
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        var key = $"{typeof(T1).FullName},{typeof(T2).FullName},{typeof(T3).FullName}";
        return _cache.GetOrAdd(key, _ => new QueryDescription().WithAll<T1, T2, T3>());
    }

    /// <summary>
    ///     Gets or creates a query description with required components and excluded components.
    /// </summary>
    public static QueryDescription GetWithNone<TWith, TNone>()
        where TWith : struct
        where TNone : struct
    {
        var key = $"{typeof(TWith).FullName}!{typeof(TNone).FullName}";
        return _cache.GetOrAdd(key, _ => new QueryDescription().WithAll<TWith>().WithNone<TNone>());
    }

    /// <summary>
    ///     Gets or creates a query description with two required components and one excluded component.
    /// </summary>
    public static QueryDescription GetWithNone<T1, T2, TNone>()
        where T1 : struct
        where T2 : struct
        where TNone : struct
    {
        var key = $"{typeof(T1).FullName},{typeof(T2).FullName}!{typeof(TNone).FullName}";
        return _cache.GetOrAdd(
            key,
            _ => new QueryDescription().WithAll<T1, T2>().WithNone<TNone>()
        );
    }

    /// <summary>
    ///     Clears the query cache (primarily for testing).
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }
}
