namespace PokeSharp.Core.Components.Movement;

/// <summary>
///     Represents the velocity of an entity in pixels per second.
/// </summary>
public struct Velocity
{
    /// <summary>
    ///     Gets or sets the horizontal velocity in pixels per second.
    /// </summary>
    public float VelocityX { get; set; }

    /// <summary>
    ///     Gets or sets the vertical velocity in pixels per second.
    /// </summary>
    public float VelocityY { get; set; }

    /// <summary>
    ///     Initializes a new instance of the Velocity struct.
    /// </summary>
    public Velocity(float velocityX, float velocityY)
    {
        VelocityX = velocityX;
        VelocityY = velocityY;
    }
}
