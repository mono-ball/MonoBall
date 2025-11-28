using System.Text.Json;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace PokeSharp.Game.Data.Tests;

/// <summary>
///     Tests for map connection parsing and validation from Tiled JSON format.
///     Verifies that connections are correctly parsed, offsets calculated, and connections validated.
/// </summary>
public class MapConnectionTests
{
    private const int TILE_SIZE = 16;

    #region Connection Parsing Tests

    [Fact]
    public void ParseConnection_North_ShouldExtractCorrectData()
    {
        // Arrange
        var jsonProperty = """
            {
                "name": "connection_north",
                "propertytype": "Connection",
                "type": "class",
                "value": {
                    "direction": "North",
                    "map": "route101",
                    "offset": 0
                }
            }
            """;

        // Act
        var connection = ParseConnectionFromJson(jsonProperty);

        // Assert
        connection.Should().NotBeNull();
        connection!.Direction.Should().Be(ConnectionDirection.North);
        connection.TargetMap.Should().Be("route101");
        connection.Offset.Should().Be(0);
    }

    [Fact]
    public void ParseConnection_WithOffset_ShouldExtractOffsetValue()
    {
        // Arrange
        var jsonProperty = """
            {
                "name": "connection_east",
                "propertytype": "Connection",
                "type": "class",
                "value": {
                    "direction": "East",
                    "map": "route103",
                    "offset": 5
                }
            }
            """;

        // Act
        var connection = ParseConnectionFromJson(jsonProperty);

        // Assert
        connection.Should().NotBeNull();
        connection!.Direction.Should().Be(ConnectionDirection.East);
        connection.TargetMap.Should().Be("route103");
        connection.Offset.Should().Be(5, "offset of 5 means maps are not aligned at 0,0");
    }

    [Fact]
    public void ParseConnection_AllDirections_ShouldParseCorrectly()
    {
        // Arrange
        var directions = new[]
        {
            ("North", ConnectionDirection.North),
            ("South", ConnectionDirection.South),
            ("East", ConnectionDirection.East),
            ("West", ConnectionDirection.West),
        };

        foreach (var (directionString, expectedDirection) in directions)
        {
            var json = $$"""
                {
                    "name": "connection_{{directionString.ToLower()}}",
                    "propertytype": "Connection",
                    "type": "class",
                    "value": {
                        "direction": "{{directionString}}",
                        "map": "test_map",
                        "offset": 0
                    }
                }
                """;

            // Act
            var connection = ParseConnectionFromJson(json);

            // Assert
            connection.Should().NotBeNull();
            connection!
                .Direction.Should()
                .Be(
                    expectedDirection,
                    $"direction '{directionString}' should parse to {expectedDirection}"
                );
        }
    }

    [Fact]
    public void ParseConnection_MissingMap_ShouldReturnInvalidConnection()
    {
        // Arrange
        var jsonProperty = """
            {
                "name": "connection_north",
                "propertytype": "Connection",
                "type": "class",
                "value": {
                    "direction": "North",
                    "offset": 0
                }
            }
            """;

        // Act
        var connection = ParseConnectionFromJson(jsonProperty);

        // Assert
        connection.Should().BeNull("connection without target map should be invalid");
    }

    [Fact]
    public void ParseConnection_InvalidDirection_ShouldReturnNull()
    {
        // Arrange
        var jsonProperty = """
            {
                "name": "connection_diagonal",
                "propertytype": "Connection",
                "type": "class",
                "value": {
                    "direction": "NorthEast",
                    "map": "route101",
                    "offset": 0
                }
            }
            """;

        // Act
        var connection = ParseConnectionFromJson(jsonProperty);

        // Assert
        connection.Should().BeNull("diagonal directions are not supported");
    }

    #endregion

    #region Offset Calculation Tests

    [Fact]
    public void CalculateOffset_NorthConnection_ShouldUseSourceMapHeight()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.North, "route101", 0);
        var sourceMapHeight = 20; // tiles
        var sourceMapWidth = 20; // tiles

        // Act
        var offset = CalculateWorldOffset(connection, sourceMapWidth, sourceMapHeight, TILE_SIZE);

        // Assert
        offset
            .Y.Should()
            .Be(-320, "north connection: Y = -(sourceHeight * tileSize) = -(20 * 16) = -320");
        offset.X.Should().Be(0, "X matches connection offset");
    }

    [Fact]
    public void CalculateOffset_SouthConnection_ShouldUseSourceMapHeight()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.South, "oldale_town", 0);
        var sourceMapHeight = 20;
        var sourceMapWidth = 20;

        // Act
        var offset = CalculateWorldOffset(connection, sourceMapWidth, sourceMapHeight, TILE_SIZE);

        // Assert
        offset.Y.Should().Be(320, "south connection: Y = sourceHeight * tileSize = 20 * 16 = 320");
        offset.X.Should().Be(0);
    }

    [Fact]
    public void CalculateOffset_EastConnection_ShouldUseSourceMapWidth()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.East, "route103", 0);
        var sourceMapHeight = 20;
        var sourceMapWidth = 20;

        // Act
        var offset = CalculateWorldOffset(connection, sourceMapWidth, sourceMapHeight, TILE_SIZE);

        // Assert
        offset.X.Should().Be(320, "east connection: X = sourceWidth * tileSize = 20 * 16 = 320");
        offset.Y.Should().Be(0);
    }

    [Fact]
    public void CalculateOffset_WestConnection_ShouldBeNegative()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.West, "petalburg_city", 0);
        var sourceMapHeight = 20;
        var sourceMapWidth = 20;
        var targetMapWidth = 30; // West map is wider

        // Act
        var offset = CalculateWorldOffset(
            connection,
            sourceMapWidth,
            sourceMapHeight,
            TILE_SIZE,
            targetMapWidth
        );

        // Assert
        offset
            .X.Should()
            .Be(-480, "west connection: X = -(targetWidth * tileSize) = -(30 * 16) = -480");
        offset.Y.Should().Be(0);
    }

    [Fact]
    public void CalculateOffset_WithNonZeroOffset_ShouldAdjustXOrY()
    {
        // Arrange - Connection with 5 tile offset
        var connection = new MapConnection(ConnectionDirection.North, "route101", 5);
        var sourceMapHeight = 20;
        var sourceMapWidth = 20;

        // Act
        var offset = CalculateWorldOffset(connection, sourceMapWidth, sourceMapHeight, TILE_SIZE);

        // Assert
        offset.X.Should().Be(80, "offset of 5 tiles = 5 * 16 = 80 pixels in X direction");
        offset.Y.Should().Be(-320, "Y remains same as zero-offset north connection");
    }

    [Fact]
    public void CalculateOffset_NegativeOffset_ShouldWork()
    {
        // Arrange - Negative offset (map is to the left)
        var connection = new MapConnection(ConnectionDirection.North, "route101", -3);
        var sourceMapHeight = 20;
        var sourceMapWidth = 20;

        // Act
        var offset = CalculateWorldOffset(connection, sourceMapWidth, sourceMapHeight, TILE_SIZE);

        // Assert
        offset.X.Should().Be(-48, "negative offset: -3 * 16 = -48 pixels");
        offset.Y.Should().Be(-320);
    }

    #endregion

    #region Connection Validation Tests

    [Fact]
    public void ValidateConnection_ValidData_ShouldPass()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.North, "route101", 0);

        // Act
        var isValid = ValidateConnection(connection);

        // Assert
        isValid.Should().BeTrue("connection has all required fields");
    }

    [Fact]
    public void ValidateConnection_EmptyMapName_ShouldFail()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.North, "", 0);

        // Act
        var isValid = ValidateConnection(connection);

        // Assert
        isValid.Should().BeFalse("connection must have a target map name");
    }

    [Fact]
    public void ValidateConnection_NullMapName_ShouldFail()
    {
        // Arrange
        var connection = new MapConnection(ConnectionDirection.North, null!, 0);

        // Act
        var isValid = ValidateConnection(connection);

        // Assert
        isValid.Should().BeFalse("connection must have a non-null target map name");
    }

    [Fact]
    public void ValidateConnection_ExtremeOffset_ShouldStillBeValid()
    {
        // Arrange - Very large offset (edge case)
        var connection = new MapConnection(ConnectionDirection.East, "route103", 100);

        // Act
        var isValid = ValidateConnection(connection);

        // Assert
        isValid.Should().BeTrue("large offsets are allowed (though unusual)");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void RealWorldExample_LittlerootToRoute101_ShouldCalculateCorrectly()
    {
        // Arrange - Based on actual Hoenn map data
        var littlerootToRoute101 = new MapConnection(ConnectionDirection.North, "route101", 0);
        var littlerootWidth = 20;
        var littlerootHeight = 20;

        // Act
        var offset = CalculateWorldOffset(
            littlerootToRoute101,
            littlerootWidth,
            littlerootHeight,
            TILE_SIZE
        );

        // Assert
        offset
            .Should()
            .Be(new Vector2(0, -320), "Route 101 is directly north of Littleroot, aligned at X=0");
    }

    [Fact]
    public void RealWorldExample_PetalburgToRoute102_ShouldCalculateCorrectly()
    {
        // Arrange - Petalburg City (30x30) to Route 102 (east connection)
        var petalburgToRoute102 = new MapConnection(ConnectionDirection.East, "route102", 0);
        var petalburgWidth = 30;
        var petalburgHeight = 30;

        // Act
        var offset = CalculateWorldOffset(
            petalburgToRoute102,
            petalburgWidth,
            petalburgHeight,
            TILE_SIZE
        );

        // Assert
        offset.X.Should().Be(480, "Route 102 is east of Petalburg: 30 tiles * 16 = 480 pixels");
        offset.Y.Should().Be(0);
    }

    [Fact]
    public void ParseMultipleConnections_FromMapData_ShouldParseAll()
    {
        // Arrange - Map with connections in multiple directions
        var mapJson = """
            {
                "properties": [
                    {
                        "name": "connection_north",
                        "propertytype": "Connection",
                        "type": "class",
                        "value": {
                            "direction": "North",
                            "map": "route101",
                            "offset": 0
                        }
                    },
                    {
                        "name": "connection_south",
                        "propertytype": "Connection",
                        "type": "class",
                        "value": {
                            "direction": "South",
                            "map": "route103",
                            "offset": 0
                        }
                    }
                ]
            }
            """;

        // Act
        var connections = ParseAllConnectionsFromMapJson(mapJson);

        // Assert
        connections.Should().HaveCount(2);
        connections.Should().Contain(c => c.Direction == ConnectionDirection.North);
        connections.Should().Contain(c => c.Direction == ConnectionDirection.South);
    }

    #endregion

    #region Test Data Structures

    public record MapConnection(ConnectionDirection Direction, string TargetMap, int Offset);

    public enum ConnectionDirection
    {
        North,
        South,
        East,
        West,
    }

    #endregion

    #region Helper Methods

    private MapConnection? ParseConnectionFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("value", out var valueElement))
                return null;

            if (!valueElement.TryGetProperty("map", out var mapElement))
                return null;

            if (!valueElement.TryGetProperty("direction", out var directionElement))
                return null;

            var targetMap = mapElement.GetString();
            if (string.IsNullOrEmpty(targetMap))
                return null;

            var directionString = directionElement.GetString();
            if (!Enum.TryParse<ConnectionDirection>(directionString, out var direction))
                return null;

            var offset = valueElement.TryGetProperty("offset", out var offsetElement)
                ? offsetElement.GetInt32()
                : 0;

            return new MapConnection(direction, targetMap, offset);
        }
        catch
        {
            return null;
        }
    }

    private List<MapConnection> ParseAllConnectionsFromMapJson(string mapJson)
    {
        var connections = new List<MapConnection>();

        try
        {
            using var doc = JsonDocument.Parse(mapJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("properties", out var properties))
                return connections;

            foreach (var property in properties.EnumerateArray())
            {
                if (property.TryGetProperty("name", out var nameElement))
                {
                    var name = nameElement.GetString();
                    if (name?.StartsWith("connection_") == true)
                    {
                        var connection = ParseConnectionFromJson(property.GetRawText());
                        if (connection != null)
                            connections.Add(connection);
                    }
                }
            }
        }
        catch
        {
            // Return empty list on parse error
        }

        return connections;
    }

    private Vector2 CalculateWorldOffset(
        MapConnection connection,
        int sourceMapWidthTiles,
        int sourceMapHeightTiles,
        int tileSize,
        int? targetMapWidthTiles = null
    )
    {
        return connection.Direction switch
        {
            ConnectionDirection.North => new Vector2(
                connection.Offset * tileSize,
                -(sourceMapHeightTiles * tileSize)
            ),
            ConnectionDirection.South => new Vector2(
                connection.Offset * tileSize,
                sourceMapHeightTiles * tileSize
            ),
            ConnectionDirection.East => new Vector2(
                sourceMapWidthTiles * tileSize,
                connection.Offset * tileSize
            ),
            ConnectionDirection.West => new Vector2(
                -((targetMapWidthTiles ?? sourceMapWidthTiles) * tileSize),
                connection.Offset * tileSize
            ),
            _ => Vector2.Zero,
        };
    }

    private bool ValidateConnection(MapConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.TargetMap))
            return false;

        if (!Enum.IsDefined(typeof(ConnectionDirection), connection.Direction))
            return false;

        return true;
    }

    #endregion
}
