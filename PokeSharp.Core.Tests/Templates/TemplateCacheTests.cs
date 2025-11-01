using PokeSharp.Core.Templates;
using Xunit;
using FluentAssertions;

namespace PokeSharp.Core.Tests.Templates;

public class TemplateCacheTests
{
    [Fact]
    public void Register_ShouldAddTemplateToCache()
    {
        // Arrange
        var cache = new TemplateCache();
        var template = CreateTestTemplate("test/template");

        // Act
        cache.Register(template);

        // Assert
        cache.Get("test/template").Should().NotBeNull();
    }

    [Fact]
    public void Register_WithDuplicateId_ShouldOverwrite()
    {
        // Arrange
        var cache = new TemplateCache();
        var template1 = CreateTestTemplate("test/template");
        var template2 = CreateTestTemplate("test/template");
        template2.Name = "Updated";

        // Act
        cache.Register(template1);
        cache.Register(template2);

        // Assert
        var cached = cache.Get("test/template");
        cached.Should().NotBeNull();
        cached!.Name.Should().Be("Updated");
    }

    [Fact]
    public void Get_WithNonExistentTemplate_ShouldReturnNull()
    {
        // Arrange
        var cache = new TemplateCache();

        // Act
        var template = cache.Get("nonexistent");

        // Assert
        template.Should().BeNull();
    }

    [Fact]
    public void RegisterBatch_ShouldAddAllTemplates()
    {
        // Arrange
        var cache = new TemplateCache();
        var templates = new[]
        {
            CreateTestTemplate("test/one"),
            CreateTestTemplate("test/two"),
            CreateTestTemplate("test/three")
        };

        // Act
        cache.RegisterBatch(templates);

        // Assert
        cache.GetStatistics().TotalTemplates.Should().Be(3);
        cache.Get("test/one").Should().NotBeNull();
        cache.Get("test/two").Should().NotBeNull();
        cache.Get("test/three").Should().NotBeNull();
    }

    [Fact]
    public void GetByTag_ShouldReturnMatchingTemplates()
    {
        // Arrange
        var cache = new TemplateCache();
        var template1 = CreateTestTemplate("test/one", "pokemon");
        var template2 = CreateTestTemplate("test/two", "pokemon");
        var template3 = CreateTestTemplate("test/three", "npc");

        cache.Register(template1);
        cache.Register(template2);
        cache.Register(template3);

        // Act
        var pokemonTemplates = cache.GetByTag("pokemon");

        // Assert
        pokemonTemplates.Should().HaveCount(2);
        pokemonTemplates.Should().Contain(t => t.TemplateId == "test/one");
        pokemonTemplates.Should().Contain(t => t.TemplateId == "test/two");
    }

    [Fact]
    public void Invalidate_ShouldRemoveTemplate()
    {
        // Arrange
        var cache = new TemplateCache();
        var template = CreateTestTemplate("test/template");
        cache.Register(template);

        // Act
        cache.Invalidate("test/template");

        // Assert
        cache.Get("test/template").Should().BeNull();
    }

    [Fact]
    public void InvalidateByTag_ShouldRemoveMatchingTemplates()
    {
        // Arrange
        var cache = new TemplateCache();
        cache.Register(CreateTestTemplate("test/one", "pokemon"));
        cache.Register(CreateTestTemplate("test/two", "pokemon"));
        cache.Register(CreateTestTemplate("test/three", "npc"));

        // Act
        cache.InvalidateWhere(id => cache.Get(id)?.Tag == "pokemon");

        // Assert
        cache.Get("test/one").Should().BeNull();
        cache.Get("test/two").Should().BeNull();
        cache.Get("test/three").Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var cache = new TemplateCache();
        cache.Register(CreateTestTemplate("test/one", "pokemon"));
        cache.Register(CreateTestTemplate("test/two", "pokemon"));
        cache.Register(CreateTestTemplate("test/three", "npc"));

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.TotalTemplates.Should().Be(3);
        stats.TemplatesByTag.Should().ContainKey("pokemon");
        stats.TemplatesByTag["pokemon"].Should().Be(2);
        stats.TemplatesByTag["npc"].Should().Be(1);
    }

    [Fact]
    public void ThreadSafety_ConcurrentOperations_ShouldNotThrow()
    {
        // Arrange
        var cache = new TemplateCache();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                cache.Register(CreateTestTemplate($"test/{index}"));
                cache.Get($"test/{index}");
            }));
        }

        // Assert
        Action act = () => Task.WaitAll(tasks.ToArray());
        act.Should().NotThrow();
        cache.GetStatistics().TotalTemplates.Should().Be(100);
    }

    // Helper method
    private static EntityTemplate CreateTestTemplate(string id, string tag = "test")
    {
        return new EntityTemplate
        {
            TemplateId = id,
            Name = $"Template {id}",
            Tag = tag
        }.WithComponent(new TestComponent { Value = 42 });
    }

    private struct TestComponent
    {
        public int Value { get; set; }
    }
}
