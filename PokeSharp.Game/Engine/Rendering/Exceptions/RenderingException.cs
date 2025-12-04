using PokeSharp.Game.Engine.Core.Exceptions;

namespace PokeSharp.Game.Engine.Rendering.Exceptions;

/// <summary>
///     Base exception for all rendering and asset loading errors.
/// </summary>
public abstract class RenderingException : PokeSharpException
{
    protected RenderingException(string errorCode, string message)
        : base(errorCode, message) { }

    protected RenderingException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException) { }

    public override string GetUserFriendlyMessage()
    {
        return "A rendering error occurred. Graphics may not display correctly.";
    }
}

/// <summary>
///     Exception thrown when an asset (texture, sprite) fails to load.
/// </summary>
public class AssetLoadException : RenderingException
{
    public AssetLoadException(string assetId, string assetType, string message)
        : base("RENDER_ASSET_LOAD_FAILED", message)
    {
        WithContext("AssetId", assetId).WithContext("AssetType", assetType);
    }

    public AssetLoadException(
        string assetId,
        string assetType,
        string message,
        Exception innerException
    )
        : base("RENDER_ASSET_LOAD_FAILED", message, innerException)
    {
        WithContext("AssetId", assetId).WithContext("AssetType", assetType);
    }

    public string AssetId =>
        Context.TryGetValue("AssetId", out object? id) ? id?.ToString() ?? "" : "";

    public string AssetType =>
        Context.TryGetValue("AssetType", out object? type) ? type?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can use fallback textures

    public override string GetUserFriendlyMessage()
    {
        return $"Failed to load {AssetType} '{AssetId}'. Using placeholder graphics.";
    }
}

/// <summary>
///     Exception thrown when texture loading fails.
/// </summary>
public class TextureLoadException : RenderingException
{
    public TextureLoadException(string textureId, string filePath, string message)
        : base("RENDER_TEXTURE_LOAD_FAILED", message)
    {
        WithContext("TextureId", textureId).WithContext("FilePath", filePath);
    }

    public TextureLoadException(
        string textureId,
        string filePath,
        string message,
        Exception innerException
    )
        : base("RENDER_TEXTURE_LOAD_FAILED", message, innerException)
    {
        WithContext("TextureId", textureId).WithContext("FilePath", filePath);
    }

    public string TextureId =>
        Context.TryGetValue("TextureId", out object? id) ? id?.ToString() ?? "" : "";

    public string FilePath =>
        Context.TryGetValue("FilePath", out object? path) ? path?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can use fallback textures

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load some textures. Graphics may appear incorrect.";
    }
}

/// <summary>
///     Exception thrown when sprite loading fails.
/// </summary>
public class SpriteLoadException : RenderingException
{
    public SpriteLoadException(string spriteId, string message)
        : base("RENDER_SPRITE_LOAD_FAILED", message)
    {
        WithContext("SpriteId", spriteId);
    }

    public SpriteLoadException(string spriteId, string message, Exception innerException)
        : base("RENDER_SPRITE_LOAD_FAILED", message, innerException)
    {
        WithContext("SpriteId", spriteId);
    }

    public string SpriteId =>
        Context.TryGetValue("SpriteId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can use default sprite

    public override string GetUserFriendlyMessage()
    {
        return "Failed to load some character sprites.";
    }
}

/// <summary>
///     Exception thrown when the LRU cache evicts textures unexpectedly.
/// </summary>
public class CacheEvictionException : RenderingException
{
    public CacheEvictionException(string textureId, long currentSize, long maxSize)
        : base("RENDER_CACHE_EVICTION", $"Texture '{textureId}' was evicted from cache")
    {
        WithContext("TextureId", textureId)
            .WithContext("CurrentCacheSize", currentSize)
            .WithContext("MaxCacheSize", maxSize)
            .WithContext("CacheUsagePercent", (double)currentSize / maxSize * 100);
    }

    public string TextureId =>
        Context.TryGetValue("TextureId", out object? id) ? id?.ToString() ?? "" : "";

    public long CurrentCacheSize =>
        Context.TryGetValue("CurrentCacheSize", out object? size) && size is long l ? l : 0L;

    public long MaxCacheSize =>
        Context.TryGetValue("MaxCacheSize", out object? max) && max is long m ? m : 0L;

    public override bool IsRecoverable => true; // Can reload texture

    public override string GetUserFriendlyMessage()
    {
        return "Texture cache full. Some textures may need to be reloaded.";
    }
}

/// <summary>
///     Exception thrown when shader compilation or GPU operations fail.
/// </summary>
public class GraphicsDeviceException : RenderingException
{
    public GraphicsDeviceException(string operation, string message)
        : base("RENDER_GRAPHICS_DEVICE_ERROR", message)
    {
        WithContext("Operation", operation);
    }

    public GraphicsDeviceException(string operation, string message, Exception innerException)
        : base("RENDER_GRAPHICS_DEVICE_ERROR", message, innerException)
    {
        WithContext("Operation", operation);
    }

    public string Operation =>
        Context.TryGetValue("Operation", out object? op) ? op?.ToString() ?? "" : "";

    public override bool IsRecoverable => false; // GPU errors are usually critical

    public override string GetUserFriendlyMessage()
    {
        return "A graphics device error occurred. Please update your graphics drivers.";
    }
}

/// <summary>
///     Exception thrown when animation data is invalid or corrupted.
/// </summary>
public class AnimationException : RenderingException
{
    public AnimationException(string animationId, string message)
        : base("RENDER_ANIMATION_ERROR", message)
    {
        WithContext("AnimationId", animationId);
    }

    public AnimationException(string animationId, string message, Exception innerException)
        : base("RENDER_ANIMATION_ERROR", message, innerException)
    {
        WithContext("AnimationId", animationId);
    }

    public string AnimationId =>
        Context.TryGetValue("AnimationId", out object? id) ? id?.ToString() ?? "" : "";

    public override bool IsRecoverable => true; // Can skip animation

    public override string GetUserFriendlyMessage()
    {
        return "Animation error detected. Some animations may not play.";
    }
}
