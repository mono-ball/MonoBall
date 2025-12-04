using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Rendering;

/// <summary>
///     Component for image layers from Tiled maps.
///     Image layers are full images rendered at specific positions in the layer order.
/// </summary>
public struct ImageLayer
{
    /// <summary>
    ///     Gets or sets the texture identifier for asset loading.
    /// </summary>
    public string TextureId { get; set; }

    /// <summary>
    ///     Gets or sets the X position in pixels.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Gets or sets the Y position in pixels.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    ///     Gets or sets the layer opacity (0.0 to 1.0).
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    ///     Gets or sets the Z-order depth for rendering.
    ///     Lower values render in front, higher values render behind.
    /// </summary>
    public float LayerDepth { get; set; }

    /// <summary>
    ///     Gets or sets the layer index from Tiled (for sorting).
    /// </summary>
    public int LayerIndex { get; set; }

    /// <summary>
    ///     Initializes a new instance of the ImageLayer struct.
    /// </summary>
    /// <param name="textureId">The texture identifier.</param>
    /// <param name="x">X position in pixels.</param>
    /// <param name="y">Y position in pixels.</param>
    /// <param name="opacity">Layer opacity (0.0 to 1.0).</param>
    /// <param name="layerDepth">Z-order depth for rendering.</param>
    /// <param name="layerIndex">Layer index from Tiled.</param>
    public ImageLayer(
        string textureId,
        float x,
        float y,
        float opacity,
        float layerDepth,
        int layerIndex
    )
    {
        TextureId = textureId;
        X = x;
        Y = y;
        Opacity = opacity;
        LayerDepth = layerDepth;
        LayerIndex = layerIndex;
    }

    /// <summary>
    ///     Gets the render position as a Vector2.
    /// </summary>
    public readonly Vector2 Position => new(X, Y);

    /// <summary>
    ///     Gets the tint color with opacity applied.
    /// </summary>
    public readonly Color TintColor => Color.White * Opacity;
}
