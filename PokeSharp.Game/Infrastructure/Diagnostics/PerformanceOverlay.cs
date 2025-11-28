using Arch.Core;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Systems.Pooling;

namespace PokeSharp.Game.Infrastructure.Diagnostics;

/// <summary>
///     Renders a debug overlay showing performance statistics.
///     Toggle with F3 key.
/// </summary>
public class PerformanceOverlay : IDisposable
{
    private readonly Texture2D _backgroundTexture;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly EntityPoolManager? _poolManager;
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
        EntityPoolManager? poolManager = null
    )
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _performanceMonitor = performanceMonitor;
        _world = world;
        _poolManager = poolManager;

        // Create a 1x1 white texture for backgrounds
        _backgroundTexture = new Texture2D(graphicsDevice, 1, 1);
        _backgroundTexture.SetData(new[] { Color.White });

        // Try to load system font
        LoadFont();
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

        _disposed = true;

        _spriteBatch.Dispose();
        _backgroundTexture.Dispose();
        _fontSystem?.Dispose();
    }

    private void LoadFont()
    {
        _fontSystem = new FontSystem();

        // Try to load common monospace fonts
        string[] fontPaths =
        [
            // macOS
            "/System/Library/Fonts/Monaco.ttf",
            "/System/Library/Fonts/Menlo.ttc",
            "/System/Library/Fonts/Courier New.ttf",
            "/System/Library/Fonts/Supplemental/Courier New.ttf",
            // Windows
            "C:\\Windows\\Fonts\\consola.ttf",
            "C:\\Windows\\Fonts\\cour.ttf",
            "C:\\Windows\\Fonts\\arial.ttf",
            // Linux
            "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
            "/usr/share/fonts/TTF/DejaVuSansMono.ttf",
        ];

        foreach (string path in fontPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    _fontSystem.AddFont(File.ReadAllBytes(path));
                    _font = _fontSystem.GetFont(14);
                    return;
                }
                catch
                {
                    // Try next font
                }
            }
        }
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
    public void Draw()
    {
        if (!IsVisible || _font == null)
        {
            return;
        }

        PerformanceStats stats = GatherStats();
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
            EntityCount = _world.Size,
        };

        // Get pool stats if available
        if (_poolManager != null)
        {
            AggregatePoolStatistics poolStats = _poolManager.GetStatistics();
            stats.PooledEntities = poolStats.TotalActive;
            stats.AvailablePooled = poolStats.TotalAvailable;
        }

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
            ($"FPS: {stats.Fps:F1}", GetFpsColor(stats.Fps)),
            (
                $"Frame: {stats.FrameTimeMs:F2}ms (min: {stats.MinFrameTimeMs:F2}, max: {stats.MaxFrameTimeMs:F2})",
                Color.White
            ),
            ($"Memory: {stats.MemoryMb:F1} MB", GetMemoryColor(stats.MemoryMb)),
            (
                $"GC: Gen0={stats.Gen0Collections}, Gen1={stats.Gen1Collections}, Gen2={stats.Gen2Collections}",
                Color.White
            ),
            ($"Entities: {stats.EntityCount:N0}", Color.Cyan),
        };

        if (_poolManager != null)
        {
            lines.Add(
                (
                    $"Pooled: {stats.PooledEntities:N0} active, {stats.AvailablePooled:N0} available",
                    Color.Yellow
                )
            );
        }

        // Add helpful tip
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

        if (memoryMb < 512)
        {
            return Color.Yellow;
        }

        return Color.Orange;
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
        public int PooledEntities;
        public int AvailablePooled;
    }
}
