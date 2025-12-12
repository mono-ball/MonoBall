using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Ecs.Components.Audio;

/// <summary>
///     ECS component for defining music zones on maps.
///     Automatically changes background music when player enters/exits the zone.
/// </summary>
public struct MusicZoneComponent
{
    /// <summary>
    ///     Gets or sets the name/path of the music track for this zone.
    /// </summary>
    public string MusicName { get; set; }

    /// <summary>
    ///     Gets or sets the zone bounds in world space (tile coordinates).
    /// </summary>
    public Rectangle ZoneBounds { get; set; }

    /// <summary>
    ///     Gets or sets the priority of this music zone.
    ///     Higher priority zones override lower priority zones when overlapping.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Gets or sets whether the music should loop.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    ///     Gets or sets the crossfade duration when entering/exiting the zone (in seconds).
    /// </summary>
    public float CrossfadeDuration { get; set; }

    /// <summary>
    ///     Gets or sets whether this zone is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Gets or sets whether the player is currently inside this zone.
    /// </summary>
    public bool PlayerInZone { get; set; }

    /// <summary>
    ///     Initializes a new music zone component.
    /// </summary>
    /// <param name="musicName">The name/path of the music track.</param>
    /// <param name="zoneBounds">The zone bounds in tile coordinates.</param>
    /// <param name="priority">The zone priority (default 0).</param>
    public MusicZoneComponent(string musicName, Rectangle zoneBounds, int priority = 0)
    {
        MusicName = musicName;
        ZoneBounds = zoneBounds;
        Priority = priority;
        Loop = true;
        CrossfadeDuration = 1.0f;
        IsActive = true;
        PlayerInZone = false;
    }

    /// <summary>
    ///     Checks if a position is within this zone.
    /// </summary>
    /// <param name="position">The position to check (in tile coordinates).</param>
    /// <returns>True if the position is within the zone bounds.</returns>
    public readonly bool Contains(Vector2 position)
    {
        return ZoneBounds.Contains(position);
    }
}
