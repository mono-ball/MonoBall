using System.Collections.Concurrent;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
///     Thread-safe Least Recently Used (LRU) cache implementation.
///     Provides O(1) lookup and eviction of least recently used items when capacity is reached.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache. Must be non-null.</typeparam>
/// <typeparam name="TValue">The type of values in the cache.</typeparam>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>> _dictionary;
    private readonly LinkedList<CacheEntry> _linkedList;
    private readonly Lock _lock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="LruCache{TKey, TValue}" /> class.
    /// </summary>
    /// <param name="capacity">The maximum number of items the cache can hold.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than or equal to zero.</exception>
    public LruCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _dictionary = new ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>>();
        _linkedList = new LinkedList<CacheEntry>();
    }

    /// <summary>
    ///     Gets the current number of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _linkedList.Count;
            }
        }
    }

    /// <summary>
    ///     Attempts to get the value associated with the specified key.
    ///     If successful, the item is moved to the front (most recently used position).
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">
    ///     When this method returns, contains the value associated with the key, if found; otherwise, the
    ///     default value.
    /// </param>
    /// <returns><c>true</c> if the key was found in the cache; otherwise, <c>false</c>.</returns>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_dictionary.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                // Verify node is still in our list (not removed by another operation)
                if (node.List == _linkedList)
                {
                    // Move to front (most recently used)
                    _linkedList.Remove(node);
                    _linkedList.AddFirst(node);
                }

                value = node.Value.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///     Adds or updates a cache entry with the specified key and value.
    ///     If the cache is at capacity, the least recently used item is evicted.
    /// </summary>
    /// <param name="key">The key of the entry to add or update.</param>
    /// <param name="value">The value to associate with the key.</param>
    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            // If key already exists, update it and move to front
            if (_dictionary.TryGetValue(key, out LinkedListNode<CacheEntry>? existingNode))
            {
                existingNode.Value.Value = value;
                _linkedList.Remove(existingNode);
                _linkedList.AddFirst(existingNode);
                return;
            }

            // Check if we need to evict the LRU item
            if (_linkedList.Count >= _capacity)
            {
                LinkedListNode<CacheEntry>? lastNode = _linkedList.Last;
                if (lastNode != null)
                {
                    _dictionary.TryRemove(lastNode.Value.Key, out _);
                    _linkedList.RemoveLast();
                }
            }

            // Add new entry at the front
            var cacheEntry = new CacheEntry(key, value);
            var newNode = new LinkedListNode<CacheEntry>(cacheEntry);
            _linkedList.AddFirst(newNode);
            _dictionary[key] = newNode;
        }
    }

    /// <summary>
    ///     Removes all entries from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _linkedList.Clear();
            _dictionary.Clear();
        }
    }

    /// <summary>
    ///     Removes all cache entries that match the specified predicate.
    /// </summary>
    /// <param name="predicate">A function to test each key for a condition.</param>
    /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
    public void RemoveWhere(Func<TKey, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_lock)
        {
            var keysToRemove = new List<TKey>();
            LinkedListNode<CacheEntry>? currentNode = _linkedList.First;

            while (currentNode != null)
            {
                LinkedListNode<CacheEntry>? nextNode = currentNode.Next;
                if (predicate(currentNode.Value.Key))
                {
                    keysToRemove.Add(currentNode.Value.Key);
                    _linkedList.Remove(currentNode);
                }

                currentNode = nextNode;
            }

            foreach (TKey key in keysToRemove)
            {
                _dictionary.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    ///     Represents a cache entry containing a key-value pair.
    /// </summary>
    private class CacheEntry
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CacheEntry" /> class.
        /// </summary>
        /// <param name="key">The key of the entry.</param>
        /// <param name="value">The value of the entry.</param>
        public CacheEntry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        ///     Gets the key of the cache entry.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        ///     Gets or sets the value of the cache entry.
        /// </summary>
        public TValue Value { get; set; }
    }
}
