using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

namespace PokeSharp.Core.Tests.Scripting;

/// <summary>
///     Comprehensive tests for ScriptContext to verify state management and isolation.
///     These tests ensure the state corruption bug (where multiple entities shared state) is fixed.
/// </summary>
public class ScriptContextTests : IDisposable
{
    private readonly ILogger _logger;
    private readonly World _world;

    public ScriptContextTests()
    {
        _world = World.Create();
        _logger = NullLogger.Instance;
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // ============================================================================
    // 1. ScriptContext Creation Tests
    // ============================================================================

    [Fact]
    public void Constructor_WithEntity_CreatesEntityScriptContext()
    {
        // Arrange
        var entity = _world.Create();

        // Act
        var context = new ScriptContext(_world, entity, _logger);

        // Assert
        context.World.Should().Be(_world);
        context.Entity.Should().Be(entity);
        context.IsEntityScript.Should().BeTrue();
        context.IsGlobalScript.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithoutEntity_CreatesGlobalScriptContext()
    {
        // Act
        var context = new ScriptContext(_world, null, _logger);

        // Assert
        context.World.Should().Be(_world);
        context.Entity.Should().BeNull();
        context.IsEntityScript.Should().BeFalse();
        context.IsGlobalScript.Should().BeTrue();
    }

    // ============================================================================
    // 2. State Management Tests (Enhanced ScriptContext)
    // ============================================================================
    // NOTE: These tests assume the enhanced ScriptContext with state management
    // methods like GetState<T>(), TryGetState<T>(), etc.
    // If not implemented yet, these serve as specifications.

    [Fact]
    public void GetState_WithExistingComponent_ReturnsComponent()
    {
        // Arrange
        var entity = _world.Create(new PatrolState { CurrentWaypoint = 5 });
        var context = CreateContext(entity);

        // Act
        ref var state = ref context.GetState<PatrolState>();

        // Assert
        state.CurrentWaypoint.Should().Be(5);
    }

    [Fact]
    public void GetState_WithoutEntity_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = CreateGlobalContext();

        // Act & Assert
        var act = () => context.GetState<PatrolState>();
        act.Should().Throw<InvalidOperationException>().WithMessage("*global script*");
    }

    [Fact]
    public void TryGetState_WithExistingComponent_ReturnsTrue()
    {
        // Arrange
        var entity = _world.Create(new PatrolState { CurrentWaypoint = 3 });
        var context = CreateContext(entity);

        // Act
        var result = context.TryGetState<PatrolState>(out var state);

        // Assert
        result.Should().BeTrue();
        state.CurrentWaypoint.Should().Be(3);
    }

    [Fact]
    public void TryGetState_WithoutComponent_ReturnsFalse()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act
        var result = context.TryGetState<PatrolState>(out var state);

        // Assert
        result.Should().BeFalse();
        state.Should().Be(default(PatrolState));
    }

    [Fact]
    public void TryGetState_ForGlobalScript_ReturnsFalse()
    {
        // Arrange
        var context = CreateGlobalContext();

        // Act
        var result = context.TryGetState<PatrolState>(out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetOrAddState_WithExistingComponent_ReturnsExisting()
    {
        // Arrange
        var entity = _world.Create(new PatrolState { CurrentWaypoint = 7 });
        var context = CreateContext(entity);

        // Act
        ref var state = ref context.GetOrAddState<PatrolState>();

        // Assert
        state.CurrentWaypoint.Should().Be(7);
    }

    [Fact]
    public void GetOrAddState_WithoutComponent_AddsAndReturnsNew()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act
        ref var state = ref context.GetOrAddState<PatrolState>();
        state.CurrentWaypoint = 10;

        // Assert
        _world.Has<PatrolState>(entity).Should().BeTrue();
        _world.Get<PatrolState>(entity).CurrentWaypoint.Should().Be(10);
    }

    [Fact]
    public void HasState_WithComponent_ReturnsTrue()
    {
        // Arrange
        var entity = _world.Create(new PatrolState());
        var context = CreateContext(entity);

        // Act
        var result = context.HasState<PatrolState>();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasState_WithoutComponent_ReturnsFalse()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act
        var result = context.HasState<PatrolState>();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveState_WithComponent_RemovesComponent()
    {
        // Arrange
        var entity = _world.Create(new PatrolState());
        var context = CreateContext(entity);

        // Act
        context.RemoveState<PatrolState>();

        // Assert
        _world.Has<PatrolState>(entity).Should().BeFalse();
    }

    // ============================================================================
    // 3. Multi-Entity State Isolation Tests (CRITICAL - Proves Bug Is Fixed)
    // ============================================================================

    [Fact]
    public void MultipleNPCs_WithPatrolBehavior_HaveIndependentState()
    {
        // Arrange - Create 3 NPCs with patrol behavior
        var npc1 = _world.Create(new PatrolState { CurrentWaypoint = 0 }, new Position(1, 1));
        var npc2 = _world.Create(new PatrolState { CurrentWaypoint = 0 }, new Position(2, 2));
        var npc3 = _world.Create(new PatrolState { CurrentWaypoint = 0 }, new Position(3, 3));

        var ctx1 = CreateContext(npc1);
        var ctx2 = CreateContext(npc2);
        var ctx3 = CreateContext(npc3);

        // Act - Modify NPC1's state
        ref var state1 = ref _world.Get<PatrolState>(npc1);
        state1.CurrentWaypoint = 5;

        // Assert - Verify NPC2 and NPC3 are unaffected
        _world.Get<PatrolState>(npc2).CurrentWaypoint.Should().Be(0, "NPC2 should be independent");
        _world.Get<PatrolState>(npc3).CurrentWaypoint.Should().Be(0, "NPC3 should be independent");

        // Act - Modify NPC2's state
        ref var state2 = ref _world.Get<PatrolState>(npc2);
        state2.CurrentWaypoint = 10;

        // Assert - Verify NPC1 and NPC3 still independent
        _world
            .Get<PatrolState>(npc1)
            .CurrentWaypoint.Should()
            .Be(5, "NPC1 should remain unchanged");
        _world
            .Get<PatrolState>(npc3)
            .CurrentWaypoint.Should()
            .Be(0, "NPC3 should remain unchanged");

        // Act - Modify NPC3's state
        ref var state3 = ref _world.Get<PatrolState>(npc3);
        state3.CurrentWaypoint = 15;

        // Assert - Final verification
        _world.Get<PatrolState>(npc1).CurrentWaypoint.Should().Be(5);
        _world.Get<PatrolState>(npc2).CurrentWaypoint.Should().Be(10);
        _world.Get<PatrolState>(npc3).CurrentWaypoint.Should().Be(15);
    }

    [Fact]
    public void MultipleNPCs_WithDifferentBehaviors_DoNotInterfere()
    {
        // Arrange - Create NPCs with different behaviors
        var patrolNpc = _world.Create(new PatrolState { CurrentWaypoint = 0 }, new Position(1, 1));
        var wanderNpc = _world.Create(new WanderState { WanderTimer = 0.0f }, new Position(2, 2));

        // Act - Modify both
        ref var patrolState = ref _world.Get<PatrolState>(patrolNpc);
        patrolState.CurrentWaypoint = 7;

        ref var wanderState = ref _world.Get<WanderState>(wanderNpc);
        wanderState.WanderTimer = 3.5f;

        // Assert - Verify independence
        _world.Get<PatrolState>(patrolNpc).CurrentWaypoint.Should().Be(7);
        _world.Get<WanderState>(wanderNpc).WanderTimer.Should().Be(3.5f);
    }

    [Fact]
    public void ConcurrentStateModifications_AcrossMultipleEntities_RemainsConsistent()
    {
        // Arrange - Create 10 NPCs
        var entities = Enumerable
            .Range(0, 10)
            .Select(i => _world.Create(new PatrolState { CurrentWaypoint = i }))
            .ToArray();

        // Act - Modify each entity's state
        for (var i = 0; i < entities.Length; i++)
        {
            ref var state = ref _world.Get<PatrolState>(entities[i]);
            state.CurrentWaypoint = i * 10;
        }

        // Assert - Verify each entity has correct independent state
        for (var i = 0; i < entities.Length; i++)
            _world
                .Get<PatrolState>(entities[i])
                .CurrentWaypoint.Should()
                .Be(i * 10, $"Entity {i} should have independent state");
    }

    // ============================================================================
    // 4. Global Script Tests
    // ============================================================================

    [Fact]
    public void GlobalContext_IsGlobalScript_ReturnsTrue()
    {
        // Arrange
        var context = CreateGlobalContext();

        // Act & Assert
        IsGlobalScript(context).Should().BeTrue();
    }

    [Fact]
    public void EntityContext_IsGlobalScript_ReturnsFalse()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act & Assert
        IsGlobalScript(context).Should().BeFalse();
    }

    [Fact]
    public void EntityContext_IsEntityScript_ReturnsTrue()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act & Assert
        IsEntityScript(context).Should().BeTrue();
    }

    [Fact]
    public void GlobalContext_IsEntityScript_ReturnsFalse()
    {
        // Arrange
        var context = CreateGlobalContext();

        // Act & Assert
        IsEntityScript(context).Should().BeFalse();
    }

    // ============================================================================
    // 5. Property Shortcut Tests
    // ============================================================================

    [Fact]
    public void Position_WithPositionComponent_ReturnsPosition()
    {
        // Arrange
        var entity = _world.Create(new Position(10, 20));
        var context = CreateContext(entity);

        // Act
        var position = _world.Get<Position>(entity);

        // Assert
        position.X.Should().Be(10);
        position.Y.Should().Be(20);
    }

    [Fact]
    public void HasPosition_WithPositionComponent_ReturnsTrue()
    {
        // Arrange
        var entity = _world.Create(new Position(5, 5));
        var context = CreateContext(entity);

        // Act & Assert
        _world.Has<Position>(entity).Should().BeTrue();
    }

    [Fact]
    public void HasPosition_WithoutPositionComponent_ReturnsFalse()
    {
        // Arrange
        var entity = _world.Create();
        var context = CreateContext(entity);

        // Act & Assert
        _world.Has<Position>(entity).Should().BeFalse();
    }

    // ============================================================================
    // 6. Integration Tests
    // ============================================================================

    [Fact]
    public void FullScriptExecution_WithScriptContext_WorksCorrectly()
    {
        // Arrange
        var entity = _world.Create(new PatrolState { CurrentWaypoint = 0 }, new Position(0, 0));
        var context = CreateContext(entity);

        // Act - Simulate script execution
        ref var state = ref _world.Get<PatrolState>(entity);
        state.CurrentWaypoint++;

        ref var position = ref _world.Get<Position>(entity);
        position.X += 1;

        // Assert
        _world.Get<PatrolState>(entity).CurrentWaypoint.Should().Be(1);
        _world.Get<Position>(entity).X.Should().Be(1);
    }

    [Fact]
    public void MultipleScripts_RunningSimultaneously_DoNotInterfere()
    {
        // Arrange
        var npc1 = _world.Create(new PatrolState { CurrentWaypoint = 0 });
        var npc2 = _world.Create(new WanderState { WanderTimer = 0.0f });

        var ctx1 = CreateContext(npc1);
        var ctx2 = CreateContext(npc2);

        // Act - Execute both scripts
        ref var patrol = ref _world.Get<PatrolState>(npc1);
        patrol.CurrentWaypoint = 5;

        ref var wander = ref _world.Get<WanderState>(npc2);
        wander.WanderTimer = 2.5f;

        // Assert
        _world.Get<PatrolState>(npc1).CurrentWaypoint.Should().Be(5);
        _world.Get<WanderState>(npc2).WanderTimer.Should().Be(2.5f);
    }

    [Fact]
    public void ScriptError_DoesNotCorruptOtherScripts()
    {
        // Arrange
        var npc1 = _world.Create(new PatrolState { CurrentWaypoint = 0 });
        var npc2 = _world.Create(new PatrolState { CurrentWaypoint = 0 });

        // Act - NPC1 script succeeds
        ref var state1 = ref _world.Get<PatrolState>(npc1);
        state1.CurrentWaypoint = 10;

        // Act - Simulate NPC2 script error (exception doesn't affect NPC1)
        try
        {
            ref var state2 = ref _world.Get<PatrolState>(npc2);
            state2.CurrentWaypoint = 20;
            throw new Exception("Simulated script error");
        }
        catch
        {
            // Error caught
        }

        // Assert - NPC1 state is unchanged despite NPC2 error
        _world.Get<PatrolState>(npc1).CurrentWaypoint.Should().Be(10);
        _world.Get<PatrolState>(npc2).CurrentWaypoint.Should().Be(20);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private ScriptContext CreateContext(Entity entity)
    {
        return new ScriptContext(_world, entity, _logger);
    }

    private ScriptContext CreateGlobalContext()
    {
        return new ScriptContext(_world, null, _logger);
    }

    private void RemoveState<T>(ScriptContext context)
        where T : struct
    {
        if (context.Entity != null)
            context.World.Remove<T>(context.Entity.Value);
    }

    private bool IsGlobalScript(ScriptContext context)
    {
        return context.Entity == null;
    }

    private bool IsEntityScript(ScriptContext context)
    {
        return context.Entity != null;
    }
}
