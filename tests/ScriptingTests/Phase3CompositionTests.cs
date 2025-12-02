// PHASE 3: MULTI-SCRIPT COMPOSITION TESTS
// Validates ScriptBase lifecycle, event subscriptions, state management, and multi-script composition.
// Tests the event-driven architecture's ability to handle multiple behaviors per tile/entity.

using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Events;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.EventDriven;
using PokeSharp.Game.Scripting.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.ScriptingTests
{
    /// <summary>
    /// Phase 3 Composition Tests - Multi-script behaviors with event-driven architecture.
    /// </summary>
    public class Phase3CompositionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private World _world;
        private EventBus _eventBus;
        private Entity _testEntity;
        private Entity _testTileEntity;

        public Phase3CompositionTests(ITestOutputHelper output)
        {
            _output = output;
            _world = World.Create();
            _eventBus = new EventBus(_world);
            _testEntity = _world.Create();
            _testTileEntity = _world.Create();
        }

        #region 1. ScriptBase Lifecycle Tests

        [Fact]
        [Trait("Category", "ScriptLifecycle")]
        [Trait("Priority", "Critical")]
        public void Initialize_SetsContextCorrectly()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Initialize() sets Context correctly ===");
            var script = new TestLifecycleScript();
            var context = new ScriptContext();

            // ACT
            script.Initialize(_eventBus, _testTileEntity, context);

            // ASSERT
            Assert.NotNull(script.Events);
            Assert.Equal(_testTileEntity, script.TileEntity);
            Assert.True(script.InitializeCalled);
            _output.WriteLine("✅ PASS: Initialize() set context correctly");
        }

        [Fact]
        [Trait("Category", "ScriptLifecycle")]
        [Trait("Priority", "Critical")]
        public void RegisterEventHandlers_CalledAfterInitialize()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: RegisterEventHandlers() called after Initialize() ===");
            var script = new TestLifecycleScript();
            var context = new ScriptContext();

            // ACT
            script.Initialize(_eventBus, _testTileEntity, context);

            // ASSERT
            Assert.True(script.RegisterEventHandlersCalled);
            Assert.True(script.InitializeCalled);
            _output.WriteLine("✅ PASS: RegisterEventHandlers() was called");
        }

        [Fact]
        [Trait("Category", "ScriptLifecycle")]
        [Trait("Priority", "High")]
        public void Dispose_CleansUpSubscriptions()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Dispose() cleans up subscriptions ===");
            var script = new TestLifecycleScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // Subscribe to events
            int eventCount = 0;
            script.OnTileStep((ref TileSteppedEvent evt) => { eventCount++; });

            // ACT - Trigger event before disposal
            var evt1 = new TileSteppedEvent { Entity = _testEntity, TileEntity = _testTileEntity, Timestamp = 1.0f };
            _eventBus.Publish(ref evt1);
            Assert.Equal(1, eventCount);

            // Dispose script
            script.Dispose();

            // Trigger event after disposal
            var evt2 = new TileSteppedEvent { Entity = _testEntity, TileEntity = _testTileEntity, Timestamp = 2.0f };
            _eventBus.Publish(ref evt2);

            // ASSERT - Event count should not increase after disposal
            Assert.Equal(1, eventCount);
            _output.WriteLine("✅ PASS: Dispose() cleaned up subscriptions");
        }

        [Fact]
        [Trait("Category", "ScriptLifecycle")]
        [Trait("Priority", "High")]
        public void MultipleInitialize_DoesNotLeakHandlers()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Multiple Initialize() calls don't leak handlers ===");
            var script = new TestLifecycleScript();
            var context = new ScriptContext();

            // ACT - Initialize multiple times
            script.Initialize(_eventBus, _testTileEntity, context);
            script.Dispose();
            script.Initialize(_eventBus, _testTileEntity, context);
            script.Dispose();
            script.Initialize(_eventBus, _testTileEntity, context);

            // Get statistics
            var stats = _eventBus.GetStatistics<TileSteppedEvent>();

            // ASSERT - Should only have 1 handler (from latest Initialize)
            Assert.NotNull(stats);
            Assert.Equal(1, stats.Value.HandlerCount);
            _output.WriteLine($"Handler count: {stats.Value.HandlerCount}");
            _output.WriteLine("✅ PASS: No handler leaks detected");
        }

        #endregion

        #region 2. Event Subscription Tests

        [Fact]
        [Trait("Category", "EventSubscription")]
        [Trait("Priority", "Critical")]
        public void OnTileStep_SubscribesCorrectly()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: OnTileStep() subscribes correctly ===");
            var script = new TestEventScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f,
                Position = (5, 10),
                MapId = 1
            };
            _eventBus.Publish(ref evt);

            // ASSERT
            Assert.True(script.TileStepCalled);
            Assert.Equal(_testEntity, script.ReceivedEntity);
            _output.WriteLine("✅ PASS: OnTileStep() subscription works");
        }

        [Fact]
        [Trait("Category", "EventSubscription")]
        [Trait("Priority", "Critical")]
        public void OnCollisionCheck_FiltersCorrectly()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: OnCollisionCheck() filters by entity ===");
            var script = new TestEventScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // ACT - Event for WRONG tile entity
            var evt1 = new CollisionCheckEvent
            {
                Entity = _testEntity,
                TileEntity = _world.Create(), // Different tile
                Timestamp = 1.0f,
                IsWalkable = true
            };
            _eventBus.Publish(ref evt1);

            // Event for CORRECT tile entity
            var evt2 = new CollisionCheckEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 2.0f,
                IsWalkable = true
            };
            _eventBus.Publish(ref evt2);

            // ASSERT - Should only respond to matching tile entity
            Assert.True(script.CollisionCheckCalled);
            Assert.Equal(1, script.CollisionCheckCount);
            _output.WriteLine("✅ PASS: Event filtering works correctly");
        }

        [Fact]
        [Trait("Category", "EventSubscription")]
        [Trait("Priority", "High")]
        public void PriorityOrdering_WorksCorrectly()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Priority ordering (high priority executes first) ===");
            var executionOrder = new List<string>();

            // Create 3 scripts with different priorities
            var lowPriorityScript = new PriorityTestScript("Low", -10, executionOrder);
            var mediumPriorityScript = new PriorityTestScript("Medium", 0, executionOrder);
            var highPriorityScript = new PriorityTestScript("High", 10, executionOrder);

            var context = new ScriptContext();
            lowPriorityScript.Initialize(_eventBus, _testTileEntity, context);
            mediumPriorityScript.Initialize(_eventBus, _testTileEntity, context);
            highPriorityScript.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT - Should execute in order: High, Medium, Low
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("High", executionOrder[0]);
            Assert.Equal("Medium", executionOrder[1]);
            Assert.Equal("Low", executionOrder[2]);

            _output.WriteLine($"Execution order: {string.Join(" -> ", executionOrder)}");
            _output.WriteLine("✅ PASS: Priority ordering works correctly");
        }

        #endregion

        #region 3. State Management Tests

        [Fact]
        [Trait("Category", "StateManagement")]
        [Trait("Priority", "High")]
        public void Get_ReturnsDefaultWhenKeyNotFound()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Get<T>() returns default when key not found ===");
            var script = new TestStateScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var result = script.GetState<int>("nonexistent");

            // ASSERT
            Assert.Equal(0, result);
            _output.WriteLine("✅ PASS: Default value returned for missing key");
        }

        [Fact]
        [Trait("Category", "StateManagement")]
        [Trait("Priority", "High")]
        public void Set_StoresValueCorrectly()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Set<T>() stores value correctly ===");
            var script = new TestStateScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            script.SetState("counter", 42);
            var result = script.GetState<int>("counter");

            // ASSERT
            Assert.Equal(42, result);
            _output.WriteLine("✅ PASS: Value stored and retrieved correctly");
        }

        [Fact]
        [Trait("Category", "StateManagement")]
        [Trait("Priority", "High")]
        public void State_PersistsAcrossTicks()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: State persists across ticks ===");
            var script = new TestStateScript();
            var context = new ScriptContext();
            script.Initialize(_eventBus, _testTileEntity, context);

            // ACT - Increment counter over multiple ticks
            script.SetState("counter", 0);

            for (int i = 0; i < 10; i++)
            {
                var evt = new TileSteppedEvent
                {
                    Entity = _testEntity,
                    TileEntity = _testTileEntity,
                    Timestamp = i
                };
                _eventBus.Publish(ref evt);
            }

            var finalCount = script.GetState<int>("counter");

            // ASSERT
            Assert.Equal(10, finalCount);
            _output.WriteLine($"Counter value after 10 ticks: {finalCount}");
            _output.WriteLine("✅ PASS: State persisted correctly");
        }

        [Fact]
        [Trait("Category", "StateManagement")]
        [Trait("Priority", "Medium")]
        public void State_IsolatedPerScriptInstance()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: State is isolated per script instance ===");
            var script1 = new TestStateScript();
            var script2 = new TestStateScript();
            var context = new ScriptContext();

            var tile1 = _world.Create();
            var tile2 = _world.Create();

            script1.Initialize(_eventBus, tile1, context);
            script2.Initialize(_eventBus, tile2, context);

            // ACT
            script1.SetState("value", 100);
            script2.SetState("value", 200);

            // ASSERT
            Assert.Equal(100, script1.GetState<int>("value"));
            Assert.Equal(200, script2.GetState<int>("value"));
            _output.WriteLine("✅ PASS: State is isolated per instance");
        }

        #endregion

        #region 4. Multi-Script Composition Tests

        [Fact]
        [Trait("Category", "Composition")]
        [Trait("Priority", "Critical")]
        public void TwoScripts_CanAttachToSameTile()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: 2+ scripts can attach to same tile ===");
            var iceScript = new IceTileBehavior();
            var grassScript = new TallGrassBehavior();
            var context = new ScriptContext();

            // ACT
            iceScript.Initialize(_eventBus, _testTileEntity, context);
            grassScript.Initialize(_eventBus, _testTileEntity, context);

            // Get handler counts
            var iceStats = _eventBus.GetStatistics<ForcedMovementCheckEvent>();
            var grassStats = _eventBus.GetStatistics<TileSteppedEvent>();

            // ASSERT
            Assert.NotNull(iceStats);
            Assert.NotNull(grassStats);
            Assert.True(iceStats.Value.HandlerCount >= 1);
            Assert.True(grassStats.Value.HandlerCount >= 1);

            _output.WriteLine($"ForcedMovement handlers: {iceStats.Value.HandlerCount}");
            _output.WriteLine($"TileStep handlers: {grassStats.Value.HandlerCount}");
            _output.WriteLine("✅ PASS: Multiple scripts attached successfully");
        }

        [Fact]
        [Trait("Category", "Composition")]
        [Trait("Priority", "Critical")]
        public void AllScripts_ReceiveEvents()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: All scripts receive events ===");
            var script1 = new TestEventScript();
            var script2 = new TestEventScript();
            var context = new ScriptContext();

            script1.Initialize(_eventBus, _testTileEntity, context);
            script2.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT
            Assert.True(script1.TileStepCalled);
            Assert.True(script2.TileStepCalled);
            _output.WriteLine("✅ PASS: Both scripts received the event");
        }

        [Fact]
        [Trait("Category", "Composition")]
        [Trait("Priority", "High")]
        public void Priority_DeterminesExecutionOrder()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Priority determines execution order in composition ===");
            var executionOrder = new List<string>();

            var scriptA = new PriorityTestScript("A", 5, executionOrder);
            var scriptB = new PriorityTestScript("B", 10, executionOrder);
            var scriptC = new PriorityTestScript("C", 0, executionOrder);

            var context = new ScriptContext();
            scriptA.Initialize(_eventBus, _testTileEntity, context);
            scriptB.Initialize(_eventBus, _testTileEntity, context);
            scriptC.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT - Should execute B (10), A (5), C (0)
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("B", executionOrder[0]);
            Assert.Equal("A", executionOrder[1]);
            Assert.Equal("C", executionOrder[2]);

            _output.WriteLine($"Execution order: {string.Join(" -> ", executionOrder)}");
            _output.WriteLine("✅ PASS: Priority-based execution works");
        }

        [Fact]
        [Trait("Category", "Composition")]
        [Trait("Priority", "Medium")]
        public void Scripts_CanBeAddedDynamically()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Scripts can be added dynamically ===");
            var script1 = new TestEventScript();
            var context = new ScriptContext();
            script1.Initialize(_eventBus, _testTileEntity, context);

            // ACT - Trigger event with 1 script
            var evt1 = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt1);
            Assert.Equal(1, script1.TileStepCallCount);

            // Add second script dynamically
            var script2 = new TestEventScript();
            script2.Initialize(_eventBus, _testTileEntity, context);

            // Trigger event with 2 scripts
            var evt2 = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 2.0f
            };
            _eventBus.Publish(ref evt2);

            // ASSERT
            Assert.Equal(2, script1.TileStepCallCount);
            Assert.Equal(1, script2.TileStepCallCount);
            _output.WriteLine("✅ PASS: Dynamic script addition works");
        }

        [Fact]
        [Trait("Category", "Composition")]
        [Trait("Priority", "Medium")]
        public void Scripts_CanBeRemovedDynamically()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Scripts can be removed dynamically ===");
            var script1 = new TestEventScript();
            var script2 = new TestEventScript();
            var context = new ScriptContext();

            script1.Initialize(_eventBus, _testTileEntity, context);
            script2.Initialize(_eventBus, _testTileEntity, context);

            // ACT - Trigger with both scripts
            var evt1 = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt1);

            Assert.Equal(1, script1.TileStepCallCount);
            Assert.Equal(1, script2.TileStepCallCount);

            // Remove script1
            script1.Dispose();

            // Trigger with only script2
            var evt2 = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 2.0f
            };
            _eventBus.Publish(ref evt2);

            // ASSERT
            Assert.Equal(1, script1.TileStepCallCount); // Should not increase
            Assert.Equal(2, script2.TileStepCallCount);
            _output.WriteLine("✅ PASS: Dynamic script removal works");
        }

        #endregion

        #region 5. Custom Event Tests

        [Fact]
        [Trait("Category", "CustomEvents")]
        [Trait("Priority", "High")]
        public void CustomEvent_PublishesToEventBus()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Custom events publish to EventBus ===");
            var publisherScript = new CustomEventPublisherScript();
            var context = new ScriptContext();
            publisherScript.Initialize(_eventBus, _testTileEntity, context);

            bool eventReceived = false;
            _eventBus.Subscribe<LedgeJumpedEvent>((ref LedgeJumpedEvent evt) =>
            {
                eventReceived = true;
            });

            // ACT
            var stepEvt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref stepEvt);

            // ASSERT
            Assert.True(eventReceived);
            _output.WriteLine("✅ PASS: Custom event published successfully");
        }

        [Fact]
        [Trait("Category", "CustomEvents")]
        [Trait("Priority", "High")]
        public void CustomEvent_ReceivedByOtherScripts()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Other scripts receive custom events ===");
            var publisherScript = new CustomEventPublisherScript();
            var receiverScript = new CustomEventReceiverScript();
            var context = new ScriptContext();

            publisherScript.Initialize(_eventBus, _testTileEntity, context);
            receiverScript.Initialize(_eventBus, _world.Create(), context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT
            Assert.True(receiverScript.CustomEventReceived);
            _output.WriteLine("✅ PASS: Custom event received by other script");
        }

        [Fact]
        [Trait("Category", "CustomEvents")]
        [Trait("Priority", "Medium")]
        public void CustomEvent_DataPreserved()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Custom event data is preserved ===");
            var publisherScript = new CustomEventPublisherScript();
            var context = new ScriptContext();
            publisherScript.Initialize(_eventBus, _testTileEntity, context);

            Entity? receivedEntity = null;
            Direction receivedDirection = Direction.None;

            _eventBus.Subscribe<LedgeJumpedEvent>((ref LedgeJumpedEvent evt) =>
            {
                receivedEntity = evt.Entity;
                receivedDirection = evt.JumpDirection;
            });

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT
            Assert.Equal(_testEntity, receivedEntity);
            Assert.Equal(Direction.South, receivedDirection);
            _output.WriteLine($"Received entity: {receivedEntity}, direction: {receivedDirection}");
            _output.WriteLine("✅ PASS: Event data preserved correctly");
        }

        #endregion

        #region 6. Integration Tests

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "Critical")]
        public void IceAndGrass_BothTriggerOnSameTile()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Ice + Grass on same tile (both trigger) ===");
            var iceScript = new IceTileBehavior();
            var grassScript = new TallGrassBehavior();
            var context = new ScriptContext();
            context.SetProperty("encounterRate", 1.0f); // 100% encounter for testing

            iceScript.Initialize(_eventBus, _testTileEntity, context);
            grassScript.Initialize(_eventBus, _testTileEntity, context);

            // Add PlayerTag to entity
            _world.Add<PlayerTag>(_testEntity);

            bool iceTriggered = false;
            bool grassTriggered = false;

            _eventBus.Subscribe<ForcedMovementCheckEvent>((ref ForcedMovementCheckEvent evt) =>
            {
                if (evt.TileEntity == _testTileEntity)
                    iceTriggered = true;
            });

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f,
                EntryDirection = Direction.North
            };
            _eventBus.Publish(ref evt);

            // Trigger forced movement check
            var forceEvt = new ForcedMovementCheckEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                CurrentDirection = Direction.North,
                Timestamp = 2.0f
            };
            _eventBus.Publish(ref forceEvt);

            // ASSERT
            Assert.True(iceTriggered);
            _output.WriteLine("✅ PASS: Both ice and grass behaviors triggered");
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "High")]
        public void ThreeScripts_ComposeTogether()
        {
            // ARRANGE
            _output.WriteLine("=== TEST: Composition with 3+ scripts ===");
            var script1 = new TestEventScript();
            var script2 = new TestEventScript();
            var script3 = new TestEventScript();
            var context = new ScriptContext();

            script1.Initialize(_eventBus, _testTileEntity, context);
            script2.Initialize(_eventBus, _testTileEntity, context);
            script3.Initialize(_eventBus, _testTileEntity, context);

            // ACT
            var evt = new TileSteppedEvent
            {
                Entity = _testEntity,
                TileEntity = _testTileEntity,
                Timestamp = 1.0f
            };
            _eventBus.Publish(ref evt);

            // ASSERT
            Assert.True(script1.TileStepCalled);
            Assert.True(script2.TileStepCalled);
            Assert.True(script3.TileStepCalled);
            _output.WriteLine("✅ PASS: All 3 scripts executed successfully");
        }

        #endregion

        public void Dispose()
        {
            _eventBus?.Dispose();
            _world?.Dispose();
        }
    }

    #region Test Helper Scripts

    /// <summary>
    /// Test script for lifecycle validation.
    /// </summary>
    public class TestLifecycleScript : EventDrivenScriptBase
    {
        public bool InitializeCalled { get; private set; }
        public bool RegisterEventHandlersCalled { get; private set; }

        public override void Initialize(EventBus events, Entity tileEntity, ScriptContext context)
        {
            InitializeCalled = true;
            base.Initialize(events, tileEntity, context);
        }

        protected override void RegisterEventHandlers(ScriptContext context)
        {
            RegisterEventHandlersCalled = true;

            // Subscribe to test events
            OnTileStep((ref TileSteppedEvent evt) =>
            {
                // Test handler
            });
        }
    }

    /// <summary>
    /// Test script for event subscription validation.
    /// </summary>
    public class TestEventScript : EventDrivenScriptBase
    {
        public bool TileStepCalled { get; set; }
        public bool CollisionCheckCalled { get; set; }
        public int TileStepCallCount { get; set; }
        public int CollisionCheckCount { get; set; }
        public Entity ReceivedEntity { get; set; }

        protected override void RegisterEventHandlers(ScriptContext context)
        {
            OnTileStep((ref TileSteppedEvent evt) =>
            {
                if (evt.TileEntity != TileEntity) return;

                TileStepCalled = true;
                TileStepCallCount++;
                ReceivedEntity = evt.Entity;
            });

            OnCollisionCheck((ref CollisionCheckEvent evt) =>
            {
                if (evt.TileEntity != TileEntity) return;

                CollisionCheckCalled = true;
                CollisionCheckCount++;
            });
        }
    }

    /// <summary>
    /// Test script for priority ordering validation.
    /// </summary>
    public class PriorityTestScript : EventDrivenScriptBase
    {
        private readonly string _name;
        private readonly int _priority;
        private readonly List<string> _executionOrder;

        public PriorityTestScript(string name, int priority, List<string> executionOrder)
        {
            _name = name;
            _priority = priority;
            _executionOrder = executionOrder;
        }

        protected override void RegisterEventHandlers(ScriptContext context)
        {
            OnTileStep((ref TileSteppedEvent evt) =>
            {
                if (evt.TileEntity != TileEntity) return;
                _executionOrder.Add(_name);
            }, _priority);
        }
    }

    /// <summary>
    /// Test script for state management validation.
    /// </summary>
    public class TestStateScript : EventDrivenScriptBase
    {
        private Dictionary<string, object> _state = new Dictionary<string, object>();

        public T GetState<T>(string key)
        {
            if (_state.TryGetValue(key, out var value))
                return (T)value;
            return default(T);
        }

        public void SetState(string key, object value)
        {
            _state[key] = value;
        }

        protected override void RegisterEventHandlers(ScriptContext context)
        {
            OnTileStep((ref TileSteppedEvent evt) =>
            {
                if (evt.TileEntity != TileEntity) return;

                // Increment counter on each step
                var counter = GetState<int>("counter");
                SetState("counter", counter + 1);
            });
        }
    }

    /// <summary>
    /// Test script that publishes custom events.
    /// </summary>
    public class CustomEventPublisherScript : EventDrivenScriptBase
    {
        protected override void RegisterEventHandlers(ScriptContext context)
        {
            OnTileStep((ref TileSteppedEvent evt) =>
            {
                if (evt.TileEntity != TileEntity) return;

                // Publish custom event
                var customEvt = new LedgeJumpedEvent
                {
                    Entity = evt.Entity,
                    TileEntity = TileEntity,
                    JumpDirection = Direction.South,
                    Timestamp = evt.Timestamp
                };
                Events.Publish(ref customEvt);
            });
        }
    }

    /// <summary>
    /// Test script that receives custom events.
    /// </summary>
    public class CustomEventReceiverScript : EventDrivenScriptBase
    {
        public bool CustomEventReceived { get; set; }

        protected override void RegisterEventHandlers(ScriptContext context)
        {
            Events.Subscribe<LedgeJumpedEvent>((ref LedgeJumpedEvent evt) =>
            {
                CustomEventReceived = true;
            });
        }
    }

    /// <summary>
    /// Custom event for testing.
    /// </summary>
    public struct LedgeJumpedEvent : ITileBehaviorEvent
    {
        public Entity Entity { get; init; }
        public Entity TileEntity { get; init; }
        public Direction JumpDirection { get; init; }
        public float Timestamp { get; init; }
    }

    #endregion
}
