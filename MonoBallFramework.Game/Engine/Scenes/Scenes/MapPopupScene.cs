using FontStashSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Rendering.Services;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Scenes.Scenes;

/// <summary>
///     Scene for displaying map region/location popups during map transitions.
///     Popup slides DOWN from top to top-left corner position,
///     displays with location text, then slides UP back to top.
///     Based on Pokémon Emerald's "MAPSEC" display.
/// </summary>
public class MapPopupScene : SceneBase
{
    // Animation timing (in seconds)
    private const float SlideInDuration = 0.4f;
    private const float DisplayDuration = 2.5f;
    private const float SlideOutDuration = 0.4f;

    // GBA-accurate constants (pokeemerald dimensions at 1x scale)
    private const int ScreenPadding = 0; // No padding from screen edges (pokeemerald accurate)
    private const int GbaBackgroundWidth = 80; // Background width at 1x GBA scale
    private const int GbaBackgroundHeight = 24; // Background height at 1x GBA scale
    private const int GbaBaseFontSize = 12; // Font size at 1x scale
    private const int GbaTextOffsetY = 3; // Text Y offset from window top (1x scale)
    private const int GbaTextPadding = 4; // Text padding from edges (1x scale)
    private const int GbaShadowOffsetX = 1; // Shadow offset X (pokeemerald: 1 pixel right at 1x scale)
    private const int GbaShadowOffsetY = 1; // Shadow offset Y (pokeemerald: 1 pixel down at 1x scale)
    private const int GbaInteriorTilesX = 10; // Interior tiles width (80 / 8)
    private const int GbaInteriorTilesY = 3; // Interior tiles height (24 / 8)
    private readonly IAssetProvider _assetProvider;
    private readonly PopupBackgroundEntity _backgroundDef;
    private readonly ICameraProvider _cameraProvider;
    private readonly IContentProvider _contentProvider;
    private readonly string _mapName;
    private readonly PopupOutlineEntity _outlineDef;
    private readonly IRenderingService _renderingService;
    private readonly SceneManager _sceneManager;
    private PopupAnimationState _animationState = PopupAnimationState.SlideIn;
    private Texture2D? _backgroundTexture;
    private int _cachedBgHeight;
    private int _cachedBgWidth;
    private int _cachedBorderThickness;
    private string? _cachedDisplayText;
    private int _cachedMaxTextWidth;
    private int _cachedShadowOffset;
    private int _cachedTextOffsetY;
    private int _cachedTextPadding;

    // Current viewport scale factor (e.g., 3 for 720x480 window)
    // Setting this triggers cache recalculation via OnScaleChanged()
    private int _currentScale = 1;
    private float _currentY;

    // Animation state
    private float _elapsedTime;

    // Position (slides DOWN from top, positioned in top-left corner)
    private int _fixedX; // Fixed X position in top-left area
    private DynamicSpriteFont? _font;
    private FontSystem? _fontSystem;
    private Texture2D? _outlineTexture;
    private int _popupHeight;

    // Popup dimensions (at 1x GBA scale)
    private int _popupWidth;

    // Scale-dependent cached values (recalculated when scale changes)
    private DynamicSpriteFont? _scaledFont;

    private SpriteBatch _spriteBatch = null!; // Set from IRenderingService - shared, not disposed here
    private float _targetY;

    /// <summary>
    ///     Initializes a new instance of the MapPopupScene class.
    /// </summary>
    public MapPopupScene(
        GraphicsDevice graphicsDevice,
        ILogger<MapPopupScene> logger,
        IAssetProvider assetProvider,
        PopupBackgroundEntity backgroundDefinition,
        PopupOutlineEntity outlineDefinition,
        string mapName,
        SceneManager sceneManager,
        ICameraProvider cameraProvider,
        IRenderingService renderingService,
        IContentProvider contentProvider
    )
        : base(graphicsDevice, logger)
    {
        ArgumentNullException.ThrowIfNull(assetProvider);
        ArgumentNullException.ThrowIfNull(backgroundDefinition);
        ArgumentNullException.ThrowIfNull(outlineDefinition);
        ArgumentException.ThrowIfNullOrEmpty(mapName);
        ArgumentNullException.ThrowIfNull(sceneManager);
        ArgumentNullException.ThrowIfNull(cameraProvider);
        ArgumentNullException.ThrowIfNull(renderingService);
        ArgumentNullException.ThrowIfNull(contentProvider);

        _assetProvider = assetProvider;
        _backgroundDef = backgroundDefinition;
        _contentProvider = contentProvider;
        _outlineDef = outlineDefinition;
        _mapName = mapName;
        _sceneManager = sceneManager;
        _cameraProvider = cameraProvider;
        _renderingService = renderingService;

        // Configure scene to render and update with scenes below
        RenderScenesBelow = true;
        UpdateScenesBelow = true;
        ExclusiveInput = false; // Don't block input to game below

        logger.LogDebug(
            "MapPopupScene created - Map: '{MapName}', Background: {BgId}, Outline: {OutlineId}",
            mapName,
            backgroundDefinition.BackgroundId,
            outlineDefinition.OutlineId
        );
    }

    private int CurrentScale
    {
        get => _currentScale;
        set
        {
            if (_currentScale != value)
            {
                _currentScale = value;
                OnScaleChanged();
            }
        }
    }

    /// <inheritdoc />
    public override void LoadContent()
    {
        base.LoadContent();

        Logger.LogDebug("LoadContent called for map: '{MapName}'", _mapName);

        // Use shared SpriteBatch from RenderingService (eliminates GPU resource churn)
        _spriteBatch = _renderingService.SpriteBatch;

        // Load textures
        LoadPopupTextures();

        // Load font
        LoadFont();

        // Calculate popup dimensions
        CalculatePopupDimensions();

        // Initialize scale-dependent caches (font, text, dimensions)
        // This assumes scale = 1, will recalculate in first Draw() when real scale is known
        _currentScale = 1;
        OnScaleChanged();

        // Initialize animation (slide DOWN from top, top-left corner)
        Viewport viewport = GraphicsDevice.Viewport;
        _fixedX = ScreenPadding; // Top-left corner positioning
        _currentY = -_popupHeight; // Start off-screen above
        _targetY = ScreenPadding; // Target position in top-left corner

        Logger.LogDebug(
            "LoadContent complete - Size: {Width}x{Height}, Position: X={FixedX}, Y={StartY} -> {TargetY}",
            _popupWidth,
            _popupHeight,
            _fixedX,
            _currentY,
            _targetY
        );
    }

    /// <summary>
    ///     Loads popup textures (background and outline) from the asset provider.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Texture Lifecycle:</b>
    ///         Textures are loaded via <see cref="IAssetProvider" /> which owns and manages their lifecycle.
    ///         The AssetManager caches textures and handles disposal when the manager is disposed.
    ///         This scene does NOT dispose textures - they remain cached for reuse across multiple popup displays.
    ///     </para>
    ///     <para>
    ///         <b>Rationale:</b>
    ///         Map popups are frequently displayed during gameplay (every map transition).
    ///         Caching textures in AssetManager avoids repeated disk I/O and GPU uploads,
    ///         significantly improving performance.
    ///     </para>
    /// </remarks>
    private void LoadPopupTextures()
    {
        try
        {
            // Cast to AssetManager to access GetTexture method
            if (_assetProvider is not AssetManager assetManager)
            {
                Logger.LogError("AssetProvider is not an AssetManager instance");
                return;
            }

            Logger.LogDebug(
                "LoadPopupTextures: Background ID='{BgId}', TexturePath='{BgPath}', Outline ID='{OutId}', TexturePath='{OutPath}'",
                _backgroundDef.BackgroundId, _backgroundDef.TexturePath,
                _outlineDef.OutlineId, _outlineDef.TexturePath);

            // Load outline texture
            string outlineKey = $"popup_outline_{_outlineDef.OutlineId}";
            if (!_assetProvider.HasTexture(outlineKey))
            {
                Logger.LogDebug("Loading outline texture from: {Path}", _outlineDef.TexturePath);
                _assetProvider.LoadTexture(outlineKey, _outlineDef.TexturePath);
            }

            _outlineTexture = assetManager.GetTexture(outlineKey);

            // Load background texture
            string backgroundKey = $"popup_background_{_backgroundDef.BackgroundId}";
            if (!_assetProvider.HasTexture(backgroundKey))
            {
                Logger.LogDebug("Loading background texture from: {Path}", _backgroundDef.TexturePath);
                _assetProvider.LoadTexture(backgroundKey, _backgroundDef.TexturePath);
            }

            _backgroundTexture = assetManager.GetTexture(backgroundKey);

            Logger.LogDebug("Popup textures loaded - Background={BgLoaded}, Outline={OutLoaded}",
                _backgroundTexture != null, _outlineTexture != null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load popup textures - will render without textures");
        }
    }

    private void LoadFont()
    {
        // Try to use cached font from AssetManager (preloaded during initialization)
        if (_assetProvider is AssetManager assetManager && assetManager.HasFont("pokemon"))
        {
            _fontSystem = assetManager.GetFontSystem("pokemon");
            if (_fontSystem != null)
            {
                _font = _fontSystem.GetFont(GbaBaseFontSize);
                Logger.LogDebug("Using cached pokemon font from AssetManager");
                return;
            }
        }

        // Fallback: Create local FontSystem using content provider
        Logger.LogDebug("Font not cached, loading from disk (slower path)");
        _fontSystem = new FontSystem();

        string? fontPath = _contentProvider.ResolveContentPath("Fonts", "pokemon.ttf");

        if (fontPath == null)
        {
            throw new FileNotFoundException("Pokemon font not found: Fonts/pokemon.ttf");
        }

        try
        {
            byte[] fontData = File.ReadAllBytes(fontPath);
            _fontSystem.AddFont(fontData);
            _font = _fontSystem.GetFont(GbaBaseFontSize);
            Logger.LogDebug("Loaded pokemon.ttf font from {FontPath}", fontPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load font from {fontPath}", ex);
        }
    }

    private void CalculatePopupDimensions()
    {
        // In pokeemerald, the background is ALWAYS 80x24 pixels
        // The frame tiles add 8 pixels on each side (1 tile border)
        int tileSize = _outlineDef.IsTileSheet ? _outlineDef.TileWidth : 8;

        // Total popup size includes border tiles (1 tile on each side)
        _popupWidth = GbaBackgroundWidth + (tileSize * 2);
        _popupHeight = GbaBackgroundHeight + (tileSize * 2);
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _elapsedTime += deltaTime;

        switch (_animationState)
        {
            case PopupAnimationState.SlideIn:
                UpdateSlideIn(deltaTime);
                break;

            case PopupAnimationState.Display:
                UpdateDisplay();
                break;

            case PopupAnimationState.SlideOut:
                UpdateSlideOut(deltaTime);
                break;

            case PopupAnimationState.Complete:
                // Request this specific scene to be removed (not just top of stack)
                // This is critical: using RemoveScene(this) instead of PopScene() ensures
                // that if another scene (like ConsoleScene) was pushed on top, we don't
                // accidentally remove that scene instead of ourselves.
                _sceneManager.RemoveScene(this);
                Logger.LogDebug("Animation complete, requesting scene removal");
                // Set to terminal state to prevent re-requesting removal
                _animationState = PopupAnimationState.Disposed;
                break;
        }
    }

    private void UpdateSlideIn(float deltaTime)
    {
        if (_elapsedTime >= SlideInDuration)
        {
            _currentY = _targetY;
            _elapsedTime = 0;
            _animationState = PopupAnimationState.Display;
            Logger.LogDebug("SlideIn complete");
        }
        else
        {
            // Ease-out interpolation for smooth animation (slides DOWN from top)
            float progress = _elapsedTime / SlideInDuration;
            float easedProgress = 1f - MathF.Pow(1f - progress, 3f); // Cubic ease-out
            _currentY = MathHelper.Lerp(-_popupHeight, _targetY, easedProgress);
        }
    }

    private void UpdateDisplay()
    {
        if (_elapsedTime >= DisplayDuration)
        {
            _elapsedTime = 0;
            _animationState = PopupAnimationState.SlideOut;
            Logger.LogDebug("Display complete, sliding out");
        }
    }

    private void UpdateSlideOut(float deltaTime)
    {
        if (_elapsedTime >= SlideOutDuration)
        {
            _animationState = PopupAnimationState.Complete;
            Logger.LogDebug("SlideOut complete");
        }
        else
        {
            // Ease-in interpolation for smooth animation (slides UP to top)
            float progress = _elapsedTime / SlideOutDuration;
            float easedProgress = progress * progress * progress; // Cubic ease-in
            _currentY = MathHelper.Lerp(_targetY, -_popupHeight, easedProgress);
        }
    }

    /// <inheritdoc />
    public override void Draw(GameTime gameTime)
    {
        if (_spriteBatch == null)
        {
            return;
        }

        // Don't render if animation is complete or disposed
        if (_animationState is PopupAnimationState.Complete or PopupAnimationState.Disposed)
        {
            return;
        }

        // Get camera and apply same viewport as game scene
        Camera? camera = GetGameCamera();
        if (!camera.HasValue)
        {
            return; // Can't render without camera
        }

        Camera cameraValue = camera.Value;

        // Use the same virtual viewport as the game rendering
        GraphicsDevice.Viewport = new Viewport(cameraValue.VirtualViewport);

        // Calculate the integer scale factor from GBA native resolution
        // Setting CurrentScale property triggers OnScaleChanged() if value changed
        CurrentScale = cameraValue.VirtualViewport.Width / Camera.GbaNativeWidth;

        // Calculate popup position in SCREEN SPACE (not world space)
        // Map popups are HUD elements that should stay fixed on screen
        int scaledPadding = ScreenPadding * _currentScale;
        int popupX = scaledPadding;

        // Scale the animation offset too (animation is in GBA 1x coordinates)
        int scaledAnimationY = (int)MathF.Round(_currentY * _currentScale);
        int popupY = scaledAnimationY;

        // Render in SCREEN SPACE with no camera transformation
        // Map popups are UI overlays, not world objects
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp, // Point sampling for crisp pixel art
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise,
            null, // No effect
            Matrix.Identity // Screen space (no camera transform)
        );

        // Background position (inside the border frame)
        int bgX = popupX + _cachedBorderThickness;
        int bgY = popupY + _cachedBorderThickness;

        // Draw background texture (required - no fallback)
        if (_backgroundTexture != null)
        {
            _spriteBatch.Draw(
                _backgroundTexture,
                new Rectangle(bgX, bgY, _cachedBgWidth, _cachedBgHeight),
                null, // Use full source texture
                Color.White
            );
        }
        else
        {
            Logger.LogError("Background texture is null - cannot render popup");
        }

        // Draw outline border on top (covers edges of background)
        // IMPORTANT: Pass interior coordinates (bgX, bgY) not outer coordinates (popupX, popupY)
        // DrawTileSheetBorder expects the interior position and draws the frame AROUND it
        if (_outlineTexture != null)
        {
            DrawNineSliceBorder(bgX, bgY, _cachedBgWidth, _cachedBgHeight, _currentScale);
        }

        // Draw text (pokeemerald-accurate: centered within 80px, Y offset 3px from window top)
        if (_scaledFont != null && _cachedDisplayText != null)
        {
            Vector2 textSize = _scaledFont.MeasureString(_cachedDisplayText);

            // Center text within scaled width (pokeemerald: centered in 80px at 1x scale)
            float textX = bgX + ((_cachedMaxTextWidth - textSize.X) / 2);
            float textY = bgY + _cachedTextOffsetY;

            // CRITICAL: Round to integer positions for crisp pixel-perfect rendering
            // Sub-pixel positioning causes blurriness with point sampling
            int intTextX = (int)Math.Round(textX);
            int intTextY = (int)Math.Round(textY);

            // Draw text shadow first (pokeemerald uses DARK_GRAY shadow)
            _spriteBatch.DrawString(
                _scaledFont,
                _cachedDisplayText,
                new Vector2(intTextX + _cachedShadowOffset, intTextY + _cachedShadowOffset),
                new Color(72, 72, 80, 255) // Dark gray shadow, fully opaque
            );

            // Draw main text on top (pokeemerald uses WHITE text)
            _spriteBatch.DrawString(
                _scaledFont,
                _cachedDisplayText,
                new Vector2(intTextX, intTextY),
                new Color(255, 255, 255, 255) // White text, fully opaque
            );
        }

        _spriteBatch.End();
    }

    /// <summary>
    ///     Called when CurrentScale changes - recalculates all scale-dependent values.
    ///     This event-driven approach avoids checking every frame.
    /// </summary>
    private void OnScaleChanged()
    {
        // Recalculate scaled dimensions
        int baseBorderThickness = _outlineDef.IsTileSheet ? _outlineDef.TileWidth : _outlineDef.BorderWidth;
        _cachedBorderThickness = baseBorderThickness * _currentScale;
        _cachedBgWidth = GbaBackgroundWidth * _currentScale;
        _cachedBgHeight = GbaBackgroundHeight * _currentScale;
        _cachedMaxTextWidth = GbaBackgroundWidth * _currentScale;
        _cachedTextPadding = GbaTextPadding * _currentScale;
        _cachedTextOffsetY = GbaTextOffsetY * _currentScale;
        _cachedShadowOffset = GbaShadowOffsetX * _currentScale; // X and Y are same (1px each)

        // Recalculate font at new scale
        if (_fontSystem != null)
        {
            _scaledFont = _fontSystem.GetFont(GbaBaseFontSize * _currentScale);

            // Recalculate text truncation with new font
            RecalculateTextTruncation();
        }
    }

    /// <summary>
    ///     Truncates text to fit within the popup width using binary search.
    /// </summary>
    private void RecalculateTextTruncation()
    {
        if (_scaledFont == null)
        {
            return;
        }

        // Calculate usable width for text
        int usableWidth = _cachedMaxTextWidth - (_cachedTextPadding * 2);

        // Measure full text
        string displayText = _mapName;
        Vector2 textSize = _scaledFont.MeasureString(displayText);

        // Truncate if too wide using binary search
        if (textSize.X > usableWidth)
        {
            int left = 0;
            int right = displayText.Length;
            int bestFit = 0;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                string testText = displayText[..mid];
                Vector2 testSize = _scaledFont.MeasureString(testText);

                if (testSize.X <= usableWidth)
                {
                    bestFit = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            if (bestFit > 0)
            {
                displayText = displayText[..bestFit];
            }
        }

        _cachedDisplayText = displayText;
    }

    /// <summary>
    ///     Gets the camera from the game world using the injected camera provider.
    ///     Returns null if camera is not available.
    /// </summary>
    private Camera? GetGameCamera()
    {
        return _cameraProvider.GetActiveCamera();
    }

    /// <summary>
    ///     Draws the border using either tile sheet rendering (GBA-accurate) or legacy 9-slice.
    /// </summary>
    /// <param name="scale">The viewport scale factor to apply to all dimensions.</param>
    private void DrawNineSliceBorder(int x, int y, int width, int height, int scale)
    {
        if (_outlineTexture == null || _spriteBatch == null)
        {
            return;
        }

        // Use tile sheet rendering if available (GBA-accurate)
        if (_outlineDef.IsTileSheet)
        {
            DrawTileSheetBorder(x, y, width, height, scale);
        }
        else
        {
            DrawLegacyNineSliceBorder(x, y, width, height, scale);
        }
    }

    /// <summary>
    ///     Draws a tile-based border (GBA-accurate pokeemerald style).
    ///     Matches DrawMapNamePopUpFrame from pokeemerald exactly.
    /// </summary>
    /// <param name="scale">The viewport scale factor to apply to tile dimensions.</param>
    private void DrawTileSheetBorder(int x, int y, int width, int height, int scale)
    {
        if (_outlineTexture == null || _spriteBatch == null || _outlineDef.Tiles.Count == 0 ||
            _outlineDef.TileUsage == null)
        {
            return;
        }

        // Scale tile dimensions to match viewport scaling
        int tileW = _outlineDef.TileWidth * scale;
        int tileH = _outlineDef.TileHeight * scale;

        // Build a lookup dictionary for tiles by their Index property
        // EF Core owned collections don't guarantee order, so we can't use array position
        var tileLookup = _outlineDef.Tiles.ToDictionary(t => t.Index);

        // Helper to draw a single tile by its Index property (not array position)
        void DrawTile(int tileIndex, int destX, int destY)
        {
            if (!tileLookup.TryGetValue(tileIndex, out OutlineTile? tile))
            {
                return;
            }

            var srcRect = new Rectangle(tile.X, tile.Y, tile.Width, tile.Height);
            var destRect = new Rectangle(destX, destY, tileW, tileH);
            _spriteBatch.Draw(_outlineTexture, destRect, srcRect, Color.White);
        }

        OutlineTileUsage usage = _outlineDef.TileUsage;

        // Pokeemerald draws the window interior at (x, y) with size (deltaX, deltaY)
        // The frame is drawn AROUND it
        // For 80x24 background: deltaX = 10 tiles, deltaY = 3 tiles

        // Draw top edge (12 tiles from x-1 to x+10)
        // This includes the top-left and top-right corner tiles
        for (int i = 0; i < usage.TopEdge.Count && i < 12; i++)
        {
            int tileIndex = usage.TopEdge[i];
            int tileX = x + ((i - 1) * tileW); // i-1 means first tile is at x-1 (pokeemerald: i - 1 + x)
            int tileY = y - tileH; // y-1 in tile units (pokeemerald: y - 1)
            DrawTile(tileIndex, tileX, tileY);
        }

        // Draw left edge (3 tiles at x-1, for y+0, y+1, y+2)
        for (int i = 0; i < usage.LeftEdge.Count && i < 3; i++)
        {
            int tileIndex = usage.LeftEdge[i];
            DrawTile(tileIndex, x - tileW, y + (i * tileH));
        }

        // Draw right edge (3 tiles at deltaX+x, for y+0, y+1, y+2)
        for (int i = 0; i < usage.RightEdge.Count && i < GbaInteriorTilesY; i++)
        {
            int tileIndex = usage.RightEdge[i];
            DrawTile(tileIndex, x + (GbaInteriorTilesX * tileW), y + (i * tileH));
        }

        // Draw bottom edge (12 tiles from x-1 to x+10)
        // This includes the bottom-left and bottom-right corner tiles
        for (int i = 0; i < usage.BottomEdge.Count && i < 12; i++)
        {
            int tileIndex = usage.BottomEdge[i];
            int tileX = x + ((i - 1) * tileW);
            int tileY = y + (GbaInteriorTilesY * tileH);
            DrawTile(tileIndex, tileX, tileY);
        }
    }

    /// <summary>
    ///     Draws a 9-slice border (legacy rendering for backwards compatibility).
    /// </summary>
    private void DrawLegacyNineSliceBorder(int x, int y, int width, int height, int scale)
    {
        if (_outlineTexture == null || _spriteBatch == null)
        {
            return;
        }

        // Original texture dimensions (unscaled)
        int srcCornerW = _outlineDef.CornerWidth;
        int srcCornerH = _outlineDef.CornerHeight;
        int texWidth = _outlineTexture.Width;
        int texHeight = _outlineTexture.Height;

        // Scaled destination dimensions
        int destCornerW = srcCornerW * scale;
        int destCornerH = srcCornerH * scale;

        // Calculate source rectangles for 9-slice regions (unscaled texture coordinates)
        var srcTopLeft = new Rectangle(0, 0, srcCornerW, srcCornerH);
        var srcTopRight = new Rectangle(texWidth - srcCornerW, 0, srcCornerW, srcCornerH);
        var srcBottomLeft = new Rectangle(0, texHeight - srcCornerH, srcCornerW, srcCornerH);
        var srcBottomRight = new Rectangle(texWidth - srcCornerW, texHeight - srcCornerH, srcCornerW, srcCornerH);

        var srcTop = new Rectangle(srcCornerW, 0, texWidth - (srcCornerW * 2), srcCornerH);
        var srcBottom = new Rectangle(srcCornerW, texHeight - srcCornerH, texWidth - (srcCornerW * 2), srcCornerH);
        var srcLeft = new Rectangle(0, srcCornerH, srcCornerW, texHeight - (srcCornerH * 2));
        var srcRight = new Rectangle(texWidth - srcCornerW, srcCornerH, srcCornerW, texHeight - (srcCornerH * 2));

        // Draw corners (scaled destinations, unscaled sources)
        _spriteBatch.Draw(_outlineTexture, new Rectangle(x, y, destCornerW, destCornerH), srcTopLeft, Color.White);
        _spriteBatch.Draw(_outlineTexture, new Rectangle(x + width - destCornerW, y, destCornerW, destCornerH),
            srcTopRight, Color.White);
        _spriteBatch.Draw(_outlineTexture, new Rectangle(x, y + height - destCornerH, destCornerW, destCornerH),
            srcBottomLeft, Color.White);
        _spriteBatch.Draw(_outlineTexture,
            new Rectangle(x + width - destCornerW, y + height - destCornerH, destCornerW, destCornerH), srcBottomRight,
            Color.White);

        // Draw edges (stretched to fill space between corners)
        _spriteBatch.Draw(_outlineTexture, new Rectangle(x + destCornerW, y, width - (destCornerW * 2), destCornerH),
            srcTop, Color.White);
        _spriteBatch.Draw(_outlineTexture,
            new Rectangle(x + destCornerW, y + height - destCornerH, width - (destCornerW * 2), destCornerH), srcBottom,
            Color.White);
        _spriteBatch.Draw(_outlineTexture, new Rectangle(x, y + destCornerH, destCornerW, height - (destCornerH * 2)),
            srcLeft, Color.White);
        _spriteBatch.Draw(_outlineTexture,
            new Rectangle(x + width - destCornerW, y + destCornerH, destCornerW, height - (destCornerH * 2)), srcRight,
            Color.White);
    }

    /// <inheritdoc />
    public override void UnloadContent()
    {
        base.UnloadContent();

        // Only dispose FontSystem if we created it locally (not cached)
        // Cached fonts are owned by AssetManager
        if (_assetProvider is AssetManager assetManager && assetManager.HasFont("pokemon"))
        {
            // Font is cached - don't dispose
            _fontSystem = null;
        }
        else
        {
            _fontSystem?.Dispose();
            _fontSystem = null;
        }

        // SpriteBatch is shared via RenderingService - do NOT dispose here
        // RenderingService owns the SpriteBatch lifecycle
    }

    private enum PopupAnimationState
    {
        SlideIn,
        Display,
        SlideOut,
        Complete,
        Disposed // Terminal state after scene is popped
    }
}
