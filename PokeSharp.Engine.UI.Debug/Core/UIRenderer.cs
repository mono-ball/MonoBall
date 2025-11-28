using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Low-level rendering primitives for the debug UI system.
///     Handles drawing rectangles, text, and clipping.
/// </summary>
public class UIRenderer : IDisposable
{
    // Font size limits
    public const int MinFontSize = 10;
    public const int MaxFontSize = 32;
    public const int DefaultFontSize = 16;

    // Static reusable RasterizerState for scissor testing
    private static readonly RasterizerState _scissorRasterizerState = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
    };

    // Clipping stack
    private readonly Stack<Rectangle> _clipStack = new();
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Texture2D _pixel;
    private readonly SpriteBatch _spriteBatch;
    private Rectangle? _currentClipRect;
    private bool _disposed;
    private SpriteFontBase? _font;
    private FontSystem? _fontSystem;
    private bool _isInBatch; // Track if SpriteBatch.Begin() has been called

    public UIRenderer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spriteBatch = new SpriteBatch(graphicsDevice);

        // Create 1x1 white pixel for rectangle drawing
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>
    ///     Gets the current font size.
    /// </summary>
    public int FontSize { get; private set; } = 16;

    /// <summary>
    ///     Gets the current font (throws if not set).
    /// </summary>
    public SpriteFontBase Font
    {
        get
        {
            if (_font == null)
            {
                throw new InvalidOperationException("Font not set. Call SetFontSystem first.");
            }

            return _font;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _spriteBatch?.Dispose();
        _pixel?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Sets the font system to use for text rendering.
    /// </summary>
    public void SetFontSystem(FontSystem fontSystem, int fontSize = 16)
    {
        _fontSystem = fontSystem ?? throw new ArgumentNullException(nameof(fontSystem));
        FontSize = Math.Clamp(fontSize, MinFontSize, MaxFontSize);
        _font = fontSystem.GetFont(FontSize);
        if (_font == null)
        {
            throw new InvalidOperationException($"GetFont returned NULL for fontSize={FontSize}");
        }
    }

    /// <summary>
    ///     Sets the font size. Requires font system to be initialized.
    /// </summary>
    public void SetFontSize(int fontSize)
    {
        if (_fontSystem == null)
        {
            throw new InvalidOperationException(
                "Font system not initialized. Call SetFontSystem first."
            );
        }

        FontSize = Math.Clamp(fontSize, MinFontSize, MaxFontSize);
        _font = _fontSystem.GetFont(FontSize);
    }

    /// <summary>
    ///     Increases font size by the specified amount.
    /// </summary>
    public void IncreaseFontSize(int amount = 2)
    {
        SetFontSize(FontSize + amount);
    }

    /// <summary>
    ///     Decreases font size by the specified amount.
    /// </summary>
    public void DecreaseFontSize(int amount = 2)
    {
        SetFontSize(FontSize - amount);
    }

    /// <summary>
    ///     Resets font size to default.
    /// </summary>
    public void ResetFontSize()
    {
        SetFontSize(DefaultFontSize);
    }

    /// <summary>
    ///     Begins a new rendering batch.
    /// </summary>
    public void Begin()
    {
        // Safety check: If already in a batch, end it first
        if (_isInBatch)
        {
            try
            {
                _spriteBatch.End();
            }
            catch (Exception ex)
            {
                // Recovery from unbalanced Begin/End - log in DEBUG for diagnostics
                System.Diagnostics.Debug.WriteLine(
                    $"[UIRenderer] SpriteBatch recovery: {ex.Message}"
                );
            }

            _isInBatch = false;
        }

        // Get the appropriate rasterizer state FIRST
        RasterizerState rasterState = GetRasterizerState();

        // Set scissor rectangle if clipping is enabled
        if (_currentClipRect.HasValue)
        {
            _graphicsDevice.ScissorRectangle = _currentClipRect.Value;
        }

        // EXPLICITLY set the RasterizerState on the device BEFORE SpriteBatch.Begin
        // This ensures MonoGame uses our scissor settings
        _graphicsDevice.RasterizerState = rasterState;

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            rasterizerState: rasterState
        );
        _isInBatch = true;
    }

    /// <summary>
    ///     Ends the current rendering batch.
    /// </summary>
    public void End()
    {
        if (!_isInBatch)
        {
            return;
        }

        _spriteBatch.End();
        _isInBatch = false;
        // Note: _currentClipRect is NOT cleared here. It should only be modified by PushClip/PopClip.
    }

    /// <summary>
    ///     Pushes a clipping rectangle onto the stack.
    ///     All subsequent draw calls will be clipped to this rectangle.
    /// </summary>
    public void PushClip(LayoutRect rect)
    {
        var clipRect = rect.ToRectangle();

        // Intersect with current clip if one exists
        if (_currentClipRect.HasValue)
        {
            clipRect = Rectangle.Intersect(_currentClipRect.Value, clipRect);
        }

        // Clamp to viewport bounds to ensure valid scissor rectangle
        Viewport viewport = _graphicsDevice.Viewport;
        var viewportRect = new Rectangle(0, 0, viewport.Width, viewport.Height);
        clipRect = Rectangle.Intersect(clipRect, viewportRect);

        _clipStack.Push(clipRect);
        _currentClipRect = clipRect;

        // Set scissor rectangle on graphics device
        _graphicsDevice.ScissorRectangle = clipRect;

        // Apply clipping by restarting batch (maintain _isInBatch state)
        if (_isInBatch)
        {
            _spriteBatch.End();
            RasterizerState rasterState = GetRasterizerState();

            // EXPLICITLY set the RasterizerState on the device BEFORE SpriteBatch.Begin
            _graphicsDevice.RasterizerState = rasterState;

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                rasterizerState: rasterState
            );
            // _isInBatch remains true
        }
    }

    /// <summary>
    ///     Pops the top clipping rectangle from the stack.
    ///     Gracefully handles stack imbalance to prevent game crashes.
    /// </summary>
    public void PopClip()
    {
        if (_clipStack.Count == 0)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                "[UIRenderer] PopClip called with empty stack - ignoring"
            );
#endif
            return;
        }

        _clipStack.Pop();
        _currentClipRect = _clipStack.Count > 0 ? _clipStack.Peek() : null;

        // Set scissor rectangle on graphics device
        if (_currentClipRect.HasValue)
        {
            _graphicsDevice.ScissorRectangle = _currentClipRect.Value;
        }

        // Apply new clipping by restarting batch (maintain _isInBatch state)
        if (_isInBatch)
        {
            _spriteBatch.End();
            RasterizerState rasterState = GetRasterizerState();

            // EXPLICITLY set the RasterizerState on the device BEFORE SpriteBatch.Begin
            _graphicsDevice.RasterizerState = rasterState;

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                rasterizerState: rasterState
            );
            // _isInBatch remains true
        }
    }

    /// <summary>
    ///     Draws a filled rectangle.
    /// </summary>
    public void DrawRectangle(LayoutRect rect, Color color)
    {
        _spriteBatch.Draw(_pixel, rect.ToRectangle(), color);
    }

    /// <summary>
    ///     Draws a rectangle outline.
    /// </summary>
    public void DrawRectangleOutline(LayoutRect rect, Color color, int thickness = 1)
    {
        var r = rect.ToRectangle();

        // Top
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        // Bottom
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness),
            color
        );
        // Left
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        // Right
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(r.Right - thickness, r.Y, thickness, r.Height),
            color
        );
    }

    /// <summary>
    ///     Draws text at the specified position.
    /// </summary>
    public void DrawText(string text, Vector2 position, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_font == null)
        {
            // Font not set - silently skip (would spam logs during startup)
            return;
        }

        _spriteBatch.DrawString(_font, text, position, color);
    }

    /// <summary>
    ///     Draws text at the specified position.
    /// </summary>
    public void DrawText(string text, float x, float y, Color color)
    {
        DrawText(text, new Vector2(x, y), color);
    }

    /// <summary>
    ///     Measures the size of a text string.
    /// </summary>
    public Vector2 MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text) || _font == null)
        {
            return Vector2.Zero;
        }

        Vector2 size = _font.MeasureString(text);

        return size;
    }

    /// <summary>
    ///     Gets the line height of the current font.
    /// </summary>
    public int GetLineHeight()
    {
        return _font?.LineHeight ?? 20;
    }

    private RasterizerState GetRasterizerState()
    {
        if (!_currentClipRect.HasValue)
        {
            return RasterizerState.CullNone;
        }

        // Use static shared RasterizerState with scissor test enabled
        return _scissorRasterizerState;
    }
}
