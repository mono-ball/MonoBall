using Arch.Core;

namespace PokeSharp.Rendering.Animation;

/// <summary>
///     Represents an event that can be triggered at a specific frame in an animation.
///     Useful for synchronizing sound effects, particle effects, or gameplay logic with animations.
/// </summary>
public class AnimationEvent
{
    /// <summary>
    ///     Initializes a new instance of the AnimationEvent class.
    /// </summary>
    public AnimationEvent()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the AnimationEvent class with a name and callback.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="callback">The callback to execute when triggered.</param>
    public AnimationEvent(string eventName, Action<Entity> callback)
    {
        EventName = eventName;
        Callback = callback;
    }

    /// <summary>
    ///     Initializes a new instance of the AnimationEvent class with a name, callback, and data.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="callback">The callback to execute when triggered.</param>
    /// <param name="data">Optional data to pass to the callback.</param>
    public AnimationEvent(string eventName, Action<Entity> callback, object? data)
    {
        EventName = eventName;
        Callback = callback;
        Data = data;
    }

    /// <summary>
    ///     Gets or sets the name/identifier of this event.
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the action to execute when this event is triggered.
    ///     The Entity parameter is the entity that owns the animation.
    /// </summary>
    public Action<Entity>? Callback { get; set; }

    /// <summary>
    ///     Gets or sets optional data associated with this event.
    ///     Can be used to pass parameters to the callback.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    ///     Triggers this event for the specified entity.
    /// </summary>
    /// <param name="entity">The entity that owns the animation.</param>
    public void Trigger(Entity entity)
    {
        Callback?.Invoke(entity);
    }

    /// <summary>
    ///     Returns a string representation of this animation event.
    /// </summary>
    public override string ToString()
    {
        return $"AnimationEvent('{EventName}', HasCallback={Callback != null}, HasData={Data != null})";
    }
}