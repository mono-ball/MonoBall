using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.UI.Debug.Components.Layout;

/// <summary>
///     A simple panel component with background and optional border.
/// </summary>
public class Panel : UIContainer
{
    /// <summary>Background color (null for transparent)</summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>Border color (null for no border)</summary>
    public Color? BorderColor { get; set; }

    /// <summary>Border thickness</summary>
    public int BorderThickness { get; set; } = 1;

    protected override void OnRenderContainer(UIContext context)
    {
        // Draw background
        if (BackgroundColor.HasValue)
        {
            context.Renderer.DrawRectangle(Rect, BackgroundColor.Value);
        }

        // Draw border
        if (BorderColor.HasValue && BorderThickness > 0)
        {
            context.Renderer.DrawRectangleOutline(Rect, BorderColor.Value, BorderThickness);
        }
    }
}
