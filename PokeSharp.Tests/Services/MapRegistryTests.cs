using FluentAssertions;
using PokeSharp.Core.Services;
using Xunit;

namespace PokeSharp.Tests.Services;

public class MapRegistryTests
{
    private readonly MapRegistry _registry;

    public MapRegistryTests()
    {
        _registry = new MapRegistry();
    }

    [Fact]
    public void GetOrCreateMapId_NewMap_ReturnsUniqueId()
    {
        // Arrange
        const string mapName = "TestMap";

        // Act
        var mapId = _registry.GetOrCreateMapId(mapName);

        // Assert
        mapId.Should().BeGreaterThanOrEqualTo(0, "map ID should be a non-negative integer");
        _registry.GetMapName(mapId).Should().Be(mapName);
    }

    [Fact]
    public void GetOrCreateMapId_ExistingMap_ReturnsSameId()
    {
        // Arrange
        const string mapName = "TestMap";
        var firstId = _registry.GetOrCreateMapId(mapName);

        // Act
        var secondId = _registry.GetOrCreateMapId(mapName);

        // Assert
        secondId.Should().Be(firstId, "existing map should return the same ID");
    }

    [Fact]
    public void GetOrCreateMapId_Concurrent_NoRaceConditions()
    {
        // Arrange
        const string mapName = "ConcurrentMap";
        const int threadCount = 100;
        var ids = new int[threadCount];
        var tasks = new Task[threadCount];

        // Act - Create multiple threads trying to get/create the same map ID simultaneously
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                ids[index] = _registry.GetOrCreateMapId(mapName);
            });
        }

        Task.WaitAll(tasks);

        // Assert
        ids.Distinct().Should().HaveCount(1, "all threads should receive the same map ID");
        ids.All(id => id >= 0).Should().BeTrue("all IDs should be valid non-negative integers");
    }

    [Fact]
    public void MarkMapLoaded_AddsToLoadedMaps()
    {
        // Arrange
        const string mapName = "LoadedMap";
        var mapId = _registry.GetOrCreateMapId(mapName);

        // Act
        _registry.MarkMapLoaded(mapId);

        // Assert
        _registry.IsMapLoaded(mapId).Should().BeTrue("map should be marked as loaded");
    }

    [Fact]
    public void GetMapName_ReturnsCorrectName()
    {
        // Arrange
        const string mapName = "NamedMap";
        var mapId = _registry.GetOrCreateMapId(mapName);

        // Act
        var retrievedName = _registry.GetMapName(mapId);

        // Assert
        retrievedName.Should().Be(mapName, "registry should return the correct map name for the given ID");
    }

    [Fact]
    public void GetMapName_UnknownId_ReturnsNull()
    {
        // Arrange
        const int unknownId = 99999;

        // Act
        var retrievedName = _registry.GetMapName(unknownId);

        // Assert
        retrievedName.Should().BeNull("unknown map ID should return null");
    }

    [Fact]
    public void IsMapLoaded_UnloadedMap_ReturnsFalse()
    {
        // Arrange
        const string mapName = "UnloadedMap";
        var mapId = _registry.GetOrCreateMapId(mapName);

        // Act
        var isLoaded = _registry.IsMapLoaded(mapId);

        // Assert
        isLoaded.Should().BeFalse("newly created map should not be marked as loaded");
    }

    [Fact]
    public void GetOrCreateMapId_DifferentMaps_ReturnsDifferentIds()
    {
        // Arrange
        const string mapName1 = "Map1";
        const string mapName2 = "Map2";

        // Act
        var id1 = _registry.GetOrCreateMapId(mapName1);
        var id2 = _registry.GetOrCreateMapId(mapName2);

        // Assert
        id1.Should().NotBe(id2, "different maps should have different IDs");
    }
}
