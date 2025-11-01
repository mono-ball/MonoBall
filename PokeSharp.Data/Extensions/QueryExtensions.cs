using Microsoft.EntityFrameworkCore;
using PokeSharp.Data.Caching;

namespace PokeSharp.Data.Extensions;

/// <summary>
/// Generic query extension methods for common EF Core operations.
/// Provides caching, pagination, and filtering helpers.
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// Get entity by ID with caching support.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="dbSet">DbSet to query</param>
    /// <param name="id">Entity ID</param>
    /// <param name="cache">Cache service</param>
    /// <param name="cacheKeyFunc">Function to generate cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Entity or null if not found</returns>
    public static async Task<TEntity?> GetByIdCachedAsync<TEntity>(
        this DbSet<TEntity> dbSet,
        int id,
        CacheService cache,
        Func<int, string> cacheKeyFunc,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(cache, nameof(cache));
        ArgumentNullException.ThrowIfNull(cacheKeyFunc, nameof(cacheKeyFunc));

        var cacheKey = cacheKeyFunc(id);

        return await cache.GetOrCreateAsync(
            cacheKey,
            async () => await dbSet.FindAsync(new object[] { id }, cancellationToken),
            new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });
    }

    /// <summary>
    /// Get all entities with caching support.
    /// WARNING: Only use for small datasets (< 1000 entities).
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="query">IQueryable to execute</param>
    /// <param name="cache">Cache service</param>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entities</returns>
    public static async Task<List<TEntity>> ToListCachedAsync<TEntity>(
        this IQueryable<TEntity> query,
        CacheService cache,
        string cacheKey,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(cache, nameof(cache));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey, nameof(cacheKey));

        return await cache.GetOrCreateAsync(
            cacheKey,
            async () => await query.ToListAsync(cancellationToken),
            new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15)
            });
    }

    /// <summary>
    /// Paginate a query with optional caching.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="query">IQueryable to paginate</param>
    /// <param name="pageNumber">Page number (1-indexed)</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>Paginated result</returns>
    public static async Task<PaginatedResult<TEntity>> ToPaginatedAsync<TEntity>(
        this IQueryable<TEntity> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be >= 1", nameof(pageNumber));
        if (pageSize < 1 || pageSize > 100) throw new ArgumentException("Page size must be between 1 and 100", nameof(pageSize));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<TEntity>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}

/// <summary>
/// Result of a paginated query.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public sealed class PaginatedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
