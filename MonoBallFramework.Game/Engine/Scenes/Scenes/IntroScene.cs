using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBallFramework.Game.Engine.Scenes.Scenes;

/// <summary>
///     Intro scene that displays the game logo with a spin-in, hold, fade-out animation
///     before transitioning to the loading screen.
///     The logo spins in from a small size while rotating until it fills the screen
///     (either full width or full height, whichever comes first), holds briefly,
///     then the logo fades out to transition to the loading screen.
/// </summary>
public class IntroScene : SceneBase
{
    // Animation timing (in seconds)
    private const float PauseBeforeSpinDuration = 0.5f;
    private const float SpinInDuration = 2.5f;
    private const float HoldDuration = 2.0f;
    private const float FadeOutDuration = 1.0f;
    private const float TotalDuration = PauseBeforeSpinDuration + SpinInDuration + HoldDuration + FadeOutDuration;

    // Spin animation parameters
    private const float StartScale = 0.05f; // Start very small
    private const float TotalRotations = 2.0f; // Number of full rotations during spin-in

    // Colors - matches the outer edge of the logo
    private static readonly Color BackgroundColor = new(234, 234, 233);

    private readonly Func<IScene> _createNextScene;
    private readonly ILogger<IntroScene> _logger;
    private readonly SceneManager _sceneManager;

    private float _elapsedTime;
    private Texture2D? _logoTexture;
    private Texture2D? _pixel;
    private SpriteBatch? _spriteBatch;
    private bool _transitionStarted;

    /// <summary>
    ///     Initializes a new instance of the IntroScene class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="logger">The logger for this scene.</param>
    /// <param name="sceneManager">The scene manager for transitions.</param>
    /// <param name="createNextScene">Factory function to create the next scene (loading scene).</param>
    public IntroScene(
        GraphicsDevice graphicsDevice,
        ILogger<IntroScene> logger,
        SceneManager sceneManager,
        Func<IScene> createNextScene
    )
        : base(graphicsDevice, logger)
    {
        ArgumentNullException.ThrowIfNull(sceneManager);
        ArgumentNullException.ThrowIfNull(createNextScene);

        _logger = logger;
        _sceneManager = sceneManager;
        _createNextScene = createNextScene;
    }

    /// <inheritdoc />
    public override void LoadContent()
    {
        base.LoadContent();

        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a 1x1 white pixel texture for drawing
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Load the logo texture from Assets folder
        try
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png");

            if (File.Exists(logoPath))
            {
                using FileStream stream = File.OpenRead(logoPath);
                _logoTexture = Texture2D.FromStream(GraphicsDevice, stream);
                _logger.LogInformation("Logo loaded successfully from {Path}", logoPath);
            }
            else
            {
                _logger.LogWarning("Logo file not found at {Path}", logoPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load logo texture");
        }
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update elapsed time
        _elapsedTime += deltaTime;

        // Check if intro is complete
        if (_elapsedTime >= TotalDuration && !_transitionStarted)
        {
            _transitionStarted = true;
            TransitionToNextScene();
        }
    }

    /// <inheritdoc />
    public override void Draw(GameTime gameTime)
    {
        // Calculate animation state
        AnimationState state = CalculateAnimationState();

        // Static background color (no fade-in)
        GraphicsDevice.Clear(BackgroundColor);

        if (_spriteBatch == null)
        {
            return;
        }

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

        if (state.ShowLogo)
        {
            if (_logoTexture != null)
            {
                DrawLogo(state);
            }
            else
            {
                DrawPlaceholder(state);
            }
        }

        _spriteBatch.End();
    }

    /// <summary>
    ///     Animation state containing scale, rotation, and alpha values.
    /// </summary>
    private readonly record struct AnimationState(
        float Scale,
        float Rotation,
        float Alpha,
        bool ShowLogo = true,
        float LogoAlpha = 1f
    );

    /// <summary>
    ///     Calculates the current animation state based on elapsed time.
    /// </summary>
    private AnimationState CalculateAnimationState()
    {
        if (_elapsedTime < PauseBeforeSpinDuration)
        {
            // Pause phase - waiting before logo appears
            return new AnimationState(0f, 0f, 1f, ShowLogo: false);
        }

        float spinStartTime = PauseBeforeSpinDuration;

        if (_elapsedTime < spinStartTime + SpinInDuration)
        {
            // Spin-in phase - scale up while rotating until logo fills screen
            float progress = (_elapsedTime - spinStartTime) / SpinInDuration;
            float easedProgress = EaseOutCubic(progress);

            // Scale from tiny to full screen (1.0 = fills screen width or height)
            float scale = MathHelper.Lerp(StartScale, 1.0f, easedProgress);

            // Rotation slows down as it approaches final position
            float rotation = MathHelper.TwoPi * TotalRotations * EaseOutQuad(progress);

            // Logo at full opacity (no fade-in)
            return new AnimationState(scale, rotation, 1f, ShowLogo: true, LogoAlpha: 1f);
        }

        float holdStartTime = spinStartTime + SpinInDuration;

        if (_elapsedTime < holdStartTime + HoldDuration)
        {
            // Hold phase - full size, no rotation, full opacity
            return new AnimationState(1.0f, 0f, 1f, ShowLogo: true);
        }

        // Spin-out phase - logo shrinks while spinning until gone
        float spinOutStartTime = holdStartTime + HoldDuration;
        float outProgress = (_elapsedTime - spinOutStartTime) / FadeOutDuration;
        float clampedOutProgress = Math.Min(outProgress, 1.0f);
        float easedOutProgress = EaseInQuad(clampedOutProgress);

        // Scale from full size down to zero
        float outScale = MathHelper.Lerp(1.0f, 0f, easedOutProgress);

        // Continue rotating (accelerates as it shrinks)
        float outRotation = MathHelper.TwoPi * TotalRotations * clampedOutProgress;

        return new AnimationState(outScale, outRotation, 1f, ShowLogo: outScale > 0.001f, LogoAlpha: 1f);
    }

    /// <summary>
    ///     Quadratic ease-out function.
    /// </summary>
    private static float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }

    /// <summary>
    ///     Cubic ease-out function - smoother deceleration.
    /// </summary>
    private static float EaseOutCubic(float t)
    {
        return 1 - (float)Math.Pow(1 - t, 3);
    }

    /// <summary>
    ///     Quadratic ease-in function.
    /// </summary>
    private static float EaseInQuad(float t)
    {
        return t * t;
    }

    /// <summary>
    ///     Draws the logo centered on screen with the current animation state.
    ///     At scale 1.0, the logo fills either the full width or full height of the screen
    ///     (whichever comes first while maintaining aspect ratio).
    /// </summary>
    private void DrawLogo(AnimationState state)
    {
        if (_spriteBatch == null || _logoTexture == null)
        {
            return;
        }

        Viewport viewport = GraphicsDevice.Viewport;
        Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);

        // Calculate the size needed to fill the screen (width or height, whichever is smaller ratio)
        float textureAspect = (float)_logoTexture.Width / _logoTexture.Height;
        float screenAspect = (float)viewport.Width / viewport.Height;

        float fullSizeWidth, fullSizeHeight;

        if (textureAspect > screenAspect)
        {
            // Texture is wider than screen - fit to width
            fullSizeWidth = viewport.Width;
            fullSizeHeight = viewport.Width / textureAspect;
        }
        else
        {
            // Texture is taller than screen - fit to height
            fullSizeHeight = viewport.Height;
            fullSizeWidth = viewport.Height * textureAspect;
        }

        // Apply scale from animation state (1.0 = fills screen)
        float scaledWidth = fullSizeWidth * state.Scale;
        float scaledHeight = fullSizeHeight * state.Scale;

        // Draw with origin at center for proper rotation
        Vector2 origin = new(_logoTexture.Width / 2f, _logoTexture.Height / 2f);

        Color tint = Color.White * state.LogoAlpha;

        _spriteBatch.Draw(
            _logoTexture,
            center,
            null,
            tint,
            state.Rotation,
            origin,
            new Vector2(scaledWidth / _logoTexture.Width, scaledHeight / _logoTexture.Height),
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>
    ///     Draws placeholder if logo texture is not available.
    /// </summary>
    private void DrawPlaceholder(AnimationState state)
    {
        if (_spriteBatch == null || _pixel == null)
        {
            return;
        }

        Viewport viewport = GraphicsDevice.Viewport;
        Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);

        // Draw a placeholder that scales to fill screen like the logo would
        int fullSize = Math.Min(viewport.Width, viewport.Height);
        int size = (int)(fullSize * state.Scale);

        Color color = new Color(235, 72, 60) * state.LogoAlpha; // Pokéball red
        _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size), color);
    }

    /// <summary>
    ///     Transitions to the next scene (loading scene).
    /// </summary>
    private void TransitionToNextScene()
    {
        _logger.LogInformation("Intro complete, transitioning to loading scene");

        try
        {
            IScene nextScene = _createNextScene();
            _sceneManager.ChangeScene(nextScene);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create and transition to next scene");
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spriteBatch?.Dispose();
            _pixel?.Dispose();
            _logoTexture?.Dispose();
        }

        base.Dispose(disposing);
    }
}
