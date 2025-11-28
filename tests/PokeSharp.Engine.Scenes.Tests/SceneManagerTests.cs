using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using Xunit;

namespace PokeSharp.Engine.Scenes.Tests;

/// <summary>
///     Unit tests for SceneManager class.
/// </summary>
public class SceneManagerTests : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;

    public SceneManagerTests()
    {
        // Create a headless GraphicsDevice for testing
        // This approach avoids requiring a Game instance with a window
        try
        {
            var presentationParameters = new PresentationParameters
            {
                BackBufferWidth = 800,
                BackBufferHeight = 600,
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.Depth24,
                DeviceWindowHandle = IntPtr.Zero,
                IsFullScreen = false,
            };

            _graphicsDevice = new GraphicsDevice(
                GraphicsAdapter.DefaultAdapter,
                GraphicsProfile.HiDef,
                presentationParameters
            );
        }
        catch (NullReferenceException)
        {
            // In headless test environments, GraphicsAdapter may not be available
            // This is a known limitation of XNA/MonoGame in CI/headless environments
            // For now, we'll use a mock/null pattern or skip tests that require graphics
            throw new InvalidOperationException(
                "GraphicsDevice cannot be created in this environment. "
                    + "GraphicsAdapter is not available (typically in headless CI environments). "
                    + "Consider using mock GraphicsDevice or marking tests as [Fact(Skip = \"Requires graphics adapter\")]"
            );
        }
    }

    public void Dispose()
    {
        _graphicsDevice?.Dispose();
    }

    private SceneManager CreateSceneManager()
    {
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<SceneManager>>();

        return new SceneManager(_graphicsDevice, services.Object, logger.Object);
    }

    private static Mock<IScene> CreateMockScene(string name)
    {
        var scene = new Mock<IScene>();
        scene.Setup(s => s.GetType().Name).Returns(name);
        scene.Setup(s => s.IsDisposed).Returns(false);
        scene.Setup(s => s.IsInitialized).Returns(false);
        scene.Setup(s => s.IsContentLoaded).Returns(false);
        return scene;
    }

    [Fact]
    public void ChangeScene_ShouldQueueSceneChange()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> scene = CreateMockScene("TestScene");

        // Act
        manager.ChangeScene(scene.Object);

        // Assert - Scene should not be current yet (two-step pattern)
        manager.CurrentScene.Should().BeNull();
    }

    [Fact]
    public void ChangeScene_ShouldTransitionOnNextUpdate()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime);

        // Assert - Scene should now be current
        manager.CurrentScene.Should().Be(scene.Object);
        scene.Verify(s => s.Initialize(), Times.Once);
        scene.Verify(s => s.LoadContent(), Times.Once);
    }

    [Fact]
    public void ChangeScene_ShouldDisposePreviousScene()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> scene1 = CreateMockScene("Scene1");
        Mock<IScene> scene2 = CreateMockScene("Scene2");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene1.Object);
        manager.Update(gameTime);
        manager.ChangeScene(scene2.Object);
        manager.Update(gameTime);

        // Assert
        scene1.Verify(s => s.Dispose(), Times.Once);
        manager.CurrentScene.Should().Be(scene2.Object);
    }

    [Fact]
    public void PushScene_ShouldAddToStack()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> baseScene = CreateMockScene("BaseScene");
        Mock<IScene> overlayScene = CreateMockScene("OverlayScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(baseScene.Object);
        manager.Update(gameTime);
        manager.PushScene(overlayScene.Object);
        manager.Update(gameTime);

        // Assert
        manager.CurrentScene.Should().Be(overlayScene.Object); // Stack top
        overlayScene.Verify(s => s.Initialize(), Times.Once);
        overlayScene.Verify(s => s.LoadContent(), Times.Once);
    }

    [Fact]
    public void PopScene_ShouldRemoveFromStack()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> baseScene = CreateMockScene("BaseScene");
        Mock<IScene> overlayScene = CreateMockScene("OverlayScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(baseScene.Object);
        manager.Update(gameTime);
        manager.PushScene(overlayScene.Object);
        manager.Update(gameTime);
        manager.PopScene();
        manager.Update(gameTime);

        // Assert
        overlayScene.Verify(s => s.Dispose(), Times.Once);
        manager.CurrentScene.Should().Be(baseScene.Object);
    }

    [Fact]
    public void Update_ShouldUpdateCurrentScene()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime);
        manager.Update(gameTime); // Second update

        // Assert
        scene.Verify(s => s.Update(gameTime), Times.Once); // Called once after transition
    }

    [Fact]
    public void Draw_ShouldDrawCurrentScene()
    {
        // Arrange
        SceneManager manager = CreateSceneManager();
        Mock<IScene> scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime); // Initialize scene
        manager.Draw(gameTime);

        // Assert
        scene.Verify(s => s.Draw(gameTime), Times.Once);
    }
}
