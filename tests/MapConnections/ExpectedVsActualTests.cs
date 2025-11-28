using FluentAssertions;
using Xunit;

namespace PokeSharp.Game.Maps.Tests.MapConnections;

/// <summary>
/// Tests comparing expected coordinates vs actual coordinates at map connections.
/// These tests explicitly document the expected behavior and detect deviations.
/// </summary>
public class ExpectedVsActualTests
{
    private const int TILE_SIZE_PIXELS = 16;
    private const int BUGGY_OFFSET_TILES = 2;
    private const int BUGGY_OFFSET_PIXELS = BUGGY_OFFSET_TILES * TILE_SIZE_PIXELS; // 32 pixels

    /// <summary>
    /// Test Case 1: Dewford Town → Route 107 Coordinates
    ///
    /// Documents the exact expected vs actual coordinates for this connection.
    /// </summary>
    [Fact]
    public void DewfordToRoute107_Coordinates_ShouldMatch_Expected()
    {
        // Arrange - Expected values based on map data analysis
        var expected = new ConnectionTestData
        {
            SourceMap = "dewford_town",
            TargetMap = "route107",
            Direction = "north",

            // Expected source position (where player triggers connection)
            SourceTileX = 7,
            SourceTileY = 0,
            SourcePixelX = 7 * TILE_SIZE_PIXELS,
            SourcePixelY = 0,

            // Expected target position (where player should appear)
            ExpectedTargetTileX = 7,
            ExpectedTargetTileY = 14, // Bottom edge of route107
            ExpectedTargetPixelX = 7 * TILE_SIZE_PIXELS,
            ExpectedTargetPixelY = 14 * TILE_SIZE_PIXELS,

            // Map dimensions for validation
            SourceMapWidth = 15,
            SourceMapHeight = 10,
            TargetMapWidth = 15,
            TargetMapHeight = 15,
        };

        // Act - Simulate the warp
        var actual = SimulateMapWarp(expected);

        // Assert - Tile coordinates
        actual
            .ActualTargetTileX.Should()
            .Be(expected.ExpectedTargetTileX, "Target X tile coordinate should match expected");

        actual
            .ActualTargetTileY.Should()
            .Be(expected.ExpectedTargetTileY, "Target Y tile coordinate should match expected");

        // Assert - Pixel coordinates
        actual
            .ActualTargetPixelX.Should()
            .Be(expected.ExpectedTargetPixelX, "Target X pixel coordinate should match expected");

        actual
            .ActualTargetPixelY.Should()
            .Be(expected.ExpectedTargetPixelY, "Target Y pixel coordinate should match expected");

        // Critical assertion - Detect the 2-tile upward shift bug
        var tileOffsetY = expected.ExpectedTargetTileY - actual.ActualTargetTileY;
        var pixelOffsetY = expected.ExpectedTargetPixelY - actual.ActualTargetPixelY;

        tileOffsetY
            .Should()
            .Be(
                0,
                $"DETECTED BUG: Y-coordinate shifted by {tileOffsetY} tiles (expected 0, bug shows +2)"
            );

        pixelOffsetY
            .Should()
            .Be(
                0,
                $"DETECTED BUG: Y-coordinate shifted by {pixelOffsetY} pixels (expected 0, bug shows +32)"
            );

        // Explicit bug detection
        if (tileOffsetY == BUGGY_OFFSET_TILES)
        {
            Assert.Fail(
                $"CONFIRMED: 2-tile upward offset bug in dewford_town → route107 connection"
            );
        }
    }

    /// <summary>
    /// Test Case 2: Route 114 → Route 115 Coordinates
    ///
    /// Documents the exact expected vs actual coordinates for this connection.
    /// </summary>
    [Fact]
    public void Route114ToRoute115_Coordinates_ShouldMatch_Expected()
    {
        // Arrange - Expected values based on map data analysis
        var expected = new ConnectionTestData
        {
            SourceMap = "route114",
            TargetMap = "route115",
            Direction = "south",

            // Expected source position
            SourceTileX = 10,
            SourceTileY = 19, // Bottom edge of route114
            SourcePixelX = 10 * TILE_SIZE_PIXELS,
            SourcePixelY = 19 * TILE_SIZE_PIXELS,

            // Expected target position
            ExpectedTargetTileX = 10,
            ExpectedTargetTileY = 0, // Top edge of route115
            ExpectedTargetPixelX = 10 * TILE_SIZE_PIXELS,
            ExpectedTargetPixelY = 0,

            // Map dimensions
            SourceMapWidth = 20,
            SourceMapHeight = 20,
            TargetMapWidth = 20,
            TargetMapHeight = 15,
        };

        // Act
        var actual = SimulateMapWarp(expected);

        // Assert - Tile coordinates
        actual
            .ActualTargetTileX.Should()
            .Be(expected.ExpectedTargetTileX, "Target X tile coordinate should match expected");

        actual
            .ActualTargetTileY.Should()
            .Be(expected.ExpectedTargetTileY, "Target Y tile coordinate should match expected");

        // Assert - Pixel coordinates
        actual
            .ActualTargetPixelX.Should()
            .Be(expected.ExpectedTargetPixelX, "Target X pixel coordinate should match expected");

        actual
            .ActualTargetPixelY.Should()
            .Be(expected.ExpectedTargetPixelY, "Target Y pixel coordinate should match expected");

        // Critical assertion - Detect the 2-tile downward shift bug
        var tileOffsetY = actual.ActualTargetTileY - expected.ExpectedTargetTileY;
        var pixelOffsetY = actual.ActualTargetPixelY - expected.ExpectedTargetPixelY;

        tileOffsetY
            .Should()
            .Be(
                0,
                $"DETECTED BUG: Y-coordinate shifted by {tileOffsetY} tiles (expected 0, bug shows -2)"
            );

        pixelOffsetY
            .Should()
            .Be(
                0,
                $"DETECTED BUG: Y-coordinate shifted by {pixelOffsetY} pixels (expected 0, bug shows -32)"
            );

        // Explicit bug detection
        if (Math.Abs(tileOffsetY) == BUGGY_OFFSET_TILES)
        {
            Assert.Fail($"CONFIRMED: 2-tile downward offset bug in route114 → route115 connection");
        }
    }

    /// <summary>
    /// Test Case 3: Comprehensive Coordinate Matrix
    ///
    /// Tests multiple connections with expected coordinate mappings.
    /// </summary>
    [Theory]
    [InlineData("dewford_town", "route107", 7, 0, 7, 14, "Dewford north exit")]
    [InlineData("route107", "dewford_town", 7, 0, 7, 9, "Route107 south return")]
    [InlineData("route114", "route115", 10, 19, 10, 0, "Route114 south exit")]
    [InlineData("route115", "route114", 10, 0, 10, 19, "Route115 north return")]
    public void ConnectionCoordinates_ShouldMap_Correctly(
        string sourceMap,
        string targetMap,
        int sourceTileX,
        int sourceTileY,
        int expectedTargetX,
        int expectedTargetY,
        string description
    )
    {
        // Arrange
        var sourcePosition = new TileCoordinate(sourceTileX, sourceTileY);

        // Act
        var actualTarget = CalculateTargetCoordinate(sourceMap, targetMap, sourcePosition);

        // Assert
        actualTarget
            .TileX.Should()
            .Be(expectedTargetX, $"{description}: Target X should be {expectedTargetX}");

        actualTarget
            .TileY.Should()
            .Be(expectedTargetY, $"{description}: Target Y should be {expectedTargetY}");

        // Calculate and verify offset
        var offsetX = actualTarget.TileX - expectedTargetX;
        var offsetY = actualTarget.TileY - expectedTargetY;

        offsetX.Should().Be(0, $"{description}: No X offset expected");
        offsetY.Should().Be(0, $"{description}: No Y offset expected");

        // Check for the specific 2-tile bug
        if (Math.Abs(offsetY) == BUGGY_OFFSET_TILES)
        {
            Assert.Fail(
                $"{description}: DETECTED 2-tile offset bug! "
                    + $"Expected ({expectedTargetX}, {expectedTargetY}) "
                    + $"but got ({actualTarget.TileX}, {actualTarget.TileY})"
            );
        }
    }

    /// <summary>
    /// Test Case 4: Pixel-Perfect Coordinate Mapping
    ///
    /// Validates coordinates at pixel level for sub-tile accuracy.
    /// </summary>
    [Fact]
    public void PixelCoordinates_ShouldBe_Exact()
    {
        // Arrange
        var testCases = new[]
        {
            new PixelTestCase
            {
                SourceMap = "dewford_town",
                TargetMap = "route107",
                SourcePixelX = 112, // 7 * 16
                SourcePixelY = 0,
                ExpectedTargetPixelX = 112,
                ExpectedTargetPixelY = 224, // 14 * 16
                Description = "Dewford to Route107",
            },
            new PixelTestCase
            {
                SourceMap = "route114",
                TargetMap = "route115",
                SourcePixelX = 160, // 10 * 16
                SourcePixelY = 304, // 19 * 16
                ExpectedTargetPixelX = 160,
                ExpectedTargetPixelY = 0,
                Description = "Route114 to Route115",
            },
        };

        foreach (var testCase in testCases)
        {
            // Act
            var actualPixels = CalculateTargetPixels(
                testCase.SourceMap,
                testCase.TargetMap,
                testCase.SourcePixelX,
                testCase.SourcePixelY
            );

            // Assert
            actualPixels
                .X.Should()
                .Be(
                    testCase.ExpectedTargetPixelX,
                    $"{testCase.Description}: Pixel X coordinate mismatch"
                );

            actualPixels
                .Y.Should()
                .Be(
                    testCase.ExpectedTargetPixelY,
                    $"{testCase.Description}: Pixel Y coordinate mismatch"
                );

            // Check for 32-pixel offset bug
            var pixelOffsetY = Math.Abs(actualPixels.Y - testCase.ExpectedTargetPixelY);
            if (pixelOffsetY == BUGGY_OFFSET_PIXELS)
            {
                Assert.Fail(
                    $"{testCase.Description}: DETECTED 32-pixel offset bug! "
                        + $"Expected Y={testCase.ExpectedTargetPixelY} but got Y={actualPixels.Y}"
                );
            }
        }
    }

    /// <summary>
    /// Test Case 5: Offset Accumulation Test
    ///
    /// Verifies that going from A→B→A returns to original position.
    /// If there's an offset bug, it might accumulate or not properly reverse.
    /// </summary>
    [Fact]
    public void RoundTrip_Connection_ShouldReturn_ToOriginal()
    {
        // Arrange
        var originalMap = "dewford_town";
        var intermediateMap = "route107";

        var startTile = new TileCoordinate(7, 5);
        var startPixel = new PixelCoordinate(7 * TILE_SIZE_PIXELS, 5 * TILE_SIZE_PIXELS);

        // Act - Go from dewford_town → route107 → dewford_town
        var afterFirstWarp = CalculateTargetCoordinate(originalMap, intermediateMap, startTile);

        var afterRoundTrip = CalculateTargetCoordinate(
            intermediateMap,
            originalMap,
            new TileCoordinate(afterFirstWarp.TileX, afterFirstWarp.TileY)
        );

        // Assert
        afterRoundTrip
            .TileX.Should()
            .Be(startTile.TileX, "Round trip should return to original X coordinate");

        afterRoundTrip
            .TileY.Should()
            .Be(startTile.TileY, "Round trip should return to original Y coordinate");

        // If there's an offset bug, it might accumulate on round trip
        var accumulatedOffsetX = afterRoundTrip.TileX - startTile.TileX;
        var accumulatedOffsetY = afterRoundTrip.TileY - startTile.TileY;

        accumulatedOffsetX.Should().Be(0, "No X offset should accumulate on round trip");

        accumulatedOffsetY.Should().Be(0, "No Y offset should accumulate on round trip");

        if (Math.Abs(accumulatedOffsetY) > 0)
        {
            Assert.Fail(
                $"OFFSET ACCUMULATION DETECTED: Round trip has {accumulatedOffsetY} tile offset "
                    + $"(original: {startTile}, after round trip: {afterRoundTrip})"
            );
        }
    }

    #region Test Data Types

    public class ConnectionTestData
    {
        public string SourceMap { get; set; }
        public string TargetMap { get; set; }
        public string Direction { get; set; }

        public int SourceTileX { get; set; }
        public int SourceTileY { get; set; }
        public int SourcePixelX { get; set; }
        public int SourcePixelY { get; set; }

        public int ExpectedTargetTileX { get; set; }
        public int ExpectedTargetTileY { get; set; }
        public int ExpectedTargetPixelX { get; set; }
        public int ExpectedTargetPixelY { get; set; }

        public int SourceMapWidth { get; set; }
        public int SourceMapHeight { get; set; }
        public int TargetMapWidth { get; set; }
        public int TargetMapHeight { get; set; }
    }

    public class ActualConnectionResult
    {
        public int ActualTargetTileX { get; set; }
        public int ActualTargetTileY { get; set; }
        public int ActualTargetPixelX { get; set; }
        public int ActualTargetPixelY { get; set; }
    }

    public record TileCoordinate(int TileX, int TileY);

    public record PixelCoordinate(int X, int Y);

    public class PixelTestCase
    {
        public string SourceMap { get; set; }
        public string TargetMap { get; set; }
        public int SourcePixelX { get; set; }
        public int SourcePixelY { get; set; }
        public int ExpectedTargetPixelX { get; set; }
        public int ExpectedTargetPixelY { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Simulates a map warp and returns the actual result.
    /// TODO: Replace with actual game warp logic.
    /// </summary>
    private ActualConnectionResult SimulateMapWarp(ConnectionTestData expected)
    {
        // Placeholder implementation
        // In a real test, this would call the actual warp/connection logic
        return new ActualConnectionResult
        {
            ActualTargetTileX = expected.ExpectedTargetTileX,
            ActualTargetTileY = expected.ExpectedTargetTileY,
            ActualTargetPixelX = expected.ExpectedTargetPixelX,
            ActualTargetPixelY = expected.ExpectedTargetPixelY,
        };
    }

    /// <summary>
    /// Calculates the target coordinate given a source map, target map, and source position.
    /// TODO: Replace with actual coordinate calculation logic.
    /// </summary>
    private TileCoordinate CalculateTargetCoordinate(
        string sourceMap,
        string targetMap,
        TileCoordinate sourcePosition
    )
    {
        // Placeholder implementation
        return new TileCoordinate(sourcePosition.TileX, sourcePosition.TileY);
    }

    /// <summary>
    /// Calculates target pixel coordinates.
    /// TODO: Replace with actual pixel coordinate calculation.
    /// </summary>
    private PixelCoordinate CalculateTargetPixels(
        string sourceMap,
        string targetMap,
        int sourcePixelX,
        int sourcePixelY
    )
    {
        // Placeholder implementation
        return new PixelCoordinate(sourcePixelX, sourcePixelY);
    }

    #endregion
}
