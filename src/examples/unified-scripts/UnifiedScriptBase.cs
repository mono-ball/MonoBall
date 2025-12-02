using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace PokeSharp.Scripting.Unified
{
    /// <summary>
    /// Universal base class for ALL scripts - tiles, NPCs, entities, items, everything!
    /// Event-driven architecture with optional polling.
    /// </summary>
    public abstract class UnifiedScriptBase
    {
        /// <summary>
        /// Script metadata - automatically populated by the scripting system
        /// </summary>
        public ScriptMetadata Metadata { get; set; }

        /// <summary>
        /// The game object this script is attached to (tile, NPC, entity, etc.)
        /// </summary>
        public IScriptable Target { get; set; }

        /// <summary>
        /// Script-specific data storage (persisted with save game)
        /// </summary>
        protected Dictionary<string, object> Data { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Reference to the game world
        /// </summary>
        protected IGameWorld World => Target?.World;

        /// <summary>
        /// Reference to the event system
        /// </summary>
        protected IEventSystem Events => World?.EventSystem;

        #region Lifecycle Methods (Override as needed)

        /// <summary>
        /// Called once when script is first loaded/attached
        /// Use this to register event listeners, initialize state, etc.
        /// </summary>
        public virtual void Initialize()
        {
            // Override in derived scripts
        }

        /// <summary>
        /// Called every frame (if enabled)
        /// Use sparingly! Events are preferred for performance.
        /// </summary>
        public virtual void Update(GameTime gameTime)
        {
            // Override only if you need frame-by-frame updates
        }

        /// <summary>
        /// Called when script is being removed/unloaded
        /// Use this to unregister event listeners, cleanup resources
        /// </summary>
        public virtual void Cleanup()
        {
            // Override in derived scripts
        }

        #endregion

        #region Event Registration Helpers

        /// <summary>
        /// Subscribe to an event with automatic cleanup tracking
        /// </summary>
        protected void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            Events?.Subscribe(handler);
            _subscriptions.Add(() => Events?.Unsubscribe(handler));
        }

        /// <summary>
        /// Subscribe to an event with a filter condition
        /// </summary>
        protected void SubscribeWhen<T>(Func<T, bool> filter, Action<T> handler) where T : IGameEvent
        {
            Action<T> filteredHandler = evt =>
            {
                if (filter(evt))
                    handler(evt);
            };
            Subscribe(filteredHandler);
        }

        /// <summary>
        /// Publish an event to the game
        /// </summary>
        protected void Publish<T>(T gameEvent) where T : IGameEvent
        {
            Events?.Publish(gameEvent);
        }

        private List<Action> _subscriptions = new List<Action>();

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get or set persistent script data
        /// </summary>
        protected T Get<T>(string key, T defaultValue = default)
        {
            return Data.TryGetValue(key, out var value) ? (T)value : defaultValue;
        }

        protected void Set(string key, object value)
        {
            Data[key] = value;
        }

        /// <summary>
        /// Check if player is near this script's target
        /// </summary>
        protected bool IsPlayerNearby(int maxDistance = 1)
        {
            if (Target == null || World?.Player == null)
                return false;

            var distance = Vector2.Distance(
                Target.Position.ToVector2(),
                World.Player.Position.ToVector2()
            );

            return distance <= maxDistance;
        }

        /// <summary>
        /// Get all entities of a specific type near this script's target
        /// </summary>
        protected IEnumerable<T> GetNearbyEntities<T>(int radius = 5) where T : IEntity
        {
            if (Target == null || World == null)
                return Enumerable.Empty<T>();

            return World.GetEntitiesInRadius(Target.Position, radius)
                .OfType<T>();
        }

        /// <summary>
        /// Schedule a delayed action (game ticks)
        /// </summary>
        protected void DelayedAction(int ticks, Action action)
        {
            var targetTick = World.CurrentTick + ticks;
            Subscribe<TickEvent>(evt =>
            {
                if (evt.TickNumber >= targetTick)
                {
                    action();
                }
            });
        }

        #endregion

        #region Internal Lifecycle (Called by Script System)

        internal void InternalInitialize()
        {
            Initialize();
        }

        internal void InternalUpdate(GameTime gameTime)
        {
            Update(gameTime);
        }

        internal void InternalCleanup()
        {
            Cleanup();

            // Unsubscribe from all events
            foreach (var unsubscribe in _subscriptions)
            {
                unsubscribe();
            }
            _subscriptions.Clear();
        }

        #endregion
    }

    #region Supporting Interfaces and Types

    public class ScriptMetadata
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public DateTime LoadedAt { get; set; }
        public bool RequiresUpdate { get; set; }
    }

    public interface IScriptable
    {
        Point Position { get; }
        IGameWorld World { get; }
        string Name { get; }
    }

    public interface IGameWorld
    {
        IEventSystem EventSystem { get; }
        IPlayer Player { get; }
        long CurrentTick { get; }
        IEnumerable<IEntity> GetEntitiesInRadius(Point position, int radius);
    }

    public interface IEventSystem
    {
        void Subscribe<T>(Action<T> handler) where T : IGameEvent;
        void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;
        void Publish<T>(T gameEvent) where T : IGameEvent;
    }

    public interface IGameEvent
    {
        DateTime Timestamp { get; }
    }

    public interface IEntity
    {
        Point Position { get; }
        string EntityId { get; }
    }

    public interface IPlayer : IEntity
    {
        int FacingDirection { get; }
        bool IsMoving { get; }
    }

    public class TickEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public long TickNumber { get; set; }
    }

    public class PlayerMoveEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public Point FromPosition { get; set; }
        public Point ToPosition { get; set; }
        public IPlayer Player { get; set; }
    }

    public class PlayerInteractEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public IPlayer Player { get; set; }
        public Point TargetPosition { get; set; }
        public IScriptable Target { get; set; }
    }

    public class EntityMoveEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public IEntity Entity { get; set; }
        public Point FromPosition { get; set; }
        public Point ToPosition { get; set; }
    }

    #endregion
}
