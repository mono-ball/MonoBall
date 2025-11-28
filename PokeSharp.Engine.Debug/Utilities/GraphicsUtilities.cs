using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeSharp.Engine.Debug.Utilities;

/// <summary>
///     Utility class for common graphics operations.
///     Eliminates code duplication across console rendering classes.
/// </summary>
public static class GraphicsUtilities
{
    /// <summary>
    ///     Creates a 1x1 white pixel texture for rectangle drawing.
    ///     This texture can be reused for drawing solid-color rectangles via SpriteBatch.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device to create the texture on.</param>
    /// <returns>A 1x1 white texture that can be scaled and colored.</returns>
    public static Texture2D CreatePixelTexture(GraphicsDevice graphicsDevice)
    {
        var pixel = new Texture2D(graphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        return pixel;
    }

    /// <summary>
    ///     Draws a filled rectangle using a 1x1 pixel texture.
    ///     This is more efficient than creating individual textures for each rectangle.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="pixel">The 1x1 pixel texture.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="width">Width of the rectangle.</param>
    /// <param name="height">Height of the rectangle.</param>
    /// <param name="color">Color of the rectangle.</param>
    public static void DrawRectangle(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        int x,
        int y,
        int width,
        int height,
        Color color
    )
    {
        spriteBatch.Draw(pixel, new Rectangle(x, y, width, height), color);
    }

    /// <summary>
    ///     Draws a filled rectangle using a 1x1 pixel texture with Vector2 position.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="pixel">The 1x1 pixel texture.</param>
    /// <param name="position">Position of the rectangle.</param>
    /// <param name="size">Size of the rectangle.</param>
    /// <param name="color">Color of the rectangle.</param>
    public static void DrawRectangle(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 position,
        Vector2 size,
        Color color
    )
    {
        spriteBatch.Draw(
            pixel,
            position,
            null,
            color,
            0f,
            Vector2.Zero,
            size,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>
    ///     Draws a filled rectangle using a Rectangle struct.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch to draw with.</param>
    /// <param name="pixel">The 1x1 pixel texture.</param>
    /// <param name="rectangle">The rectangle to draw.</param>
    /// <param name="color">Color of the rectangle.</param>
    public static void DrawRectangle(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle rectangle,
        Color color
    )
    {
        spriteBatch.Draw(pixel, rectangle, color);
    }
}
