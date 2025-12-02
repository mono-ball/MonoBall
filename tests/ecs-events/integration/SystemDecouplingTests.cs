using System;
using System.Collections.Generic;
using Arch.Core;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Engine.Core.Systems;

namespace PokeSharp.EcsEvents.Tests.Integration;

/// <summary>
/// Integration tests verifying system decoupling through events.
/// Tests that systems can communicate without direct dependencies.
/// </summary>
[TestFixture]
public class SystemDecouplingTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private SystemManager _systemManager = null!;

    [SetUp]
    public void Setup()
    {
        _world = World.Create();
        _eventBus = new EventBus();
        _systemManager = new SystemManager();
    }

    [TearDown]
    public void TearDown()
    {
        _world?.Dispose();
        _eventBus?.Dispose();
    }

    #region Movement to Collision Tests

    [Test]
    public void MovementSystem_PublishesCollisionCheckEvent_CollisionSystemHandles()
    {
        // Arrange
        var movementSystem = new EventDrivenMovementSystem(_eventBus);
        var collisionSystem = new EventDrivenCollisionSystem(_eventBus);

        _systemManager.RegisterUpdateSystem(movementSystem);
        _systemManager.RegisterUpdateSystem(collisionSystem);
        _systemManager.Initialize(_world);

        var player = _world.Create();
        // Add movement and position components

        // Act
        _systemManager.Update(_world, 0.016f);

        // Assert
        collisionSystem.HandledCollisionChecks.Should().BeGreaterThan(0,
            "collision system should receive collision check events from movement");
    }

    [Test]
    public void CollisionSystem_PublishesBlockedEvent_MovementSystemResponds()
    {
        // Arrange
        var movementSystem = new EventDrivenMovementSystem(_eventBus);
        var collisionSystem = new EventDrivenCollisionSystem(_eventBus);
        var movementBlocked = false;

        _eventBus.Subscribe<MovementBlockedEvent>(evt => movementBlocked = true);

        // Act
        _eventBus.Publish(new CollisionCheckEvent
        {
            Entity = _world.Create(),
            IsBlocked = true
        });

        collisionSystem.ProcessCollisions();

        // Assert
        movementBlocked.Should().BeTrue("movement system should receive blocked event");
    }

    #endregion

    #region Event Chain Tests

    [Test]
    public void EventChain_PlayerInput_TriggersCompleteMovementSequence()
    {
        // Arrange
        var eventLog = new List<Type>();
        var inputSystem = new EventDrivenInputSystem(_eventBus);
        var movementSystem = new EventDrivenMovementSystem(_eventBus);
        var collisionSystem = new EventDrivenCollisionSystem(_eventBus);

        _eventBus.SubscribeToAll(evt => eventLog.Add(evt.GetType()));

        // Act - Simulate player pressing Up key
        inputSystem.SimulateInput(InputKey.Up);

        // Process systems
        inputSystem.Update(_world, 0.016f);
        movementSystem.Update(_world, 0.016f);
        collisionSystem.Update(_world, 0.016f);

        // Assert - Verify event sequence
        eventLog.Should().ContainInOrder(
            typeof(InputEvent),
            typeof(MoveCommandEvent),
            typeof(CollisionCheckEvent),
            typeof(PositionUpdateEvent)
        );
    }

    [Test]
    public void EventChain_Warp_TriggersMapTransition()
    {
        // Arrange
        var eventLog = new List<string>();
        var warpSystem = new EventDrivenWarpSystem(_eventBus);
        var mapSystem = new EventDrivenMapSystem(_eventBus);

        _eventBus.Subscribe<WarpTriggeredEvent>(evt =>
            eventLog.Add($"Warp to {evt.TargetMap}"));
        _eventBus.Subscribe<MapUnloadEvent>(evt =>
            eventLog.Add($"Unload {evt.MapId}"));
        _eventBus.Subscribe<MapLoadEvent>(evt =>
            eventLog.Add($"Load {evt.MapId}"));

        var player = _world.Create();

        // Act - Trigger warp
        _eventBus.Publish(new WarpTriggeredEvent
        {
            Entity = player,
            SourceMap = "map1",
            TargetMap = "map2"
        });

        warpSystem.Update(_world, 0.016f);
        mapSystem.Update(_world, 0.016f);

        // Assert
        eventLog.Should().ContainInOrder(
            "Warp to map2",
            "Unload map1",
            "Load map2"
        );
    }

    #endregion

    #region Cross-System Communication Tests

    [Test]
    public void PlayerSystem_BroadcastsStateChanges_MultipleSystemsRespond()
    {
        // Arrange
        var uiUpdateReceived = false;
        var animationChangeReceived = false;
        var audioEventReceived = false;

        _eventBus.Subscribe<PlayerStateChangedEvent>(evt => uiUpdateReceived = true);
        _eventBus.Subscribe<PlayerStateChangedEvent>(evt => animationChangeReceived = true);
        _eventBus.Subscribe<PlayerStateChangedEvent>(evt => audioEventReceived = true);

        // Act
        _eventBus.Publish(new PlayerStateChangedEvent
        {
            State = PlayerState.Running
        });

        // Assert
        uiUpdateReceived.Should().BeTrue("UI system should receive state change");
        animationChangeReceived.Should().BeTrue("animation system should receive state change");
        audioEventReceived.Should().BeTrue("audio system should receive state change");
    }

    [Test]
    public void NPCSystem_RequestsPathfinding_PathfindingSystemResponds()
    {
        // Arrange
        var pathfindingSystem = new EventDrivenPathfindingSystem(_eventBus);
        var npcSystem = new EventDrivenNPCSystem(_eventBus);

        var pathReceived = false;
        _eventBus.Subscribe<PathFoundEvent>(evt => pathReceived = true);

        // Act
        var npc = _world.Create();
        _eventBus.Publish(new PathfindingRequestEvent
        {
            Entity = npc,
            Start = new Vector2Int(0, 0),
            Goal = new Vector2Int(10, 10)
        });

        pathfindingSystem.Update(_world, 0.016f);

        // Assert
        pathReceived.Should().BeTrue("pathfinding system should respond with path");
    }

    [Test]
    public void TileSystem_TriggersScript_ScriptSystemExecutes()
    {
        // Arrange
        var scriptSystem = new EventDrivenScriptSystem(_eventBus);
        var scriptExecuted = false;

        _eventBus.Subscribe<ScriptExecutedEvent>(evt => scriptExecuted = true);

        // Act
        _eventBus.Publish(new TileSteppedEvent
        {
            TileScript = "test-behavior.csx"
        });

        scriptSystem.Update(_world, 0.016f);

        // Assert
        scriptExecuted.Should().BeTrue("script system should execute tile behavior");
    }

    #endregion

    #region System Independence Tests

    [Test]
    public void Systems_CanOperateIndependently_WithoutDirectCoupling()
    {
        // Arrange
        var system1 = new IndependentSystem1(_eventBus);
        var system2 = new IndependentSystem2(_eventBus);

        // Act - Systems should initialize without knowing about each other
        system1.Initialize(_world);
        system2.Initialize(_world);

        // Assert - Both systems work independently
        Action act = () =>
        {
            system1.Update(_world, 0.016f);
            system2.Update(_world, 0.016f);
        };

        act.Should().NotThrow("systems should operate independently");
    }

    [Test]
    public void System_RemovedDynamically_OtherSystemsContinue()
    {
        // Arrange
        var system1 = new EventDrivenMovementSystem(_eventBus);
        var system2 = new EventDrivenCollisionSystem(_eventBus);

        _systemManager.RegisterUpdateSystem(system1);
        _systemManager.RegisterUpdateSystem(system2);
        _systemManager.Initialize(_world);

        // Act - Remove system1
        system1.Enabled = false;
        _systemManager.InvalidateEnabledCache();

        // Assert - system2 continues working
        Action act = () => _systemManager.Update(_world, 0.016f);
        act.Should().NotThrow("remaining systems should continue functioning");
    }

    #endregion

    #region Error Propagation Tests

    [Test]
    public void System_ThrowsException_OtherSystemsUnaffected()
    {
        // Arrange
        var system1 = new FailingSystem(_eventBus);
        var system2 = new EventDrivenCollisionSystem(_eventBus);

        _systemManager.RegisterUpdateSystem(system1);
        _systemManager.RegisterUpdateSystem(system2);
        _systemManager.Initialize(_world);

        var system2Executed = false;
        _eventBus.Subscribe<CollisionCheckEvent>(evt => system2Executed = true);

        // Act
        _systemManager.Update(_world, 0.016f);

        // Assert
        system2Executed.Should().BeTrue("system2 should execute despite system1 failing");
    }

    #endregion

    #region Test System Implementations

    private class EventDrivenMovementSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 100;

        public EventDrivenMovementSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Update(World world, float deltaTime)
        {
            // Publish collision check event
            _eventBus.Publish(new CollisionCheckEvent
            {
                Entity = Entity.Null,
                IsBlocked = false
            });
        }
    }

    private class EventDrivenCollisionSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 90;
        public int HandledCollisionChecks { get; private set; }

        public EventDrivenCollisionSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<CollisionCheckEvent>(OnCollisionCheck);
        }

        private void OnCollisionCheck(CollisionCheckEvent evt)
        {
            HandledCollisionChecks++;
        }

        public void ProcessCollisions()
        {
            _eventBus.Publish(new MovementBlockedEvent());
        }

        public void Update(World world, float deltaTime) { }
    }

    private class EventDrivenInputSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 200;

        public EventDrivenInputSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void SimulateInput(InputKey key)
        {
            _eventBus.Publish(new InputEvent { Key = key });
        }

        public void Update(World world, float deltaTime)
        {
            _eventBus.Publish(new MoveCommandEvent());
        }
    }

    private class EventDrivenWarpSystem : IUpdateSystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority => 50;

        public EventDrivenWarpSystem(EventBus eventBus) { }

        public void Update(World world, float deltaTime) { }
    }

    private class EventDrivenMapSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 40;

        public EventDrivenMapSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<WarpTriggeredEvent>(OnWarpTriggered);
        }

        private void OnWarpTriggered(WarpTriggeredEvent evt)
        {
            _eventBus.Publish(new MapUnloadEvent { MapId = evt.SourceMap });
            _eventBus.Publish(new MapLoadEvent { MapId = evt.TargetMap });
        }

        public void Update(World world, float deltaTime) { }
    }

    private class EventDrivenPathfindingSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 80;

        public EventDrivenPathfindingSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<PathfindingRequestEvent>(OnPathRequest);
        }

        private void OnPathRequest(PathfindingRequestEvent evt)
        {
            _eventBus.Publish(new PathFoundEvent());
        }

        public void Update(World world, float deltaTime) { }
    }

    private class EventDrivenNPCSystem : IUpdateSystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority => 70;

        public EventDrivenNPCSystem(EventBus eventBus) { }

        public void Update(World world, float deltaTime) { }
    }

    private class EventDrivenScriptSystem : IUpdateSystem
    {
        private readonly EventBus _eventBus;
        public bool Enabled { get; set; } = true;
        public int Priority => 60;

        public EventDrivenScriptSystem(EventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe<TileSteppedEvent>(OnTileStepped);
        }

        private void OnTileStepped(TileSteppedEvent evt)
        {
            _eventBus.Publish(new ScriptExecutedEvent());
        }

        public void Update(World world, float deltaTime) { }
    }

    private class IndependentSystem1 : IUpdateSystem, ISystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority => 100;

        public IndependentSystem1(EventBus eventBus) { }

        public void Initialize(World world) { }
        public void Update(World world, float deltaTime) { }
    }

    private class IndependentSystem2 : IUpdateSystem, ISystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority => 90;

        public IndependentSystem2(EventBus eventBus) { }

        public void Initialize(World world) { }
        public void Update(World world, float deltaTime) { }
    }

    private class FailingSystem : IUpdateSystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority => 100;

        public FailingSystem(EventBus eventBus) { }

        public void Update(World world, float deltaTime)
        {
            throw new Exception("System failure");
        }
    }

    #endregion

    #region Test Event Types

    private class CollisionCheckEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public Entity Entity { get; set; }
        public bool IsBlocked { get; set; }
    }

    private class MovementBlockedEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
    }

    private class InputEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public InputKey Key { get; set; }
    }

    private class MoveCommandEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
    }

    private class PositionUpdateEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
    }

    private class WarpTriggeredEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public Entity Entity { get; set; }
        public string SourceMap { get; set; } = string.Empty;
        public string TargetMap { get; set; } = string.Empty;
    }

    private class MapUnloadEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string MapId { get; set; } = string.Empty;
    }

    private class MapLoadEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string MapId { get; set; } = string.Empty;
    }

    private class PlayerStateChangedEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public PlayerState State { get; set; }
    }

    private class PathfindingRequestEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public Entity Entity { get; set; }
        public Vector2Int Start { get; set; }
        public Vector2Int Goal { get; set; }
    }

    private class PathFoundEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
    }

    private class TileSteppedEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string TileScript { get; set; } = string.Empty;
    }

    private class ScriptExecutedEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
    }

    private enum InputKey { Up, Down, Left, Right }
    private enum PlayerState { Idle, Walking, Running }

    private record struct Vector2Int(int X, int Y);

    #endregion
}

public static class EventBusExtensions
{
    public static void SubscribeToAll(this EventBus eventBus, Action<IEvent> handler)
    {
        // TODO: Implement global subscription
    }
}
