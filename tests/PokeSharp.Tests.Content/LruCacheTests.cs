using FluentAssertions;
using MonoBallFramework.Game.Engine.Content;
using Xunit;

namespace PokeSharp.Tests.Content;

/// <summary>
///     Unit tests for the LruCache class.
/// </summary>
public class LruCacheTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesCache()
    {
        // Arrange & Act
        var cache = new LruCache<string, int>(10);

        // Assert
        cache.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity)
    {
        // Arrange & Act
        Func<LruCache<string, int>> act = () => new LruCache<string, int>(capacity);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryGet_EmptyCache_ReturnsFalse()
    {
        // Arrange
        var cache = new LruCache<string, string>(10);

        // Act
        bool result = cache.TryGet("nonexistent", out string value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Set_SingleItem_CanRetrieve()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        cache.Set("key1", 42);
        bool result = cache.TryGet("key1", out int value);

        // Assert
        result.Should().BeTrue();
        value.Should().Be(42);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Set_MultipleItems_AllCanBeRetrieved()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGet("key1", out int v1).Should().BeTrue();
        cache.TryGet("key2", out int v2).Should().BeTrue();
        cache.TryGet("key3", out int v3).Should().BeTrue();
        v1.Should().Be(1);
        v2.Should().Be(2);
        v3.Should().Be(3);
    }

    [Fact]
    public void Set_AtCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var cache = new LruCache<string, int>(3);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act - Add fourth item, should evict key1 (LRU)
        cache.Set("key4", 4);

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGet("key1", out _).Should().BeFalse(); // key1 was evicted
        cache.TryGet("key2", out _).Should().BeTrue();
        cache.TryGet("key3", out _).Should().BeTrue();
        cache.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void TryGet_MovesToFront_PreventsEviction()
    {
        // Arrange
        var cache = new LruCache<string, int>(3);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act - Access key1, making it most recently used
        cache.TryGet("key1", out _);

        // Add new item - should evict key2 (now LRU)
        cache.Set("key4", 4);

        // Assert
        cache.TryGet("key1", out _).Should().BeTrue(); // key1 was accessed, not evicted
        cache.TryGet("key2", out _).Should().BeFalse(); // key2 was evicted
        cache.TryGet("key3", out _).Should().BeTrue();
        cache.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void Set_UpdateExisting_UpdatesValueAndMovesToFront()
    {
        // Arrange
        var cache = new LruCache<string, int>(3);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act - Update key1 with new value
        cache.Set("key1", 100);

        // Add new item - should evict key2 (now LRU)
        cache.Set("key4", 4);

        // Assert
        cache.TryGet("key1", out int v1).Should().BeTrue();
        v1.Should().Be(100); // Value was updated
        cache.TryGet("key2", out _).Should().BeFalse(); // key2 was evicted
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
        cache.TryGet("key1", out _).Should().BeFalse();
        cache.TryGet("key2", out _).Should().BeFalse();
        cache.TryGet("key3", out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveWhere_RemovesMatchingItems()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("prefix:key1", 1);
        cache.Set("prefix:key2", 2);
        cache.Set("other:key3", 3);
        cache.Set("other:key4", 4);

        // Act - Remove all keys starting with "prefix:"
        cache.RemoveWhere(key => key.StartsWith("prefix:"));

        // Assert
        cache.Count.Should().Be(2);
        cache.TryGet("prefix:key1", out _).Should().BeFalse();
        cache.TryGet("prefix:key2", out _).Should().BeFalse();
        cache.TryGet("other:key3", out _).Should().BeTrue();
        cache.TryGet("other:key4", out _).Should().BeTrue();
    }

    [Fact]
    public void RemoveWhere_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        Action act = () => cache.RemoveWhere(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Set_NullableValueType_CanStoreNull()
    {
        // Arrange
        var cache = new LruCache<string, string?>(10);

        // Act
        cache.Set("key1", null);
        bool result = cache.TryGet("key1", out string? value);

        // Assert
        result.Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Count_ReturnsCorrectValue()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act & Assert
        cache.Count.Should().Be(0);

        cache.Set("key1", 1);
        cache.Count.Should().Be(1);

        cache.Set("key2", 2);
        cache.Count.Should().Be(2);

        cache.Set("key1", 100); // Update existing
        cache.Count.Should().Be(2);

        cache.Clear();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Concurrent_MultipleThreads_NoExceptions()
    {
        // Arrange
        var cache = new LruCache<int, int>(100);
        var tasks = new List<Task>();

        // Act - Multiple threads reading and writing
        for (int i = 0; i < 10; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    int key = (threadId * 100) + j;
                    cache.Set(key, key * 2);
                    cache.TryGet(key, out _);
                }
            }));
        }

        // Assert - Should complete without exceptions
        Action act = () => Task.WaitAll(tasks.ToArray());
        act.Should().NotThrow();
    }
}
