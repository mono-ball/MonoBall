using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
/// System responsible for rendering sprites to the screen using MonoGame SpriteBatch.
/// </summary>
public class RenderSystem : BaseSystem
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assetManager;
    private readonly ILogger<RenderSystem>? _logger;
    private ulong _frameCounter = 0;
    private int _spriteCount = 0;

    /// <summary>
    /// Initializes a new instance of the RenderSystem class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public RenderSystem(GraphicsDevice graphicsDevice, AssetManager assetManager, ILogger<RenderSystem>? logger = null)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _logger = logger;
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Render;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;
            _spriteCount = 0;

            _logger?.LogDebug("═══════════════════════════════════════════════════");
            _logger?.LogDebug("RenderSystem - Frame {FrameCounter} - Starting sprite rendering", _frameCounter);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            // Get camera transform matrix (if camera exists)
            Matrix cameraTransform = Matrix.Identity;
            var cameraQuery = new QueryDescription().WithAll<Player, Camera>();
            world.Query(in cameraQuery, (ref Camera camera) =>
            {
                cameraTransform = camera.GetTransformMatrix();
                _logger?.LogDebug("Camera transform applied: Position=({X:F1}, {Y:F1}), Zoom={Zoom:F2}x",
                    camera.Position.X, camera.Position.Y, camera.Zoom);
            });

            // Begin sprite batch with camera transform
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.BackToFront,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp,
                transformMatrix: cameraTransform);

            _logger?.LogDebug("SpriteBatch started (BackToFront, AlphaBlend, PointClamp)");

            // Query and render all sprites
            var query = new QueryDescription().WithAll<Position, Sprite>();
            _logger?.LogDebug("Querying entities with Position + Sprite components...");

            world.Query(in query, (ref Position position, ref Sprite sprite) =>
            {
                _spriteCount++;
                RenderSprite(ref position, ref sprite);
            });

            // End sprite batch
            _spriteBatch.End();

            _logger?.LogDebug("SpriteBatch ended");
            _logger?.LogDebug("───────────────────────────────────────────────────");
            _logger?.LogDebug("RenderSystem - Frame {FrameCounter} - Completed. Total sprites rendered: {SpriteCount}",
                _frameCounter, _spriteCount);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            if (_spriteCount == 0)
            {
                _logger?.LogWarning("⚠️  WARNING: No sprites found to render in frame {FrameCounter}", _frameCounter);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ CRITICAL ERROR in RenderSystem.Update (Frame {FrameCounter})", _frameCounter);
            throw;
        }
    }

    private void RenderSprite(ref Position position, ref Sprite sprite)
    {
        try
        {
            _logger?.LogDebug("  → Sprite #{SpriteNum}: TextureId='{TextureId}'", _spriteCount, sprite.TextureId);

            // Get texture from AssetManager
            if (!_assetManager.HasTexture(sprite.TextureId))
            {
                _logger?.LogWarning("    ⚠️  Texture '{TextureId}' NOT FOUND in AssetManager - skipping sprite", sprite.TextureId);
                return; // Texture not loaded
            }

            var texture = _assetManager.GetTexture(sprite.TextureId);
            _logger?.LogDebug("    ✓ Texture found: {Width}x{Height}px", texture.Width, texture.Height);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
            {
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
                _logger?.LogDebug("    Source: Full texture [{Width}x{Height}]", texture.Width, texture.Height);
            }
            else
            {
                _logger?.LogDebug("    Source: Rectangle (X={X}, Y={Y}, W={W}, H={H})",
                    sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height);
            }

            // Calculate render position
            var renderPosition = new Vector2(position.PixelX, position.PixelY);
            _logger?.LogDebug("    Position: Grid=({X}, {Y}) → Pixel=({PixelX}, {PixelY})",
                position.X, position.Y, position.PixelX, position.PixelY);

            // Log rendering parameters
            _logger?.LogDebug("    Render params: Tint={Tint}, Rotation={Rotation:F2}, Scale={Scale:F2}, LayerDepth=0.5",
                sprite.Tint, sprite.Rotation, sprite.Scale);

            // Draw sprite
            _spriteBatch.Draw(
                texture: texture,
                position: renderPosition,
                sourceRectangle: sourceRect,
                color: sprite.Tint,
                rotation: sprite.Rotation,
                origin: sprite.Origin,
                scale: sprite.Scale,
                effects: SpriteEffects.None,
                layerDepth: 0.5f);

            _logger?.LogDebug("    ✓ Sprite drawn successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering sprite with TextureId '{TextureId}' at position ({X}, {Y})",
                sprite.TextureId, position.PixelX, position.PixelY);
        }
    }
}
