using PokeSharp.Core.Templates;
using Xunit;
using FluentAssertions;

namespace PokeSharp.Core.Tests.Templates;

public class EntityTemplateTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange & Act
        var template = new EntityTemplate
        {
            TemplateId = "test/template",
            Name = "Test Template",
            Tag = "test"
        };

        // Assert
        template.TemplateId.Should().Be("test/template");
        template.Name.Should().Be("Test Template");
        template.Tag.Should().Be("test");
        template.Components.Should().NotBeNull();
        template.Components.Should().BeEmpty();
    }

    [Fact]
    public void AddComponent_ShouldAddComponentToList()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" };
        var component = ComponentTemplate.Create(new TestComponent { Value = 42 });

        // Act
        template.AddComponent(component);

        // Assert
        template.Components.Should().HaveCount(1);
        template.Components[0].Should().BeSameAs(component);
    }

    [Fact]
    public void AddComponent_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var template = new EntityTemplate();

        // Act
        Action act = () => template.AddComponent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithComponent_ShouldAddComponentAndReturnThis()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" };
        var data = new TestComponent { Value = 42 };

        // Act
        var result = template.WithComponent(data);

        // Assert
        result.Should().BeSameAs(template);
        template.Components.Should().HaveCount(1);
        template.Components[0].ComponentType.Should().Be(typeof(TestComponent));
    }

    [Fact]
    public void WithComponent_ShouldSupportMethodChaining()
    {
        // Arrange & Act
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 1 })
            .WithComponent(new TestComponent2 { Name = "Test" });

        // Assert
        template.Components.Should().HaveCount(2);
    }

    [Fact]
    public void ComponentCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 1 })
            .WithComponent(new TestComponent2 { Name = "Test" });

        // Act
        var count = template.ComponentCount;

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void HasComponent_WithExistingComponent_ShouldReturnTrue()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 42 });

        // Act
        var has = template.HasComponent<TestComponent>();

        // Assert
        has.Should().BeTrue();
    }

    [Fact]
    public void HasComponent_WithNonExistingComponent_ShouldReturnFalse()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 42 });

        // Act
        var has = template.HasComponent<TestComponent2>();

        // Assert
        has.Should().BeFalse();
    }

    [Fact]
    public void GetComponent_WithExistingComponent_ShouldReturnComponent()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 42 });

        // Act
        var component = template.GetComponent<TestComponent>();

        // Assert
        component.Should().NotBeNull();
        component!.ComponentType.Should().Be(typeof(TestComponent));
    }

    [Fact]
    public void GetComponent_WithNonExistingComponent_ShouldReturnNull()
    {
        // Arrange
        var template = new EntityTemplate { TemplateId = "test", Name = "Test", Tag = "test" }
            .WithComponent(new TestComponent { Value = 42 });

        // Act
        var component = template.GetComponent<TestComponent2>();

        // Assert
        component.Should().BeNull();
    }

    [Fact]
    public void Validate_WithValidTemplate_ShouldReturnTrue()
    {
        // Arrange
        var template = new EntityTemplate
        {
            TemplateId = "test/valid",
            Name = "Valid Template",
            Tag = "test"
        }.WithComponent(new TestComponent { Value = 42 });

        // Act
        var isValid = template.Validate(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithMissingTemplateId_ShouldReturnFalse()
    {
        // Arrange
        var template = new EntityTemplate
        {
            Name = "Test",
            Tag = "test"
        }.WithComponent(new TestComponent { Value = 42 });

        // Act
        var isValid = template.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("TemplateId is required");
    }

    [Fact]
    public void Validate_WithMissingName_ShouldReturnFalse()
    {
        // Arrange
        var template = new EntityTemplate
        {
            TemplateId = "test/template",
            Tag = "test"
        }.WithComponent(new TestComponent { Value = 42 });

        // Act
        var isValid = template.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Name is required");
    }

    [Fact]
    public void Validate_WithNoComponents_ShouldReturnFalse()
    {
        // Arrange
        var template = new EntityTemplate
        {
            TemplateId = "test/template",
            Name = "Test",
            Tag = "test"
        };

        // Act
        var isValid = template.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("Template must have at least one component");
    }

    [Fact]
    public void Validate_WithDuplicateComponents_ShouldReturnFalse()
    {
        // Arrange
        var template = new EntityTemplate
        {
            TemplateId = "test/template",
            Name = "Test",
            Tag = "test"
        };
        template.AddComponent(ComponentTemplate.Create(new TestComponent { Value = 1 }));
        template.AddComponent(ComponentTemplate.Create(new TestComponent { Value = 2 }));

        // Act
        var isValid = template.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("Duplicate component types"));
    }

    // Test components for testing
    private struct TestComponent
    {
        public int Value { get; set; }
    }

    private struct TestComponent2
    {
        public string Name { get; set; }
    }
}
