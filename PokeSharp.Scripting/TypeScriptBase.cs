using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;

namespace PokeSharp.Scripting;

/// <summary>
///     Base class for type behavior scripts using the ScriptContext pattern.
/// </summary>
/// <remarks>
///     <para>
///         All .csx behavior scripts should define a class that inherits from TypeScriptBase.
///         The class will be instantiated once and reused across all ticks.
///     </para>
///     <para>
///         <strong>IMPORTANT:</strong> Scripts are stateless! DO NOT use instance fields or properties.
///         Use <c>ctx.GetState&lt;T&gt;()</c> and <c>ctx.SetState&lt;T&gt;()</c> for persistent data.
///     </para>
///     <example>
///         <code>
/// public class MyScript : TypeScriptBase
/// {
///     // ❌ WRONG - instance state will break with multiple entities
///     private int counter;
///
///     // ✅ CORRECT - use ScriptContext for state
///     protected override void OnTick(ScriptContext ctx, float deltaTime)
///     {
///         var counter = ctx.GetState&lt;int&gt;("counter");
///         counter++;
///         ctx.SetState("counter", counter);
///
///         // Access ECS world, entity, logger via context
///         var position = ctx.World.Get&lt;Position&gt;(ctx.Entity);
///         ctx.Logger?.LogInformation("Position: {Pos}", position);
///     }
/// }
/// </code>
///     </example>
/// </remarks>
public abstract class TypeScriptBase
{
    // NO INSTANCE FIELDS OR PROPERTIES!
    // Scripts must be stateless - use ScriptContext.GetState<T>() for persistent data.

    // ============================================================================
    // Lifecycle Hooks
    // ============================================================================

    /// <summary>
    ///     Called once when script is loaded.
    ///     Override to set up initial state or cache data.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <remarks>
    ///     Use <c>ctx.SetState&lt;T&gt;(key, value)</c> to initialize persistent data.
    /// </remarks>
    protected virtual void OnInitialize(ScriptContext ctx) { }

    /// <summary>
    ///     Called when the type is activated on an entity or globally.
    ///     Override to handle activation logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnActivated(ScriptContext ctx) { }

    /// <summary>
    ///     Called every frame while the type is active.
    ///     Override to implement per-frame behavior logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <param name="deltaTime">Time elapsed since last frame (in seconds).</param>
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }

    /// <summary>
    ///     Called when the type is deactivated from an entity or globally.
    ///     Override to handle cleanup logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnDeactivated(ScriptContext ctx) { }

    // ============================================================================
    // Helper Methods (Static - No Instance State)
    // ============================================================================

    /// <summary>
    ///     Calculate direction from current position to target position.
    ///     Returns cardinal direction (no diagonals).
    /// </summary>
    /// <param name="from">Starting position.</param>
    /// <param name="to">Target position.</param>
    /// <returns>Direction to move to reach target.</returns>
    protected static Direction GetDirectionTo(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        // Prefer horizontal movement if tied
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? Direction.Right : Direction.Left;

        if (Math.Abs(dy) > Math.Abs(dx))
            return dy > 0 ? Direction.Down : Direction.Up;

        if (dx != 0)
            return dx > 0 ? Direction.Right : Direction.Left;

        if (dy != 0)
            return dy > 0 ? Direction.Down : Direction.Up;

        return Direction.None;
    }

    /// <summary>
    ///     Show a message to the player (placeholder for future dialogue system).
    /// </summary>
    /// <param name="ctx">Script execution context for logging.</param>
    /// <param name="message">The message text to display.</param>
    protected static void ShowMessage(ScriptContext ctx, string message)
    {
        // TODO: Integrate with dialogue system when implemented
        ctx.Logger?.LogInformation("[Script Message] {Message}", message);
    }

    /// <summary>
    ///     Play a sound effect (placeholder for future audio system).
    /// </summary>
    /// <param name="ctx">Script execution context for logging.</param>
    /// <param name="soundId">The sound identifier to play.</param>
    protected static void PlaySound(ScriptContext ctx, string soundId)
    {
        // TODO: Integrate with audio system when implemented
        ctx.Logger?.LogDebug("[Script Sound] {SoundId}", soundId);
    }

    /// <summary>
    ///     Spawn a visual effect at a position (placeholder for future particle system).
    /// </summary>
    /// <param name="ctx">Script execution context for logging.</param>
    /// <param name="effectId">The effect identifier to spawn.</param>
    /// <param name="position">The world position to spawn at.</param>
    protected static void SpawnEffect(ScriptContext ctx, string effectId, Point position)
    {
        // TODO: Integrate with particle/effect system when implemented
        ctx.Logger?.LogDebug("[Script Effect] {EffectId} at {Position}", effectId, position);
    }

    /// <summary>
    ///     Get random float between 0 and 1.
    /// </summary>
    /// <returns>Random float in range [0.0, 1.0).</returns>
    protected static float Random()
    {
        return (float)System.Random.Shared.NextDouble();
    }

    /// <summary>
    ///     Get random integer between min (inclusive) and max (exclusive).
    /// </summary>
    /// <param name="min">Minimum value (inclusive).</param>
    /// <param name="max">Maximum value (exclusive).</param>
    /// <returns>Random integer in range [min, max).</returns>
    protected static int RandomRange(int min, int max)
    {
        return System.Random.Shared.Next(min, max);
    }
}
