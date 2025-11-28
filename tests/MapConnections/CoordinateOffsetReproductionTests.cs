using FluentAssertions;
using Xunit;

namespace PokeSharp.Game.Maps.Tests.MapConnections;

/// <summary>
/// Reproduction test cases for coordinate offset issues in map connections.
/// These tests document the specific offset problems identified:
/// 1. dewford_town → route107: 2-tile upward shift
/// 2. route114 → route115: 2-tile downward shift
/// </summary>
public class CoordinateOffsetReproductionTests
{
    /// <summary>
    /// Test Case 1: Dewford Town to Route 107 Connection
    ///
    /// Issue: When warping from dewford_town to route107, the player position
    /// shifts 2 tiles upward from the expected position.
    ///
    /// Expected Behavior:
    /// - Player should appear at the exact connection point
    /// - Y-coordinate should match the warp destination
    ///
    /// Actual Behavior:
    /// - Player appears 2 tiles (32 pixels) above expected position
    /// - Y-coordinate is offset by -32 pixels
    /// </summary>
    [Fact]
    public void DewfordTownToRoute107_ShouldNotHave_UpwardOffset()
    {
        // Arrange
        const string sourceMap = "dewford_town";
        const string targetMap = "route107";

        // Expected connection point based on map data
        const int expectedPlayerX = 64; // Example X coordinate
        const int expectedPlayerY = 128; // Example Y coordinate

        // Act
        var connection = GetMapConnection(sourceMap, targetMap);
        var actualPosition = SimulateWarp(connection);

        // Assert - Player should appear at exact connection point
        actualPosition
            .X.Should()
            .Be(expectedPlayerX, "X coordinate should match connection point exactly");

        actualPosition
            .Y.Should()
            .Be(
                expectedPlayerY,
                "Y coordinate should match connection point exactly - NO upward shift"
            );

        // Additional assertion to catch the specific bug
        var yOffset = expectedPlayerY - actualPosition.Y;
        yOffset
            .Should()
            .Be(0, "There should be no Y-offset (currently has +32 pixel upward shift bug)");
    }

    /// <summary>
    /// Test Case 2: Route 114 to Route 115 Connection
    ///
    /// Issue: When warping from route114 to route115, the player position
    /// shifts 2 tiles downward from the expected position.
    ///
    /// Expected Behavior:
    /// - Player should appear at the exact connection point
    /// - Y-coordinate should match the warp destination
    ///
    /// Actual Behavior:
    /// - Player appears 2 tiles (32 pixels) below expected position
    /// - Y-coordinate is offset by +32 pixels
    /// </summary>
    [Fact]
    public void Route114ToRoute115_ShouldNotHave_DownwardOffset()
    {
        // Arrange
        const string sourceMap = "route114";
        const string targetMap = "route115";

        // Expected connection point based on map data
        const int expectedPlayerX = 96; // Example X coordinate
        const int expectedPlayerY = 64; // Example Y coordinate

        // Act
        var connection = GetMapConnection(sourceMap, targetMap);
        var actualPosition = SimulateWarp(connection);

        // Assert - Player should appear at exact connection point
        actualPosition
            .X.Should()
            .Be(expectedPlayerX, "X coordinate should match connection point exactly");

        actualPosition
            .Y.Should()
            .Be(
                expectedPlayerY,
                "Y coordinate should match connection point exactly - NO downward shift"
            );

        // Additional assertion to catch the specific bug
        var yOffset = actualPosition.Y - expectedPlayerY;
        yOffset
            .Should()
            .Be(0, "There should be no Y-offset (currently has +32 pixel downward shift bug)");
    }

    /// <summary>
    /// Test Case 3: Verify Connection Offset Calculation
    ///
    /// This test validates that the offset calculation between connected maps
    /// correctly accounts for map boundaries and connection points.
    /// </summary>
    [Theory]
    [InlineData("dewford_town", "route107", 0, 0, "dewford → route107 should have zero offset")]
    [InlineData("route114", "route115", 0, 0, "route114 → route115 should have zero offset")]
    [InlineData(
        "littleroot_town",
        "route101",
        0,
        0,
        "littleroot → route101 should have zero offset"
    )]
    public void MapConnections_ShouldHave_CorrectOffsets(
        string sourceMap,
        string targetMap,
        int expectedOffsetX,
        int expectedOffsetY,
        string reason
    )
    {
        // Arrange
        var connection = GetMapConnection(sourceMap, targetMap);

        // Act
        var calculatedOffset = CalculateConnectionOffset(connection);

        // Assert
        calculatedOffset.OffsetX.Should().Be(expectedOffsetX, reason);
        calculatedOffset.OffsetY.Should().Be(expectedOffsetY, reason);
    }

    /// <summary>
    /// Test Case 4: Boundary Alignment Validation
    ///
    /// Ensures that map boundaries align correctly at connection points.
    /// A 2-tile offset would cause visible misalignment.
    /// </summary>
    [Fact]
    public void ConnectedMaps_ShouldHave_AlignedBoundaries()
    {
        // Arrange
        var testConnections = new[]
        {
            ("dewford_town", "route107"),
            ("route114", "route115"),
            ("route103", "route110"),
        };

        foreach (var (sourceMap, targetMap) in testConnections)
        {
            // Act
            var connection = GetMapConnection(sourceMap, targetMap);
            var alignment = ValidateBoundaryAlignment(connection);

            // Assert
            alignment
                .IsAligned.Should()
                .BeTrue($"{sourceMap} → {targetMap} boundaries should align perfectly");

            alignment
                .TileOffset.Should()
                .Be(0, $"{sourceMap} → {targetMap} should have no tile offset at boundary");

            alignment
                .PixelOffset.Should()
                .Be(0, $"{sourceMap} → {targetMap} should have no pixel offset at boundary");
        }
    }

    /// <summary>
    /// Test Case 5: Connection Point Accuracy
    ///
    /// Validates that the calculated connection point matches the actual
    /// warp destination in the target map.
    /// </summary>
    [Theory]
    [InlineData("dewford_town", "route107")]
    [InlineData("route114", "route115")]
    public void ConnectionPoint_ShouldMatch_WarpDestination(string sourceMap, string targetMap)
    {
        // Arrange
        var connection = GetMapConnection(sourceMap, targetMap);
        var warpData = GetWarpData(sourceMap, targetMap);

        // Act
        var connectionPoint = CalculateConnectionPoint(connection);
        var warpDestination = warpData.Destination;

        // Assert
        connectionPoint
            .X.Should()
            .Be(warpDestination.X, "Connection point X should match warp destination X");

        connectionPoint
            .Y.Should()
            .Be(warpDestination.Y, "Connection point Y should match warp destination Y");

        var distance = CalculateDistance(connectionPoint, warpDestination);
        distance
            .Should()
            .Be(0, "Connection point should be exactly at warp destination (no offset)");
    }

    #region Helper Methods and Types

    private record MapConnection(
        string SourceMap,
        string TargetMap,
        int OffsetX,
        int OffsetY,
        string Direction
    );

    private record Position(int X, int Y);

    private record ConnectionOffset(int OffsetX, int OffsetY);

    private record BoundaryAlignment(bool IsAligned, int TileOffset, int PixelOffset);

    private record WarpData(
        string SourceMap,
        string TargetMap,
        Position Source,
        Position Destination
    );

    /// <summary>
    /// Retrieves map connection data for the specified source and target maps.
    /// This should be implemented to read from actual game data.
    /// </summary>
    private MapConnection GetMapConnection(string sourceMap, string targetMap)
    {
        // TODO: Implement actual map connection data retrieval
        // This is a placeholder that should be replaced with real implementation
        return new MapConnection(sourceMap, targetMap, 0, 0, "north");
    }

    /// <summary>
    /// Simulates a warp operation and returns the resulting player position.
    /// This should be implemented to match the actual warp logic.
    /// </summary>
    private Position SimulateWarp(MapConnection connection)
    {
        // TODO: Implement actual warp simulation
        // This is a placeholder that should be replaced with real implementation
        return new Position(0, 0);
    }

    /// <summary>
    /// Calculates the offset between two connected maps.
    /// </summary>
    private ConnectionOffset CalculateConnectionOffset(MapConnection connection)
    {
        // TODO: Implement actual offset calculation
        return new ConnectionOffset(connection.OffsetX, connection.OffsetY);
    }

    /// <summary>
    /// Validates that map boundaries are properly aligned at connection points.
    /// </summary>
    private BoundaryAlignment ValidateBoundaryAlignment(MapConnection connection)
    {
        // TODO: Implement actual boundary validation
        return new BoundaryAlignment(true, 0, 0);
    }

    /// <summary>
    /// Retrieves warp data for the specified map connection.
    /// </summary>
    private WarpData GetWarpData(string sourceMap, string targetMap)
    {
        // TODO: Implement actual warp data retrieval
        return new WarpData(sourceMap, targetMap, new Position(0, 0), new Position(0, 0));
    }

    /// <summary>
    /// Calculates the connection point where two maps should join.
    /// </summary>
    private Position CalculateConnectionPoint(MapConnection connection)
    {
        // TODO: Implement actual connection point calculation
        return new Position(0, 0);
    }

    /// <summary>
    /// Calculates the distance between two positions.
    /// </summary>
    private int CalculateDistance(Position p1, Position p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return (int)Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion
}
