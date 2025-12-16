using FontStashSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.UI.Utilities;

/// <summary>
/// Provides font loading and path resolution using the content provider system.
/// Supports mod-overridable fonts via database-driven font definitions.
/// </summary>
public sealed class FontLoader
{
    private readonly IContentProvider _contentProvider;
    private readonly IDbContextFactory<GameDataContext> _contextFactory;
    private readonly ILogger<FontLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontLoader"/> class.
    /// </summary>
    public FontLoader(
        IContentProvider contentProvider,
        IDbContextFactory<GameDataContext> contextFactory,
        ILogger<FontLoader> logger)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves the full path to a font file.
    /// </summary>
    /// <param name="fontFileName">The font filename (e.g., "pokemon.ttf").</param>
    /// <returns>The resolved path, or null if not found.</returns>
    public string? ResolveFontPath(string fontFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFileName);

        string? path = _contentProvider.ResolveContentPath("Fonts", fontFileName);

        if (path == null)
        {
            _logger.LogWarning("Font not found: {Font}", fontFileName);
        }
        else
        {
            _logger.LogDebug("Resolved font {Font} to {Path}", fontFileName, path);
        }

        return path;
    }

    /// <summary>
    /// Gets the font entity for a specific category from the database.
    /// </summary>
    /// <param name="category">The font category (e.g., "game", "debug").</param>
    /// <returns>The font entity if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no font found for the category.</exception>
    public FontEntity GetFontByCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        using GameDataContext context = _contextFactory.CreateDbContext();
        FontEntity? font = context.Fonts
            .AsNoTracking()
            .FirstOrDefault(f => string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase));

        if (font == null)
        {
            throw new InvalidOperationException(
                $"No font definition found for category '{category}'. " +
                $"Ensure a font JSON definition exists in Assets/Definitions/Fonts/ with category: \"{category}\"");
        }

        return font;
    }

    /// <summary>
    /// Gets the font entity by its unique ID from the database.
    /// </summary>
    /// <param name="fontId">The font ID (e.g., "base:font:game/pokemon").</param>
    /// <returns>The font entity if found.</returns>
    /// <exception cref="InvalidOperationException">Thrown if font not found.</exception>
    public FontEntity GetFontById(GameFontId fontId)
    {
        using GameDataContext context = _contextFactory.CreateDbContext();
        FontEntity? font = context.Fonts
            .AsNoTracking()
            .FirstOrDefault(f => f.FontId == fontId);

        if (font == null)
        {
            throw new InvalidOperationException(
                $"No font definition found for ID '{fontId}'. " +
                $"Ensure a font JSON definition exists in Assets/Definitions/Fonts/");
        }

        return font;
    }

    /// <summary>
    /// Tries to get the font entity for a specific category from the database.
    /// </summary>
    /// <param name="category">The font category (e.g., "game", "debug").</param>
    /// <returns>The font entity if found, null otherwise.</returns>
    public FontEntity? TryGetFontByCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Fonts
            .AsNoTracking()
            .FirstOrDefault(f => string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tries to get the font entity by its unique ID from the database.
    /// </summary>
    /// <param name="fontId">The font ID (e.g., "base:font:game/pokemon").</param>
    /// <returns>The font entity if found, null otherwise.</returns>
    public FontEntity? TryGetFontById(GameFontId fontId)
    {
        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Fonts
            .AsNoTracking()
            .FirstOrDefault(f => f.FontId == fontId);
    }

    /// <summary>
    /// Gets all registered fonts from the database.
    /// </summary>
    /// <returns>Collection of all font entities.</returns>
    public IReadOnlyList<FontEntity> GetAllFonts()
    {
        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Fonts
            .AsNoTracking()
            .ToList();
    }

    /// <summary>
    /// Gets the path to the main game font.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no game font defined.</exception>
    /// <exception cref="FileNotFoundException">Thrown if font file not found.</exception>
    public string GetGameFontPath()
    {
        FontEntity fontEntity = GetFontByCategory("game");
        return ResolveFontPath(fontEntity.FontPath)
            ?? throw new FileNotFoundException($"Game font file not found: {fontEntity.FontPath}");
    }

    /// <summary>
    /// Gets the path to the debug font.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no debug font defined.</exception>
    /// <exception cref="FileNotFoundException">Thrown if font file not found.</exception>
    public string GetDebugFontPath()
    {
        FontEntity fontEntity = GetFontByCategory("debug");
        return ResolveFontPath(fontEntity.FontPath)
            ?? throw new FileNotFoundException($"Debug font file not found: {fontEntity.FontPath}");
    }

    /// <summary>
    /// Checks if a font exists.
    /// </summary>
    public bool FontExists(string fontFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFileName);

        return _contentProvider.ContentExists("Fonts", fontFileName);
    }

    /// <summary>
    /// Loads a font and returns a FontSystem ready for use.
    /// </summary>
    /// <param name="fontFileName">The font filename to load.</param>
    /// <returns>A FontSystem loaded with the specified font.</returns>
    /// <exception cref="FileNotFoundException">Thrown if font not found.</exception>
    public FontSystem LoadFont(string fontFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFileName);

        string? fontPath = ResolveFontPath(fontFileName);

        if (fontPath == null)
        {
            throw new FileNotFoundException($"Font not found: {fontFileName}");
        }

        var fontSystem = new FontSystem();
        fontSystem.AddFont(File.ReadAllBytes(fontPath));

        _logger.LogDebug("Loaded font {Font} from {Path}", fontFileName, fontPath);
        return fontSystem;
    }

    /// <summary>
    /// Loads a font by its database entity.
    /// </summary>
    /// <param name="fontEntity">The font entity from the database.</param>
    /// <returns>A FontSystem loaded with the specified font.</returns>
    /// <exception cref="FileNotFoundException">Thrown if font file not found.</exception>
    public FontSystem LoadFont(FontEntity fontEntity)
    {
        if (fontEntity == null)
            throw new ArgumentNullException(nameof(fontEntity));

        string? fontPath = ResolveFontPath(fontEntity.FontPath);

        if (fontPath == null)
        {
            throw new FileNotFoundException($"Font file not found: {fontEntity.FontPath}");
        }

        var fontSystem = new FontSystem();
        fontSystem.AddFont(File.ReadAllBytes(fontPath));

        _logger.LogDebug(
            "Loaded font {FontId} ({DisplayName}) from {Path}",
            fontEntity.FontId,
            fontEntity.DisplayName,
            fontPath);

        return fontSystem;
    }

    /// <summary>
    /// Loads the debug font from the database.
    /// </summary>
    /// <returns>A FontSystem loaded with the debug font.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no debug font defined.</exception>
    public FontSystem LoadDebugFont()
    {
        FontEntity fontEntity = GetFontByCategory("debug");
        return LoadFont(fontEntity);
    }

    /// <summary>
    /// Loads the game font from the database.
    /// </summary>
    /// <returns>A FontSystem loaded with the game font.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no game font defined.</exception>
    public FontSystem LoadGameFont()
    {
        FontEntity fontEntity = GetFontByCategory("game");
        return LoadFont(fontEntity);
    }
}
