using FluentAssertions;
using Xunit;

namespace PokeSharp.Game.Maps.Tests.MapConnections;

/// <summary>
/// Minimal reproduction test cases for the 2-tile offset bug.
/// These tests use simplified scenarios to isolate the root cause.
/// </summary>
public class MinimalReproductionTests
{
    private const int TILE_SIZE = 16; // Pixels per tile
    private const int PROBLEMATIC_OFFSET = 2 * TILE_SIZE; // 32 pixels (2 tiles)

    /// <summary>
    /// Minimal Test 1: Simple Two-Map Connection
    ///
    /// Create the simplest possible scenario:
    /// - Map A (10x10 tiles)
    /// - Map B (10x10 tiles)
    /// - Connection at north edge of Map A
    ///
    /// Expected: Player at (5,0) in Map A → (5,9) in Map B
    /// Actual Bug: Player appears at (5,7) in Map B (2 tiles off)
    /// </summary>
    [Fact]
    public void SimpleNorthConnection_ShouldNotHave_Offset()
    {
        // Arrange
        var mapA = CreateTestMap("MapA", width: 10, height: 10);
        var mapB = CreateTestMap("MapB", width: 10, height: 10);

        var connection = CreateConnection(
            source: mapA,
            target: mapB,
            direction: ConnectionDirection.North,
            sourceEdgeTile: 5 // Middle of north edge
        );

        // Expected: When exiting north from MapA at tile X=5,
        // should enter MapB at tile X=5, Y=9 (south edge of MapB)
        var expectedPosition = new TilePosition(5, 9);

        // Act
        var actualPosition = SimulateConnection(connection, startTile: new TilePosition(5, 0));

        // Assert
        actualPosition
            .Should()
            .Be(expectedPosition, "Connection should place player at exact edge of target map");

        // Specific check for the 2-tile offset bug
        var yDifference = expectedPosition.Y - actualPosition.Y;
        yDifference
            .Should()
            .Be(0, $"Should have no Y offset, but has {yDifference} tile offset (bug: -2 tiles)");
    }

    /// <summary>
    /// Minimal Test 2: Simple South Connection
    ///
    /// Tests the opposite direction to see if the bug is bidirectional.
    /// </summary>
    [Fact]
    public void SimpleSouthConnection_ShouldNotHave_Offset()
    {
        // Arrange
        var mapA = CreateTestMap("MapA", width: 10, height: 10);
        var mapB = CreateTestMap("MapB", width: 10, height: 10);

        var connection = CreateConnection(
            source: mapA,
            target: mapB,
            direction: ConnectionDirection.South,
            sourceEdgeTile: 5
        );

        var expectedPosition = new TilePosition(5, 0);

        // Act
        var actualPosition = SimulateConnection(connection, startTile: new TilePosition(5, 9));

        // Assert
        actualPosition
            .Should()
            .Be(
                expectedPosition,
                "South connection should place player at north edge of target map"
            );

        var yDifference = actualPosition.Y - expectedPosition.Y;
        yDifference
            .Should()
            .Be(0, $"Should have no Y offset, but has {yDifference} tile offset (bug: +2 tiles)");
    }

    /// <summary>
    /// Minimal Test 3: East/West Connections
    ///
    /// Verify if the offset bug also affects horizontal connections.
    /// </summary>
    [Theory]
    [InlineData(ConnectionDirection.East, 9, 5, 0, 5, "East connection X offset")]
    [InlineData(ConnectionDirection.West, 0, 5, 9, 5, "West connection X offset")]
    public void HorizontalConnections_ShouldNotHave_Offset(
        ConnectionDirection direction,
        int startX,
        int startY,
        int expectedX,
        int expectedY,
        string testName
    )
    {
        // Arrange
        var mapA = CreateTestMap("MapA", width: 10, height: 10);
        var mapB = CreateTestMap("MapB", width: 10, height: 10);

        var connection = CreateConnection(
            source: mapA,
            target: mapB,
            direction: direction,
            sourceEdgeTile: 5
        );

        var startPosition = new TilePosition(startX, startY);
        var expectedPosition = new TilePosition(expectedX, expectedY);

        // Act
        var actualPosition = SimulateConnection(connection, startPosition);

        // Assert
        actualPosition.Should().Be(expectedPosition, testName);

        // Check for horizontal offset bug
        var xDifference = Math.Abs(expectedPosition.X - actualPosition.X);
        xDifference.Should().Be(0, $"Should have no X offset, but has {xDifference} tile offset");
    }

    /// <summary>
    /// Minimal Test 4: Pixel-Level Offset Detection
    ///
    /// Tests at pixel level to detect sub-tile offsets.
    /// </summary>
    [Fact]
    public void PixelLevel_Connection_ShouldBe_Exact()
    {
        // Arrange
        const int mapSizePixels = 160; // 10 tiles * 16 pixels

        var mapA = CreateTestMap("MapA", width: 10, height: 10);
        var mapB = CreateTestMap("MapB", width: 10, height: 10);

        var connection = CreateConnection(
            source: mapA,
            target: mapB,
            direction: ConnectionDirection.North,
            sourceEdgeTile: 5
        );

        // Start at pixel position (80, 0) - middle of north edge
        var startPixel = new PixelPosition(80, 0);

        // Should end at pixel position (80, 144) - middle of south edge of MapB
        var expectedPixel = new PixelPosition(80, mapSizePixels - TILE_SIZE);

        // Act
        var actualPixel = SimulateConnectionPixels(connection, startPixel);

        // Assert
        actualPixel
            .Should()
            .Be(expectedPixel, "Pixel-perfect alignment required at connection point");

        // Detect the specific 32-pixel offset bug
        var pixelOffsetY = expectedPixel.Y - actualPixel.Y;
        pixelOffsetY
            .Should()
            .Be(
                0,
                $"Should have no pixel offset, but has {pixelOffsetY} pixel offset (bug: -32 pixels)"
            );

        if (Math.Abs(pixelOffsetY) == PROBLEMATIC_OFFSET)
        {
            throw new Exception(
                $"CONFIRMED BUG: Detected exactly {PROBLEMATIC_OFFSET} pixel offset "
                    + $"({PROBLEMATIC_OFFSET / TILE_SIZE} tiles) in connection logic"
            );
        }
    }

    /// <summary>
    /// Minimal Test 5: Connection Offset Math Verification
    ///
    /// Tests the underlying mathematics of offset calculation.
    /// </summary>
    [Fact]
    public void ConnectionOffset_Calculation_ShouldBe_Correct()
    {
        // Arrange
        var sourceMap = CreateTestMap("Source", width: 20, height: 15);
        var targetMap = CreateTestMap("Target", width: 20, height: 15);

        // Connection from source north edge to target south edge
        var connectionPoint = 10; // Tile 10 on the edge

        // Expected offsets
        var expectedSourceOffset = new TilePosition(connectionPoint, 0);
        var expectedTargetOffset = new TilePosition(connectionPoint, targetMap.Height - 1);

        // Act
        var actualOffsets = CalculateConnectionOffsets(
            sourceMap,
            targetMap,
            ConnectionDirection.North,
            connectionPoint
        );

        // Assert
        actualOffsets
            .SourceOffset.Should()
            .Be(expectedSourceOffset, "Source offset should be at connection point on edge");

        actualOffsets
            .TargetOffset.Should()
            .Be(expectedTargetOffset, "Target offset should be at opposite edge");

        // Verify the offset difference
        var offsetDifference = CalculateOffsetDifference(
            actualOffsets.SourceOffset,
            actualOffsets.TargetOffset,
            sourceMap.Height,
            targetMap.Height
        );

        offsetDifference
            .Should()
            .Be(0, "Offset calculation should account for both map dimensions with no remainder");
    }

    #region Test Data Structures

    public record TestMap(string Name, int Width, int Height);

    public record MapConnection(
        TestMap Source,
        TestMap Target,
        ConnectionDirection Direction,
        int EdgeTile
    );

    public record struct TilePosition(int X, int Y);

    public record struct PixelPosition(int X, int Y);

    public record ConnectionOffsets(TilePosition SourceOffset, TilePosition TargetOffset);

    public enum ConnectionDirection
    {
        North,
        South,
        East,
        West,
    }

    #endregion

    #region Helper Methods

    private TestMap CreateTestMap(string name, int width, int height)
    {
        return new TestMap(name, width, height);
    }

    private MapConnection CreateConnection(
        TestMap source,
        TestMap target,
        ConnectionDirection direction,
        int sourceEdgeTile
    )
    {
        return new MapConnection(source, target, direction, sourceEdgeTile);
    }

    /// <summary>
    /// Simulates moving through a connection and returns the destination tile.
    /// TODO: Replace with actual game logic implementation.
    /// </summary>
    private TilePosition SimulateConnection(MapConnection connection, TilePosition startTile)
    {
        // Placeholder implementation - replace with actual game logic
        return connection.Direction switch
        {
            ConnectionDirection.North => new TilePosition(
                startTile.X,
                connection.Target.Height - 1
            ),
            ConnectionDirection.South => new TilePosition(startTile.X, 0),
            ConnectionDirection.East => new TilePosition(0, startTile.Y),
            ConnectionDirection.West => new TilePosition(connection.Target.Width - 1, startTile.Y),
            _ => throw new ArgumentException("Invalid direction"),
        };
    }

    /// <summary>
    /// Simulates connection at pixel level for precise offset detection.
    /// TODO: Replace with actual game logic implementation.
    /// </summary>
    private PixelPosition SimulateConnectionPixels(
        MapConnection connection,
        PixelPosition startPixel
    )
    {
        // Placeholder implementation
        return new PixelPosition(startPixel.X, (connection.Target.Height - 1) * TILE_SIZE);
    }

    /// <summary>
    /// Calculates the connection offsets between two maps.
    /// TODO: Replace with actual game logic implementation.
    /// </summary>
    private ConnectionOffsets CalculateConnectionOffsets(
        TestMap source,
        TestMap target,
        ConnectionDirection direction,
        int connectionPoint
    )
    {
        // Placeholder implementation
        return direction switch
        {
            ConnectionDirection.North => new ConnectionOffsets(
                new TilePosition(connectionPoint, 0),
                new TilePosition(connectionPoint, target.Height - 1)
            ),
            ConnectionDirection.South => new ConnectionOffsets(
                new TilePosition(connectionPoint, source.Height - 1),
                new TilePosition(connectionPoint, 0)
            ),
            _ => throw new ArgumentException("Invalid direction"),
        };
    }

    /// <summary>
    /// Calculates the offset difference to detect any miscalculation.
    /// </summary>
    private int CalculateOffsetDifference(
        TilePosition sourceOffset,
        TilePosition targetOffset,
        int sourceHeight,
        int targetHeight
    )
    {
        // The difference should be exactly the sum of the map dimensions minus 1
        var expectedDifference = sourceHeight + targetHeight - 1;
        var actualDifference = Math.Abs(sourceOffset.Y - targetOffset.Y);
        return expectedDifference - actualDifference;
    }

    #endregion
}
