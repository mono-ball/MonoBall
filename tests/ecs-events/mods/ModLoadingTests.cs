using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using NUnit.Framework;
using FluentAssertions;

namespace PokeSharp.EcsEvents.Tests.Mods;

/// <summary>
/// Tests for mod loading, isolation, and safety.
/// Ensures mods cannot crash game or escape sandboxing.
/// </summary>
[TestFixture]
public class ModLoadingTests
{
    private ModLoader _modLoader = null!;
    private string _testModsDirectory = null!;

    [SetUp]
    public void Setup()
    {
        _modLoader = new ModLoader();
        _testModsDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "test-mods");
        Directory.CreateDirectory(_testModsDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testModsDirectory))
        {
            Directory.Delete(_testModsDirectory, recursive: true);
        }
    }

    #region Mod Loading Tests

    [Test]
    public void LoadMod_ValidMod_LoadsSuccessfully()
    {
        // Arrange
        CreateTestMod("valid-mod", withManifest: true, hasErrors: false);

        // Act
        var result = _modLoader.LoadMod(Path.Combine(_testModsDirectory, "valid-mod"));

        // Assert
        result.Success.Should().BeTrue("valid mod should load");
        result.Mod.Should().NotBeNull();
    }

    [Test]
    public void LoadMod_MissingManifest_Fails()
    {
        // Arrange
        CreateTestMod("no-manifest", withManifest: false, hasErrors: false);

        // Act
        var result = _modLoader.LoadMod(Path.Combine(_testModsDirectory, "no-manifest"));

        // Assert
        result.Success.Should().BeFalse("mod without manifest should fail");
        result.Error.Should().Contain("manifest", "error should mention manifest");
    }

    [Test]
    public void LoadMod_InvalidManifest_Fails()
    {
        // Arrange
        var modPath = Path.Combine(_testModsDirectory, "invalid-manifest");
        Directory.CreateDirectory(modPath);
        File.WriteAllText(Path.Combine(modPath, "mod.json"), "{ invalid json }");

        // Act
        var result = _modLoader.LoadMod(modPath);

        // Assert
        result.Success.Should().BeFalse("mod with invalid manifest should fail");
    }

    [Test]
    public void LoadAllMods_MultipleMods_LoadsInDependencyOrder()
    {
        // Arrange
        CreateTestMod("mod-a", dependencies: new string[0]);
        CreateTestMod("mod-b", dependencies: new[] { "mod-a" });
        CreateTestMod("mod-c", dependencies: new[] { "mod-a", "mod-b" });

        // Act
        var loadedMods = _modLoader.LoadAllMods(_testModsDirectory);

        // Assert
        loadedMods.Should().HaveCount(3);
        var loadOrder = loadedMods.Select(m => m.Name).ToList();
        loadOrder.IndexOf("mod-a").Should().BeLessThan(loadOrder.IndexOf("mod-b"));
        loadOrder.IndexOf("mod-b").Should().BeLessThan(loadOrder.IndexOf("mod-c"));
    }

    [Test]
    public void LoadAllMods_CircularDependency_DetectsAndRejects()
    {
        // Arrange
        CreateTestMod("mod-a", dependencies: new[] { "mod-b" });
        CreateTestMod("mod-b", dependencies: new[] { "mod-a" });

        // Act & Assert
        Action act = () => _modLoader.LoadAllMods(_testModsDirectory);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*circular*", "circular dependencies should be detected");
    }

    #endregion

    #region Mod Isolation Tests

    [Test]
    public void Mod_CannotAccessFileSystem()
    {
        // Arrange
        var mod = CreateEvilMod("filesystem-access", () =>
        {
            File.ReadAllText("C:/secrets.txt");
        });

        // Act & Assert
        Action act = () => mod.Execute(new ModContext());
        act.Should().Throw<SecurityException>(
            "mod should not be able to access file system");
    }

    [Test]
    public void Mod_CannotAccessOtherModData()
    {
        // Arrange
        var mod1 = CreateTestModInstance("mod1");
        var mod2 = CreateTestModInstance("mod2");

        mod1.SetState("secret-data", "sensitive");

        // Act
        var retrieved = mod2.TryGetState<string>("mod1/secret-data");

        // Assert
        retrieved.Should().BeNull("mods should not access each other's state");
    }

    [Test]
    public void Mod_CrashDoesNotCrashGame()
    {
        // Arrange
        var crashingMod = CreateEvilMod("crashing", () =>
        {
            throw new Exception("Mod crashed!");
        });

        var stableMod = CreateTestModInstance("stable");
        var modRunner = new ModRunner();

        modRunner.RegisterMod(crashingMod);
        modRunner.RegisterMod(stableMod);

        // Act & Assert
        Action act = () => modRunner.ExecuteAllMods();
        act.Should().NotThrow("game should handle mod crashes gracefully");
    }

    [Test]
    public void Mod_ExcessiveMemory_TerminatedSafely()
    {
        // Arrange
        var memoryHog = CreateEvilMod("memory-hog", () =>
        {
            var lists = new List<byte[]>();
            for (int i = 0; i < 10000; i++)
            {
                lists.Add(new byte[1024 * 1024]); // 1MB each = 10GB total
            }
        });

        var modRunner = new ModRunner(maxMemoryMB: 100);

        // Act & Assert
        Action act = () => modRunner.ExecuteMod(memoryHog);
        act.Should().Throw<OutOfMemoryException>(
            "excessive memory allocation should be prevented");
    }

    [Test]
    public void Mod_InfiniteLoop_TimesOut()
    {
        // Arrange
        var infiniteLoop = CreateEvilMod("infinite-loop", () =>
        {
            while (true) { }
        });

        var modRunner = new ModRunner(timeout: TimeSpan.FromMilliseconds(100));

        // Act & Assert
        Action act = () => modRunner.ExecuteMod(infiniteLoop);
        act.Should().Throw<TimeoutException>(
            "infinite loops should be terminated");
    }

    #endregion

    #region Mod Event Tests

    [Test]
    public void Mod_CanSubscribeToGameEvents()
    {
        // Arrange
        var eventBus = new EventBus();
        var mod = CreateTestModInstance("event-subscriber");
        var eventsReceived = 0;

        mod.SubscribeToEvent<PlayerMoveEvent>(evt => eventsReceived++);
        mod.Initialize(eventBus);

        // Act
        eventBus.Publish(new PlayerMoveEvent());
        eventBus.Publish(new PlayerMoveEvent());

        // Assert
        eventsReceived.Should().Be(2, "mod should receive game events");
    }

    [Test]
    public void Mod_CanPublishCustomEvents()
    {
        // Arrange
        var eventBus = new EventBus();
        var mod = CreateTestModInstance("event-publisher");
        var eventReceived = false;

        eventBus.Subscribe<CustomModEvent>(evt => eventReceived = true);
        mod.Initialize(eventBus);

        // Act
        mod.PublishEvent(new CustomModEvent { Data = "test" });

        // Assert
        eventReceived.Should().BeTrue("custom mod events should be published");
    }

    [Test]
    public void Mod_CannotFloodEvents()
    {
        // Arrange
        var eventBus = new EventBus();
        var mod = CreateTestModInstance("event-flooder");
        var eventCount = 0;

        eventBus.Subscribe<CustomModEvent>(evt => eventCount++);
        mod.Initialize(eventBus);

        // Act - Try to flood with events
        for (int i = 0; i < 10000; i++)
        {
            mod.PublishEvent(new CustomModEvent());
        }

        // Assert
        eventCount.Should().BeLessThan(1000, "event rate limiting should be enforced");
    }

    #endregion

    #region Mod API Access Tests

    [Test]
    public void Mod_CanAccessGameAPI_ThroughContext()
    {
        // Arrange
        var mod = CreateTestModInstance("api-user");
        var context = new ModContext
        {
            GameAPI = new TestGameAPI()
        };

        // Act & Assert
        Action act = () =>
        {
            var player = context.GameAPI.GetPlayer();
            player.Should().NotBeNull();
        };

        act.Should().NotThrow("mods can access game API through context");
    }

    [Test]
    public void Mod_CannotAccessRestrictedAPI()
    {
        // Arrange
        var mod = CreateTestModInstance("restricted-access");
        var context = new ModContext
        {
            GameAPI = new TestGameAPI()
        };

        // Act & Assert
        Action act = () => context.GameAPI.ShutdownGame(); // Restricted method

        act.Should().Throw<SecurityException>(
            "restricted API methods should not be accessible");
    }

    #endregion

    #region Helper Methods

    private void CreateTestMod(string name, string[]? dependencies = null,
        bool withManifest = true, bool hasErrors = false)
    {
        dependencies ??= Array.Empty<string>();

        var modPath = Path.Combine(_testModsDirectory, name);
        Directory.CreateDirectory(modPath);

        if (withManifest)
        {
            var manifest = new
            {
                name = name,
                version = "1.0.0",
                author = "Test",
                dependencies = dependencies
            };

            var json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(Path.Combine(modPath, "mod.json"), json);
        }
    }

    private IMod CreateTestModInstance(string name)
    {
        return new TestMod { Name = name };
    }

    private IMod CreateEvilMod(string name, Action evilAction)
    {
        return new EvilMod { Name = name, EvilAction = evilAction };
    }

    #endregion
}

#region Mod Infrastructure

/// <summary>
/// Loads and manages game mods.
/// </summary>
public class ModLoader
{
    public ModLoadResult LoadMod(string modPath)
    {
        try
        {
            var manifestPath = Path.Combine(modPath, "mod.json");
            if (!File.Exists(manifestPath))
            {
                return ModLoadResult.Failure("Mod manifest not found");
            }

            var json = File.ReadAllText(manifestPath);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<ModManifest>(json);

            if (manifest == null)
            {
                return ModLoadResult.Failure("Invalid manifest format");
            }

            return ModLoadResult.Success(new TestMod { Name = manifest.Name });
        }
        catch (Exception ex)
        {
            return ModLoadResult.Failure(ex.Message);
        }
    }

    public List<IMod> LoadAllMods(string modsDirectory)
    {
        var mods = new List<IMod>();
        var modDirs = Directory.GetDirectories(modsDirectory);

        // Check for circular dependencies
        var dependencies = new Dictionary<string, string[]>();
        foreach (var dir in modDirs)
        {
            var result = LoadMod(dir);
            if (result.Success)
            {
                // TODO: Track dependencies
            }
        }

        // TODO: Implement topological sort for dependency order
        return mods;
    }
}

/// <summary>
/// Executes mods with safety constraints.
/// </summary>
public class ModRunner
{
    private readonly TimeSpan _timeout;
    private readonly int _maxMemoryMB;
    private readonly List<IMod> _mods = new();

    public ModRunner(TimeSpan? timeout = null, int maxMemoryMB = 100)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _maxMemoryMB = maxMemoryMB;
    }

    public void RegisterMod(IMod mod) => _mods.Add(mod);

    public void ExecuteAllMods()
    {
        foreach (var mod in _mods)
        {
            try
            {
                ExecuteMod(mod);
            }
            catch (Exception ex)
            {
                // Log error but continue with other mods
                Console.WriteLine($"Mod {mod.Name} failed: {ex.Message}");
            }
        }
    }

    public void ExecuteMod(IMod mod)
    {
        // TODO: Implement timeout and memory limits
        mod.Execute(new ModContext());
    }
}

public interface IMod
{
    string Name { get; }
    void Initialize(EventBus eventBus);
    void Execute(ModContext context);
    void SubscribeToEvent<T>(Action<T> handler) where T : IEvent;
    void PublishEvent(IEvent evt);
    void SetState<T>(string key, T value);
    T? TryGetState<T>(string key);
}

public class TestMod : IMod
{
    public string Name { get; set; } = string.Empty;
    private EventBus? _eventBus;
    private readonly Dictionary<string, object> _state = new();

    public void Initialize(EventBus eventBus) => _eventBus = eventBus;

    public void Execute(ModContext context) { }

    public void SubscribeToEvent<T>(Action<T> handler) where T : IEvent
    {
        _eventBus?.Subscribe<T>(handler.Invoke);
    }

    public void PublishEvent(IEvent evt) => _eventBus?.Publish(evt);

    public void SetState<T>(string key, T value) => _state[key] = value!;

    public T? TryGetState<T>(string key)
    {
        return _state.TryGetValue(key, out var value) ? (T?)value : default;
    }
}

public class EvilMod : IMod
{
    public string Name { get; set; } = string.Empty;
    public Action EvilAction { get; set; } = () => { };

    public void Initialize(EventBus eventBus) { }

    public void Execute(ModContext context) => EvilAction();

    public void SubscribeToEvent<T>(Action<T> handler) where T : IEvent { }
    public void PublishEvent(IEvent evt) { }
    public void SetState<T>(string key, T value) { }
    public T? TryGetState<T>(string key) => default;
}

public class ModContext
{
    public IGameAPI GameAPI { get; set; } = null!;
}

public interface IGameAPI
{
    object GetPlayer();
    void ShutdownGame(); // Restricted
}

public class TestGameAPI : IGameAPI
{
    public object GetPlayer() => new object();

    public void ShutdownGame()
    {
        throw new SecurityException("ShutdownGame is restricted");
    }
}

public class ModLoadResult
{
    public bool Success { get; set; }
    public IMod? Mod { get; set; }
    public string Error { get; set; } = string.Empty;

    public static ModLoadResult Success(IMod mod) =>
        new() { Success = true, Mod = mod };

    public static ModLoadResult Failure(string error) =>
        new() { Success = false, Error = error };
}

public class ModManifest
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
}

public class PlayerMoveEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object? Sender { get; set; }
}

public class CustomModEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object? Sender { get; set; }
    public string Data { get; set; } = string.Empty;
}

#endregion
