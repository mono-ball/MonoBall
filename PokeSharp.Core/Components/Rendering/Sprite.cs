using Microsoft.Xna.Framework;

namespace PokeSharp.Core.Components.Rendering;

/// <summary>
///     Component for rendering sprites with texture information and visual properties.
/// </summary>
public struct Sprite
{
    /// <summary>
    ///     Gets or sets the texture identifier for asset loading.
    /// </summary>
    public string TextureId { get; set; }

    /// <summary>
    ///     Gets or sets the source rectangle on the sprite sheet.
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
    /// <param name="textureId">The texture identifier.</param>
    public Sprite(string textureId)
    {
        TextureId = textureId;
        SourceRect = Rectangle.Empty;
        Origin = Vector2.Zero;
        Rotation = 0f;
        Tint = Color.White;
        Scale = 1f;
    }
}
