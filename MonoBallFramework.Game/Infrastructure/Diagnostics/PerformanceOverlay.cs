using Arch.Core;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.UI.Utilities;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Infrastructure.Diagnostics;

/// <summary>
///     Renders a debug overlay showing performance statistics.
///     Toggle with F3 key.
/// </summary>
public class PerformanceOverlay : IDisposable
{
    private readonly Texture2D _backgroundTexture;
    private readonly FontLoader _fontLoader;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly SpriteBatch _spriteBatch;
    private readonly World _world;

    private bool _disposed;
    private SpriteFontBase? _font;
    private FontSystem? _fontSystem;

    /// <summary>
    ///     Creates a new performance overlay.
    /// </summary>
    public PerformanceOverlay(
        GraphicsDevice graphicsDevice,
        PerformanceMonitor performanceMonitor,
        World world,
        FontLoader fontLoader
    )
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(performanceMonitor);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(fontLoader);

        _graphicsDevice = graphicsDevice;
        _performanceMonitor = performanceMonitor;
        _world = world;
        _fontLoader = fontLoader;

        try
        {
            _spriteBatch = new SpriteBatch(graphicsDevice);

            // Create a 1x1 white texture for backgrounds
            _backgroundTexture = new Texture2D(graphicsDevice, 1, 1);
            _backgroundTexture.SetData(new[] { Color.White });

            // Load debug font from database
            LoadFont();
        }
        catch
        {
            // Cleanup partially created resources on failure
            _spriteBatch?.Dispose();
            _backgroundTexture?.Dispose();
            _fontSystem?.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Whether the overlay is currently visible.
    /// </summary>
    public bool IsVisible { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _disposed = true;

        _spriteBatch.Dispose();
        _backgroundTexture.Dispose();
        _fontSystem?.Dispose();
        _font = null;
    }

    private void LoadFont()
    {
        // CA1031: Font loading can fail for many reasons (missing file, corrupt font, etc.);
        // silently degrading is intentional for non-critical debug overlay
#pragma warning disable CA1031
        try
        {
            // Load debug font from database using FontLoader
            // This ensures the font definition comes from the DTO system
            FontEntity fontEntity = _fontLoader.GetFontByCategory("debug");
            _fontSystem = _fontLoader.LoadFont(fontEntity);
            _font = _fontSystem.GetFont(fontEntity.DefaultSize);
        }
        catch (Exception)
        {
            // Font loading failed - overlay will gracefully degrade
            // The Draw method already checks _font == null and skips rendering
            _font = null;
            _fontSystem = null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    ///     Toggles the overlay visibility.
    /// </summary>
    public void Toggle()
    {
        IsVisible = !IsVisible;
    }

    /// <summary>
    ///     Draws the performance overlay if visible.
    /// </summary>
    /// <param name="camera">Optional camera to display information about.</param>
    /// <param name="tilesRendered">Optional number of tiles rendered in the last frame.</param>
    public void Draw(Camera? camera = null, int? tilesRendered = null)
    {
        if (!IsVisible || _font == null)
        {
            return;
        }

        PerformanceStats stats = GatherStats();
        stats.Camera = camera;
        stats.TilesRendered = tilesRendered;
        RenderOverlay(stats);
    }

    private PerformanceStats GatherStats()
    {
        var stats = new PerformanceStats
        {
            Fps = _performanceMonitor.Fps,
            FrameTimeMs = _performanceMonitor.FrameTimeMs,
            MinFrameTimeMs = _performanceMonitor.MinFrameTimeMs,
            MaxFrameTimeMs = _performanceMonitor.MaxFrameTimeMs,
            MemoryMb = _performanceMonitor.MemoryMb,
            Gen0Collections = _performanceMonitor.Gen0Collections,
            Gen1Collections = _performanceMonitor.Gen1Collections,
            Gen2Collections = _performanceMonitor.Gen2Collections,
            EntityCount = _world.Size
        };

        return stats;
    }

    private void RenderOverlay(PerformanceStats stats)
    {
        const int padding = 8;
        const int lineHeight = 18;
        int x = padding;
        int y = padding;

        // Build text lines
        var lines = new List<(string text, Color color)>
        {
            ("--- Performance ---", Color.Gray),
            ($"FPS: {stats.Fps:F1}", GetFpsColor(stats.Fps)),
            (
                $"Frame: {stats.FrameTimeMs:F2}ms (min: {stats.MinFrameTimeMs:F2}, max: {stats.MaxFrameTimeMs:F2})",
                Color.White
            ),
            ("--- Memory ---", Color.Gray),
            ($"Memory: {stats.MemoryMb:F1} MB", GetMemoryColor(stats.MemoryMb)),
            (
                $"GC: Gen0={stats.Gen0Collections}, Gen1={stats.Gen1Collections}, Gen2={stats.Gen2Collections}",
                Color.White
            ),
            ("--- Entities ---", Color.Gray),
            ($"Entities: {stats.EntityCount:N0}", Color.Cyan)
        };

        // Add camera information if available
        if (stats.Camera.HasValue)
        {
            Camera cam = stats.Camera.Value;
            lines.Add(("--- Camera ---", Color.Gray));
            lines.Add(($"Position: ({cam.Position.X:F1}, {cam.Position.Y:F1})", Color.White));
            lines.Add(($"Zoom: {cam.Zoom:F2}x", Color.White));
            lines.Add(
                ($"Viewport: {cam.Viewport.Width}x{cam.Viewport.Height}",
                    Color.White)
            );
        }

        // Add rendered tiles count if available
        if (stats.TilesRendered.HasValue)
        {
            lines.Add(("--- Rendering ---", Color.Gray));
            lines.Add(
                ($"Tiles Rendered: {stats.TilesRendered.Value:N0}",
                    Color.LightGreen)
            );
        }

        // Add helpful tip
        lines.Add(("", Color.White)); // Empty line for spacing
        lines.Add(("Press F3 to hide", Color.Gray));

        // Calculate background size
        float maxWidth = 0f;
        foreach ((string text, Color _) in lines)
        {
            Vector2 size = _font!.MeasureString(text);
            maxWidth = Math.Max(maxWidth, size.X);
        }

        int bgWidth = (int)maxWidth + (padding * 2);
        int bgHeight = (lines.Count * lineHeight) + (padding * 2);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // Draw semi-transparent background
        _spriteBatch.Draw(
            _backgroundTexture,
            new Rectangle(x - (padding / 2), y - (padding / 2), bgWidth, bgHeight),
            new Color(0, 0, 0, 200)
        );

        // Draw border
        DrawBorder(
            x - (padding / 2),
            y - (padding / 2),
            bgWidth,
            bgHeight,
            new Color(100, 100, 100)
        );

        // Draw each line
        foreach ((string text, Color color) in lines)
        {
            _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
            y += lineHeight;
        }

        _spriteBatch.End();
    }

    private void DrawBorder(int x, int y, int width, int height, Color color)
    {
        // Top
        _spriteBatch.Draw(_backgroundTexture, new Rectangle(x, y, width, 1), color);
        // Bottom
        _spriteBatch.Draw(_backgroundTexture, new Rectangle(x, y + height - 1, width, 1), color);
        // Left
        _spriteBatch.Draw(_backgroundTexture, new Rectangle(x, y, 1, height), color);
        // Right
        _spriteBatch.Draw(_backgroundTexture, new Rectangle(x + width - 1, y, 1, height), color);
    }

    private static Color GetFpsColor(float fps)
    {
        if (fps >= 58)
        {
            return Color.LimeGreen;
        }

        if (fps >= 30)
        {
            return Color.Yellow;
        }

        return Color.Red;
    }

    private static Color GetMemoryColor(double memoryMb)
    {
        if (memoryMb < 256)
        {
            return Color.LimeGreen;
        }

        return memoryMb < 512 ? Color.Yellow : Color.Orange;
    }

    private struct PerformanceStats
    {
        public float Fps;
        public float FrameTimeMs;
        public float MinFrameTimeMs;
        public float MaxFrameTimeMs;
        public double MemoryMb;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
        public int EntityCount;
        public Camera? Camera;
        public int? TilesRendered;
    }
}
