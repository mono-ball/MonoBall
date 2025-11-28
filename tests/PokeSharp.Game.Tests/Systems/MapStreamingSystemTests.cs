using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Moq;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;
using Xunit;

namespace PokeSharp.Game.Tests.Systems;

/// <summary>
///     Tests for the map streaming system that handles dynamic loading/unloading of adjacent maps.
///     Verifies boundary detection, map loading triggers, and world offset calculations.
/// </summary>
public class MapStreamingSystemTests : IDisposable
{
    private readonly World _world;
    private const int TILE_SIZE = 16;
    private const float STREAMING_RADIUS = 80f; // 5 tiles

    public MapStreamingSystemTests()
    {
        _world = World.Create();
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    #region Boundary Detection Tests

    [Fact]
    public void DetectBoundary_NorthEdge_ShouldTriggerLoading()
    {
        // Arrange - Player near north edge of map (20x20 tiles)
        var mapId = new MapIdentifier("littleroot_town");
        var mapInfo = new MapInfo(new MapRuntimeId(1), "littleroot_town", 20, 20, TILE_SIZE);
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);

        var mapEntity = _world.Create(mapInfo, mapWorldPos);

        // Player at position (10, 2) - near north edge
        var playerPos = new Position(10, 2); // Tile coordinates, converted to pixels internally
        var streaming = new MapStreaming(mapId);
        var playerEntity = _world.Create(new Player(), playerPos, streaming);

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );
        var isNearEdge = distanceToEdge < STREAMING_RADIUS;
        var isNorthEdge = playerPos.Y < STREAMING_RADIUS;

        // Assert
        isNearEdge.Should().BeTrue("player should be within streaming radius of an edge");
        isNorthEdge.Should().BeTrue("player should be near north edge");
        distanceToEdge
            .Should()
            .BeLessThan(
                STREAMING_RADIUS,
                "distance to nearest edge should be less than streaming radius"
            );
    }

    [Fact]
    public void DetectBoundary_SouthEdge_ShouldTriggerLoading()
    {
        // Arrange - Player near south edge of map (20x20 tiles = 320x320 pixels)
        var mapId = new MapIdentifier("littleroot_town");
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);

        // Player at position (10, 18) - near south edge (320 - 18*16 = 32 pixels from edge)
        var playerPos = new Position(10, 18); // Tile coordinates, converted to pixels internally
        var streaming = new MapStreaming(mapId);
        var playerEntity = _world.Create(new Player(), playerPos, streaming);

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );
        var mapHeightPixels = 20 * TILE_SIZE;
        var distanceToSouth = mapHeightPixels - playerPos.PixelY;

        // Assert
        distanceToSouth
            .Should()
            .BeLessThan(STREAMING_RADIUS, "player should be within streaming radius of south edge");
        distanceToEdge
            .Should()
            .Be(distanceToSouth, "distance to edge should equal distance to south edge");
    }

    [Fact]
    public void DetectBoundary_EastEdge_ShouldTriggerLoading()
    {
        // Arrange - Player near east edge
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);
        var playerPos = new Position(18, 10); // Near east edge (tile coordinates)

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );
        var mapWidthPixels = 20 * TILE_SIZE;
        var distanceToEast = mapWidthPixels - playerPos.PixelX;

        // Assert
        distanceToEast.Should().BeLessThan(STREAMING_RADIUS);
        distanceToEdge.Should().Be(distanceToEast);
    }

    [Fact]
    public void DetectBoundary_WestEdge_ShouldTriggerLoading()
    {
        // Arrange - Player near west edge
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);
        var playerPos = new Position(2, 10); // Near west edge (tile coordinates)

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );

        // Assert
        distanceToEdge.Should().BeLessThan(STREAMING_RADIUS);
        distanceToEdge
            .Should()
            .Be(playerPos.PixelX, "distance should equal distance from west edge (0)");
    }

    [Fact]
    public void DetectBoundary_MapCorner_ShouldDetectNearestEdge()
    {
        // Arrange - Player in northwest corner
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);
        var playerPos = new Position(2, 2); // Tile coordinates

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );

        // Assert
        distanceToEdge.Should().BeLessThan(STREAMING_RADIUS);
        distanceToEdge
            .Should()
            .Be(
                playerPos.PixelX,
                "should return distance to nearest edge (west and north are equidistant)"
            );
    }

    [Fact]
    public void DetectBoundary_MapCenter_ShouldNotTriggerLoading()
    {
        // Arrange - Player in center of map
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);
        var playerPos = new Position(10, 10); // Tile coordinates

        // Act
        var distanceToEdge = mapWorldPos.GetDistanceToEdge(
            new Vector2(playerPos.PixelX, playerPos.PixelY)
        );

        // Assert
        distanceToEdge
            .Should()
            .BeGreaterThan(STREAMING_RADIUS, "center of map should be far from all edges");
    }

    #endregion

    #region Map Loading Tests

    [Fact]
    public void MapStreaming_ShouldTrackLoadedMaps()
    {
        // Arrange
        var currentMapId = new MapIdentifier("littleroot_town");
        var streaming = new MapStreaming(currentMapId);

        // Act
        var adjacentMapId = new MapIdentifier("route101");
        var adjacentOffset = new Vector2(0, -320); // North of current map
        streaming.AddLoadedMap(adjacentMapId, adjacentOffset);

        // Assert
        streaming.LoadedMaps.Should().HaveCount(2);
        streaming.IsMapLoaded(currentMapId).Should().BeTrue();
        streaming.IsMapLoaded(adjacentMapId).Should().BeTrue();
        streaming.GetMapOffset(adjacentMapId).Should().Be(adjacentOffset);
    }

    [Fact]
    public void MapStreaming_ShouldUnloadDistantMaps()
    {
        // Arrange
        var currentMapId = new MapIdentifier("littleroot_town");
        var streaming = new MapStreaming(currentMapId);
        var adjacentMapId = new MapIdentifier("route101");
        streaming.AddLoadedMap(adjacentMapId, new Vector2(0, -320));

        // Act - Unload the adjacent map
        streaming.RemoveLoadedMap(adjacentMapId);

        // Assert
        streaming.IsMapLoaded(adjacentMapId).Should().BeFalse();
        streaming.GetMapOffset(adjacentMapId).Should().BeNull();
        streaming.LoadedMaps.Should().HaveCount(1);
        streaming.LoadedMaps.Should().Contain(currentMapId);
    }

    [Fact]
    public void MapStreaming_MultipleAdjacentMaps_ShouldLoadSimultaneously()
    {
        // Arrange
        var currentMapId = new MapIdentifier("route103");
        var streaming = new MapStreaming(currentMapId);

        // Act - Load maps in all four directions
        streaming.AddLoadedMap(new MapIdentifier("route102"), new Vector2(-800, 0)); // West
        streaming.AddLoadedMap(new MapIdentifier("route110"), new Vector2(0, 352)); // South
        streaming.AddLoadedMap(new MapIdentifier("oldale_town"), new Vector2(0, -320)); // North (hypothetical)

        // Assert
        streaming.LoadedMaps.Should().HaveCount(4, "should track current map + 3 adjacent maps");
        streaming.IsMapLoaded(new MapIdentifier("route102")).Should().BeTrue();
        streaming.IsMapLoaded(new MapIdentifier("route110")).Should().BeTrue();
        streaming.IsMapLoaded(new MapIdentifier("oldale_town")).Should().BeTrue();
    }

    #endregion

    #region World Offset Calculation Tests

    [Fact]
    public void CalculateWorldOffset_NorthConnection_ShouldBeNegativeY()
    {
        // Arrange - Route 101 is north of Littleroot Town
        var littlerootHeight = 20; // tiles
        var connectionOffset = 0; // tiles (aligned)

        // Act
        var worldOffset = CalculateNorthConnectionOffset(
            littlerootHeight,
            connectionOffset,
            TILE_SIZE
        );

        // Assert
        worldOffset
            .Y.Should()
            .Be(-320, "north connection should have negative Y offset (20 tiles * 16 pixels)");
        worldOffset.X.Should().Be(0, "X offset should match connection offset");
    }

    [Fact]
    public void CalculateWorldOffset_SouthConnection_ShouldBePositiveY()
    {
        // Arrange - Route 101 has Oldale Town to the south
        var route101Height = 20; // tiles
        var connectionOffset = 0;

        // Act
        var worldOffset = CalculateSouthConnectionOffset(
            route101Height,
            connectionOffset,
            TILE_SIZE
        );

        // Assert
        worldOffset.Y.Should().Be(320, "south connection should have positive Y offset");
        worldOffset.X.Should().Be(0);
    }

    [Fact]
    public void CalculateWorldOffset_EastConnection_ShouldBePositiveX()
    {
        // Arrange
        var currentMapWidth = 20; // tiles
        var connectionOffset = 0;

        // Act
        var worldOffset = CalculateEastConnectionOffset(
            currentMapWidth,
            connectionOffset,
            TILE_SIZE
        );

        // Assert
        worldOffset.X.Should().Be(320, "east connection should have positive X offset");
        worldOffset.Y.Should().Be(0);
    }

    [Fact]
    public void CalculateWorldOffset_WestConnection_ShouldBeNegativeX()
    {
        // Arrange
        var adjacentMapWidth = 50; // tiles
        var connectionOffset = 0;

        // Act
        var worldOffset = CalculateWestConnectionOffset(
            adjacentMapWidth,
            connectionOffset,
            TILE_SIZE
        );

        // Assert
        worldOffset
            .X.Should()
            .Be(-800, "west connection should have negative X offset (50 tiles * 16 pixels)");
        worldOffset.Y.Should().Be(0);
    }

    [Fact]
    public void CalculateWorldOffset_WithConnectionOffset_ShouldAdjustPosition()
    {
        // Arrange - Connection with offset (not aligned at 0,0)
        var currentMapHeight = 20;
        var connectionOffset = 5; // tiles offset

        // Act
        var worldOffset = CalculateNorthConnectionOffset(
            currentMapHeight,
            connectionOffset,
            TILE_SIZE
        );

        // Assert
        worldOffset.X.Should().Be(80, "X should be offset by 5 tiles * 16 pixels");
        worldOffset.Y.Should().Be(-320, "Y should still be negative for north connection");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MapStreaming_InitialState_ShouldHaveCurrentMapLoaded()
    {
        // Arrange & Act
        var mapId = new MapIdentifier("littleroot_town");
        var streaming = new MapStreaming(mapId);

        // Assert
        streaming.LoadedMaps.Should().ContainSingle();
        streaming.IsMapLoaded(mapId).Should().BeTrue();
        streaming.GetMapOffset(mapId).Should().Be(Vector2.Zero);
    }

    [Fact]
    public void MapWorldPosition_Contains_ShouldValidateBounds()
    {
        // Arrange
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);

        // Act & Assert
        mapWorldPos.Contains(new Vector2(100, 100)).Should().BeTrue();
        mapWorldPos.Contains(new Vector2(-10, 100)).Should().BeFalse("negative X is outside");
        mapWorldPos.Contains(new Vector2(100, -10)).Should().BeFalse("negative Y is outside");
        mapWorldPos.Contains(new Vector2(400, 100)).Should().BeFalse("beyond width");
        mapWorldPos.Contains(new Vector2(100, 400)).Should().BeFalse("beyond height");
    }

    [Fact]
    public void MapWorldPosition_LocalTileToWorld_ShouldConvertCorrectly()
    {
        // Arrange
        var worldOrigin = new Vector2(100, 200);
        var mapWorldPos = new MapWorldPosition(worldOrigin, 20, 20, TILE_SIZE);

        // Act
        var worldPos = mapWorldPos.LocalTileToWorld(5, 10);

        // Assert
        worldPos.X.Should().Be(180, "100 + 5*16 = 180");
        worldPos.Y.Should().Be(360, "200 + 10*16 = 360");
    }

    [Fact]
    public void MapWorldPosition_WorldToLocalTile_ShouldConvertCorrectly()
    {
        // Arrange
        var worldOrigin = new Vector2(100, 200);
        var mapWorldPos = new MapWorldPosition(worldOrigin, 20, 20, TILE_SIZE);

        // Act
        var localTile = mapWorldPos.WorldToLocalTile(new Vector2(180, 360));

        // Assert
        localTile.Should().NotBeNull();
        localTile!.Value.x.Should().Be(5);
        localTile!.Value.y.Should().Be(10);
    }

    [Fact]
    public void MapWorldPosition_WorldToLocalTile_OutsideBounds_ShouldReturnNull()
    {
        // Arrange
        var mapWorldPos = new MapWorldPosition(Vector2.Zero, 20, 20, TILE_SIZE);

        // Act
        var localTile = mapWorldPos.WorldToLocalTile(new Vector2(-10, 100));

        // Assert
        localTile.Should().BeNull("position is outside map bounds");
    }

    [Fact]
    public void MapStreaming_TransitionToNewMap_ShouldUpdateCurrentMapId()
    {
        // Arrange
        var oldMapId = new MapIdentifier("littleroot_town");
        var streaming = new MapStreaming(oldMapId);
        var newMapId = new MapIdentifier("route101");
        streaming.AddLoadedMap(newMapId, new Vector2(0, -320));

        // Act - Simulate player crossing boundary
        streaming.CurrentMapId = newMapId;

        // Assert
        streaming.CurrentMapId.Should().Be(newMapId);
        streaming.IsMapLoaded(oldMapId).Should().BeTrue("old map should still be loaded");
        streaming.IsMapLoaded(newMapId).Should().BeTrue("new map should be loaded");
    }

    #endregion

    #region Helper Methods

    private Vector2 CalculateNorthConnectionOffset(
        int currentMapHeightTiles,
        int offsetTiles,
        int tileSize
    )
    {
        return new Vector2(offsetTiles * tileSize, -(currentMapHeightTiles * tileSize));
    }

    private Vector2 CalculateSouthConnectionOffset(
        int currentMapHeightTiles,
        int offsetTiles,
        int tileSize
    )
    {
        return new Vector2(offsetTiles * tileSize, currentMapHeightTiles * tileSize);
    }

    private Vector2 CalculateEastConnectionOffset(
        int currentMapWidthTiles,
        int offsetTiles,
        int tileSize
    )
    {
        return new Vector2(currentMapWidthTiles * tileSize, offsetTiles * tileSize);
    }

    private Vector2 CalculateWestConnectionOffset(
        int adjacentMapWidthTiles,
        int offsetTiles,
        int tileSize
    )
    {
        return new Vector2(-(adjacentMapWidthTiles * tileSize), offsetTiles * tileSize);
    }

    #endregion
}
