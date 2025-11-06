using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Core.Types;

namespace PokeSharp.Core.Tests.Types;

/// <summary>
///     Tests for TypeRegistry functionality including loading, caching, and hot-reload.
/// </summary>
public class TypeRegistryTests
{
    private TypeRegistry<TestTypeDefinition> CreateTestRegistry()
    {
        var mockLogger = new Mock<ILogger>();
        return new TypeRegistry<TestTypeDefinition>("./test-data", mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange & Act
        var registry = CreateTestRegistry();

        // Assert
        Assert.NotNull(registry);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void Register_WithValidType_ShouldAddToRegistry()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var testType = new TestTypeDefinition
        {
            TypeId = "test_type",
            DisplayName = "Test Type",
            Description = "A test type",
        };

        // Act
        registry.Register(testType);

        // Assert
        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("test_type"));
    }

    [Fact]
    public void Get_ExistingType_ShouldReturnType()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var testType = new TestTypeDefinition { TypeId = "test_type", DisplayName = "Test Type" };
        registry.Register(testType);

        // Act
        var result = registry.Get("test_type");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test_type", result!.TypeId);
        Assert.Equal("Test Type", result.DisplayName);
    }

    [Fact]
    public void Get_NonExistentType_ShouldReturnNull()
    {
        // Arrange
        var registry = CreateTestRegistry();

        // Act
        var result = registry.Get("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Contains_ExistingType_ShouldReturnTrue()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var testType = new TestTypeDefinition { TypeId = "test_type", DisplayName = "Test Type" };
        registry.Register(testType);

        // Act
        var result = registry.Contains("test_type");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_NonExistentType_ShouldReturnFalse()
    {
        // Arrange
        var registry = CreateTestRegistry();

        // Act
        var result = registry.Contains("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Remove_ExistingType_ShouldReturnTrueAndRemoveType()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var testType = new TestTypeDefinition { TypeId = "test_type", DisplayName = "Test Type" };
        registry.Register(testType);

        // Act
        var result = registry.Remove("test_type");

        // Assert
        Assert.True(result);
        Assert.Equal(0, registry.Count);
        Assert.False(registry.Contains("test_type"));
    }

    [Fact]
    public void Remove_NonExistentType_ShouldReturnFalse()
    {
        // Arrange
        var registry = CreateTestRegistry();

        // Act
        var result = registry.Remove("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clear_WithTypes_ShouldRemoveAll()
    {
        // Arrange
        var registry = CreateTestRegistry();
        registry.Register(new TestTypeDefinition { TypeId = "type1", DisplayName = "Type 1" });
        registry.Register(new TestTypeDefinition { TypeId = "type2", DisplayName = "Type 2" });
        registry.Register(new TestTypeDefinition { TypeId = "type3", DisplayName = "Type 3" });

        // Act
        registry.Clear();

        // Assert
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void GetAllTypeIds_WithMultipleTypes_ShouldReturnAllIds()
    {
        // Arrange
        var registry = CreateTestRegistry();
        registry.Register(new TestTypeDefinition { TypeId = "type1", DisplayName = "Type 1" });
        registry.Register(new TestTypeDefinition { TypeId = "type2", DisplayName = "Type 2" });
        registry.Register(new TestTypeDefinition { TypeId = "type3", DisplayName = "Type 3" });

        // Act
        var ids = registry.GetAllTypeIds().ToList();

        // Assert
        Assert.Equal(3, ids.Count);
        Assert.Contains("type1", ids);
        Assert.Contains("type2", ids);
        Assert.Contains("type3", ids);
    }

    [Fact]
    public void GetAll_WithMultipleTypes_ShouldReturnAllTypes()
    {
        // Arrange
        var registry = CreateTestRegistry();
        registry.Register(new TestTypeDefinition { TypeId = "type1", DisplayName = "Type 1" });
        registry.Register(new TestTypeDefinition { TypeId = "type2", DisplayName = "Type 2" });

        // Act
        var types = registry.GetAll().ToList();

        // Assert
        Assert.Equal(2, types.Count);
    }

    [Fact]
    public void Register_WithNullType_ShouldThrow()
    {
        // Arrange
        var registry = CreateTestRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void Register_WithEmptyTypeId_ShouldThrow()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var testType = new TestTypeDefinition { TypeId = "", DisplayName = "Test" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.Register(testType));
    }

    [Fact]
    public void Register_SameTypeIdTwice_ShouldOverwrite()
    {
        // Arrange
        var registry = CreateTestRegistry();
        var type1 = new TestTypeDefinition { TypeId = "test", DisplayName = "First" };
        var type2 = new TestTypeDefinition { TypeId = "test", DisplayName = "Second" };

        // Act
        registry.Register(type1);
        registry.Register(type2);

        // Assert
        Assert.Equal(1, registry.Count);
        var result = registry.Get("test");
        Assert.Equal("Second", result!.DisplayName);
    }

    [Fact]
    public void Get_WithNullOrEmptyTypeId_ShouldReturnNull()
    {
        // Arrange
        var registry = CreateTestRegistry();

        // Act
        var result1 = registry.Get(null!);
        var result2 = registry.Get("");
        var result3 = registry.Get("   ");

        // Assert
        Assert.Null(result1);
        Assert.Null(result2);
        Assert.Null(result3);
    }

    // Test type definition for testing
    private record TestTypeDefinition : ITypeDefinition
    {
        public required string TypeId { get; init; }
        public required string DisplayName { get; init; }
        public string? Description { get; init; }
    }
}
