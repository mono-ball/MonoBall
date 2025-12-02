// Phase 3.1 ScriptBase Verification
// This file verifies that ScriptBase compiles correctly with all its methods.

using Arch.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Tests;

/// <summary>
/// Verification test script to ensure ScriptBase API compiles correctly.
/// </summary>
public class TestScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        // Test access to Context property
        var logger = Context.Logger;
        var world = Context.World;
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Test On<TEvent> method
        On<TestGameEvent>(evt =>
        {
            Context.Logger.LogInformation("Event received: {EventId}", evt.EventId);
        });

        // Test On<TEvent> with priority
        On<TestGameEvent>(evt => { }, priority: 1000);

        // Test OnEntity<TEvent> method
        var testEntity = new Entity(1, 1);
        OnEntity<TestEntityEvent>(testEntity, evt =>
        {
            Context.Logger.LogInformation("Entity event: {EntityId}", evt.Entity.Id);
        });

        // Test OnTile<TEvent> method
        var tilePos = new Vector2(10, 15);
        OnTile<TestTileEvent>(tilePos, evt =>
        {
            Context.Logger.LogInformation("Tile event at ({X}, {Y})", evt.TileX, evt.TileY);
        });
    }

    public override void OnUnload()
    {
        // Test cleanup
        base.OnUnload();
    }

    private void TestStateMethods()
    {
        // Test Get<T> method
        var value = Get<int>("key", 42);

        // Test Set<T> method
        Set("key", 100);
    }

    private void TestPublish()
    {
        // Test Publish<TEvent> method
        Publish(new TestGameEvent());
    }
}

// Test event types
public sealed record TestGameEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed record TestEntityEvent : IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
}

public sealed record TestTileEvent : ITileEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required int TileX { get; init; }
    public required int TileY { get; init; }
}

/// <summary>
/// Static class to verify compilation without execution.
/// </summary>
public static class ScriptBaseVerification
{
    public static bool VerifyCompilation()
    {
        // This method exists only to verify the code compiles
        // It should never be executed in production

        // ✅ ScriptBase compiles
        // ✅ All methods have correct signatures
        // ✅ IEntityEvent and ITileEvent interfaces work
        // ✅ Event subscriptions compile
        // ✅ State management methods compile
        // ✅ Event publishing compiles

        return true;
    }
}
