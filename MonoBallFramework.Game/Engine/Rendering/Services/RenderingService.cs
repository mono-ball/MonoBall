using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Rendering.Configuration;

namespace MonoBallFramework.Game.Engine.Rendering.Services;

/// <summary>
///     Default implementation of IRenderingService.
///     Manages shared rendering resources with proper lifecycle.
/// </summary>
public class RenderingService : IRenderingService
{
    private readonly RenderingConfiguration _config;
    private readonly ILogger<RenderingService> _logger;
    private bool _disposed;

    public RenderingService(
        GraphicsDevice graphicsDevice,
        ILogger<RenderingService> logger,
        RenderingConfiguration? config = null)
    {
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? RenderingConfiguration.Default;

        SpriteBatch = new SpriteBatch(graphicsDevice);

        _logger.LogInformation(
            "RenderingService initialized with shared SpriteBatch | MaxBatchSize: {MaxBatchSize}, VSync: {VSync}",
            _config.MaxSpriteBatchSize,
            _config.EnableVSync);
    }

    public SpriteBatch SpriteBatch { get; }
    public GraphicsDevice GraphicsDevice { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SpriteBatch?.Dispose();
        _disposed = true;
        _logger.LogDebug("RenderingService disposed");

        GC.SuppressFinalize(this);
    }
}
