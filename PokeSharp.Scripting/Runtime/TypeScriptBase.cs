using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Scripting.Runtime;

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
    public virtual void OnInitialize(ScriptContext ctx)
    {
    }

    /// <summary>
    ///     Called when the type is activated on an entity or globally.
    ///     Override to handle activation logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnActivated(ScriptContext ctx)
    {
    }

    /// <summary>
    ///     Called every frame while the type is active.
    ///     Override to implement per-frame behavior logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <param name="deltaTime">Time elapsed since last frame (in seconds).</param>
    public virtual void OnTick(ScriptContext ctx, float deltaTime)
    {
    }

    /// <summary>
    ///     Called when the type is deactivated from an entity or globally.
    ///     Override to handle cleanup logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnDeactivated(ScriptContext ctx)
    {
    }

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
    ///     Show a message to the player via the dialogue system.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to services.</param>
    /// <param name="message">The message text to display.</param>
    /// <param name="speakerName">Optional speaker name for dialogue attribution.</param>
    /// <param name="priority">Display priority (higher values show first). Default is 0.</param>
    /// <remarks>
    ///     This method publishes a DialogueRequestEvent to the event bus, allowing UI systems
    ///     to subscribe and display messages in their preferred style (dialogue box, notification, etc.).
    ///     If no dialogue system is registered, the message is logged as a fallback.
    /// </remarks>
    /// <example>
    ///     <code>
    /// ShowMessage(ctx, "Welcome to Pallet Town!");
    /// ShowMessage(ctx, "I'm Professor Oak", speakerName: "Professor Oak");
    /// ShowMessage(ctx, "CRITICAL ALERT", priority: 10);
    /// </code>
    /// </example>
    protected static void ShowMessage(
        ScriptContext ctx,
        string message,
        string? speakerName = null,
        int priority = 0
    )
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        if (string.IsNullOrWhiteSpace(message))
        {
            ctx.Logger?.LogWarning("Attempted to show null or empty message from script");
            return;
        }

        try
        {
            // Get the dialogue system from ScriptContext services
            // This uses dependency injection - the system is provided by the game host
            var dialogueSystem = ctx.WorldApi as IDialogueSystem;

            if (dialogueSystem != null)
                dialogueSystem.ShowMessage(message, speakerName, priority);
            else
                // Fallback: Log the message if no dialogue system is registered
                // This ensures scripts don't break even if the system isn't set up yet
                ctx.Logger?.LogInformation(
                    "[Script Message] {Message} (Speaker: {Speaker}, Priority: {Priority})",
                    message,
                    speakerName ?? "None",
                    priority
                );
        }
        catch (Exception ex)
        {
            ctx.Logger?.LogError(
                ex,
                "Failed to show message: {Message}. Error: {Error}",
                message,
                ex.Message
            );
        }
    }

    /// <summary>
    ///     Spawn a visual effect at a position via the effect system.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to services.</param>
    /// <param name="effectId">The effect identifier (effect name or type).</param>
    /// <param name="position">The world position to spawn at (in grid coordinates).</param>
    /// <param name="duration">Duration in seconds (0 for one-shot effects). Default is 0.</param>
    /// <param name="scale">Scale multiplier for the effect. Default is 1.0.</param>
    /// <param name="tint">Optional color tint for the effect. Default is null (no tint).</param>
    /// <remarks>
    ///     This method publishes an EffectRequestEvent to the event bus, allowing particle systems,
    ///     sprite animators, or shader effects to subscribe and render the effect.
    ///     If no effect system is registered, the request is logged as a fallback.
    /// </remarks>
    /// <example>
    ///     <code>
    /// SpawnEffect(ctx, "explosion", new Point(10, 15));
    /// SpawnEffect(ctx, "heal", playerPos, duration: 2.0f, scale: 1.5f);
    /// SpawnEffect(ctx, "sparkle", pos, tint: Color.Gold);
    /// </code>
    /// </example>
    protected static void SpawnEffect(
        ScriptContext ctx,
        string effectId,
        Point position,
        float duration = 0.0f,
        float scale = 1.0f,
        Color? tint = null
    )
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        if (string.IsNullOrWhiteSpace(effectId))
        {
            ctx.Logger?.LogWarning(
                "Attempted to spawn effect with null or empty effectId from script"
            );
            return;
        }

        // Clamp parameters to reasonable ranges
        duration = Math.Max(0.0f, duration);
        scale = Math.Clamp(scale, 0.1f, 10.0f);

        try
        {
            // Get the effect system from ScriptContext services
            // This uses dependency injection - the system is provided by the game host
            var effectSystem = ctx.WorldApi as IEffectSystem;

            if (effectSystem != null)
                effectSystem.SpawnEffect(effectId, position, duration, scale, tint);
            else
                // Fallback: Log the effect request if no effect system is registered
                // This ensures scripts don't break even if the system isn't set up yet
                ctx.Logger?.LogDebug(
                    "[Script Effect] {EffectId} at ({X}, {Y}) (Duration: {Duration:F2}s, Scale: {Scale:F2}, Tint: {Tint})",
                    effectId,
                    position.X,
                    position.Y,
                    duration,
                    scale,
                    tint?.ToString() ?? "None"
                );
        }
        catch (Exception ex)
        {
            ctx.Logger?.LogError(
                ex,
                "Failed to spawn effect: {EffectId} at {Position}. Error: {Error}",
                effectId,
                position,
                ex.Message
            );
        }
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