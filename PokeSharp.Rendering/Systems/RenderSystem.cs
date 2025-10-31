using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;

namespace PokeSharp.Rendering.Systems;

/// <summary>
/// System responsible for rendering sprites to the screen using MonoGame SpriteBatch.
/// </summary>
public class RenderSystem : BaseSystem
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly Dictionary<string, Texture2D> _textureCache;
    private Texture2D? _pixelTexture;

    /// <summary>
    /// Initializes a new instance of the RenderSystem class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    public RenderSystem(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _textureCache = new Dictionary<string, Texture2D>();
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Render;

    /// <inheritdoc/>
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Create a 1x1 white pixel texture for debugging/placeholder
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        _textureCache["pixel"] = _pixelTexture;
    }

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Clear screen
        _graphicsDevice.Clear(Color.CornflowerBlue);

        // Begin sprite batch
        _spriteBatch.Begin(
            sortMode: SpriteSortMode.BackToFront,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp);

        // Query and render all sprites
        var query = new QueryDescription().WithAll<Position, Sprite>();

        world.Query(in query, (ref Position position, ref Sprite sprite) =>
        {
            RenderSprite(ref position, ref sprite);
        });

        // End sprite batch
        _spriteBatch.End();
    }

    private void RenderSprite(ref Position position, ref Sprite sprite)
    {
        // Get texture from cache or use pixel texture as fallback
        if (!_textureCache.TryGetValue(sprite.TextureId, out var texture))
        {
            texture = _pixelTexture;
        }

        if (texture == null)
        {
            return;
        }

        // Determine source rectangle
        var sourceRect = sprite.SourceRect;
        if (sourceRect.IsEmpty)
        {
            sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        // Calculate render position
        var renderPosition = new Vector2(position.PixelX, position.PixelY);

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
    }

    /// <summary>
    /// Loads a texture and adds it to the texture cache.
    /// </summary>
    /// <param name="textureId">The texture identifier.</param>
    /// <param name="texture">The texture to cache.</param>
    public void LoadTexture(string textureId, Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(textureId);
        ArgumentNullException.ThrowIfNull(texture);

        _textureCache[textureId] = texture;
    }
}
