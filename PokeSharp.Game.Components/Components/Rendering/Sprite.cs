using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Rendering;

/// <summary>
///     Component for rendering NPC/Player sprites from the Pokemon Emerald sprite extraction.
///     Uses sprite sheet + source rectangles for efficient rendering.
/// </summary>
public struct Sprite
{
    /// <summary>
    ///     Gets or sets the sprite name (e.g., "walking", "nurse", "boy_1").
    /// </summary>
    public string SpriteName { get; set; }

    /// <summary>
    ///     Gets or sets the sprite category (e.g., "may", "brendan", "generic", "gym_leaders").
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    ///     Gets or sets the current frame index in the sprite sheet.
    /// </summary>
    public int CurrentFrame { get; set; }

    /// <summary>
    ///     Gets or sets whether to flip the sprite horizontally (for left/right directions).
    /// </summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>
    ///     Gets or sets the source rectangle on the sprite sheet (calculated from frame data).
    /// </summary>
    public Rectangle SourceRect { get; set; }

    /// <summary>
    ///     Gets or sets the origin point for rotation and scaling (typically center).
    /// </summary>
    public Vector2 Origin { get; set; }

    /// <summary>
    ///     Gets or sets the rotation angle in radians.
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    ///     Gets or sets the tint color applied to the sprite.
    /// </summary>
    public Color Tint { get; set; }

    /// <summary>
    ///     Gets or sets the scale factor for rendering.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    ///     Initializes a new instance of the Sprite struct with default values.
    /// </summary>
    /// <param name="spriteName">The sprite name (e.g., "walking", "nurse").</param>
    /// <param name="category">The sprite category (e.g., "may", "generic").</param>
    public Sprite(string spriteName, string category)
    {
        SpriteName = spriteName;
        Category = category;
        CurrentFrame = 0;
        FlipHorizontal = false;
        SourceRect = Rectangle.Empty;
        Origin = Vector2.Zero;
        Rotation = 0f;
        Tint = Color.White;
        Scale = 1f;
    }
}
