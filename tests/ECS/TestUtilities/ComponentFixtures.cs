using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Common;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Components.Rendering;

namespace PokeSharp.Tests.ECS.TestUtilities;

/// <summary>
///     Provides pre-built component configurations for testing.
///     Contains common test data and component setups.
/// </summary>
public static class ComponentFixtures
{
    #region Position Fixtures

    /// <summary>
    ///     Creates a Position at the origin (0, 0).
    /// </summary>
    public static Position CreatePositionAtOrigin() => new Position(0, 0);

    /// <summary>
    ///     Creates a Position at (10, 10).
    /// </summary>
    public static Position CreatePositionAt10x10() => new Position(10, 10);

    /// <summary>
    ///     Creates a Position at custom coordinates.
    /// </summary>
    public static Position CreatePositionAt(int x, int y, int mapId = 0) => new Position(x, y, mapId);

    /// <summary>
    ///     Creates a Position with specific pixel coordinates.
    /// </summary>
    public static Position CreatePositionWithPixels(int gridX, int gridY, float pixelX, float pixelY)
    {
        return new Position(gridX, gridY)
        {
            PixelX = pixelX,
            PixelY = pixelY
        };
    }

    #endregion

    #region GridMovement Fixtures

    /// <summary>
    ///     Creates a GridMovement component with default speed (4.0).
    /// </summary>
    public static GridMovement CreateDefaultMovement() => new GridMovement(4.0f);

    /// <summary>
    ///     Creates a GridMovement component with fast speed (8.0).
    /// </summary>
    public static GridMovement CreateFastMovement() => new GridMovement(8.0f);

    /// <summary>
    ///     Creates a GridMovement component with slow speed (2.0).
    /// </summary>
    public static GridMovement CreateSlowMovement() => new GridMovement(2.0f);

    /// <summary>
    ///     Creates a GridMovement component with a movement in progress.
    /// </summary>
    public static GridMovement CreateMovementInProgress(Direction direction, float progress = 0.5f)
    {
        var movement = new GridMovement(4.0f);
        movement.StartMovement(Vector2.Zero, new Vector2(16, 0), direction);
        movement.MovementProgress = progress;
        return movement;
    }

    /// <summary>
    ///     Creates a GridMovement component with locked movement.
    /// </summary>
    public static GridMovement CreateLockedMovement()
    {
        var movement = new GridMovement(4.0f);
        movement.MovementLocked = true;
        return movement;
    }

    #endregion

    #region MovementRequest Fixtures

    /// <summary>
    ///     Creates a MovementRequest for moving up.
    /// </summary>
    public static MovementRequest CreateMoveUpRequest() => new MovementRequest(Direction.Up);

    /// <summary>
    ///     Creates a MovementRequest for moving down.
    /// </summary>
    public static MovementRequest CreateMoveDownRequest() => new MovementRequest(Direction.Down);

    /// <summary>
    ///     Creates a MovementRequest for moving left.
    /// </summary>
    public static MovementRequest CreateMoveLeftRequest() => new MovementRequest(Direction.Left);

    /// <summary>
    ///     Creates a MovementRequest for moving right.
    /// </summary>
    public static MovementRequest CreateMoveRightRequest() => new MovementRequest(Direction.Right);

    #endregion

    #region Animation Fixtures

    /// <summary>
    ///     Creates an Animation component with idle_down animation.
    /// </summary>
    public static Animation CreateIdleDownAnimation() => new Animation
    {
        CurrentAnimation = "idle_down",
        FrameIndex = 0,
        TimeInFrame = 0f
    };

    /// <summary>
    ///     Creates an Animation component with walk_up animation.
    /// </summary>
    public static Animation CreateWalkUpAnimation() => new Animation
    {
        CurrentAnimation = "walk_up",
        FrameIndex = 0,
        TimeInFrame = 0f
    };

    /// <summary>
    ///     Creates an Animation component with a custom animation.
    /// </summary>
    public static Animation CreateCustomAnimation(string animationName, int frameIndex = 0, float timeInFrame = 0f)
    {
        return new Animation
        {
            CurrentAnimation = animationName,
            FrameIndex = frameIndex,
            TimeInFrame = timeInFrame
        };
    }

    #endregion

    #region Tag Fixtures

    /// <summary>
    ///     Creates a Tag component with "Player" value.
    /// </summary>
    public static Tag CreatePlayerTag() => new Tag("Player");

    /// <summary>
    ///     Creates a Tag component with "NPC" value.
    /// </summary>
    public static Tag CreateNpcTag() => new Tag("NPC");

    /// <summary>
    ///     Creates a Tag component with "Enemy" value.
    /// </summary>
    public static Tag CreateEnemyTag() => new Tag("Enemy");

    /// <summary>
    ///     Creates a Tag component with a custom value.
    /// </summary>
    public static Tag CreateCustomTag(string value) => new Tag(value);

    #endregion

    #region NPC Fixtures

    /// <summary>
    ///     Creates an NpcBehavior component with Wander behavior.
    /// </summary>
    public static NpcBehavior CreateWanderNpc() => new NpcBehavior
    {
        BehaviorType = NpcBehaviorType.Wander,
        MovementSpeed = 2.0f,
        WanderRadius = 5,
        WaitTimeMin = 2.0f,
        WaitTimeMax = 5.0f
    };

    /// <summary>
    ///     Creates an NpcBehavior component with Patrol behavior.
    /// </summary>
    public static NpcBehavior CreatePatrolNpc(List<Vector2>? patrolPath = null) => new NpcBehavior
    {
        BehaviorType = NpcBehaviorType.Patrol,
        MovementSpeed = 2.0f,
        PatrolPath = patrolPath ?? new List<Vector2> { Vector2.Zero, new Vector2(5, 0) },
        CurrentPatrolIndex = 0
    };

    /// <summary>
    ///     Creates an NpcBehavior component with Stationary behavior.
    /// </summary>
    public static NpcBehavior CreateStationaryNpc() => new NpcBehavior
    {
        BehaviorType = NpcBehaviorType.Stationary,
        FacingDirection = Direction.Down
    };

    #endregion

    #region Player Fixtures

    /// <summary>
    ///     Creates a PlayerInput component with no input.
    /// </summary>
    public static PlayerInput CreatePlayerInput() => new PlayerInput
    {
        MoveDirection = Direction.None,
        InteractPressed = false,
        MenuPressed = false
    };

    /// <summary>
    ///     Creates a PlayerInput component with movement input.
    /// </summary>
    public static PlayerInput CreatePlayerInputWithMovement(Direction direction) => new PlayerInput
    {
        MoveDirection = direction,
        InteractPressed = false,
        MenuPressed = false
    };

    /// <summary>
    ///     Creates a PlayerInput component with interact pressed.
    /// </summary>
    public static PlayerInput CreatePlayerInputWithInteract() => new PlayerInput
    {
        MoveDirection = Direction.None,
        InteractPressed = true,
        MenuPressed = false
    };

    #endregion

    #region Rendering Fixtures

    /// <summary>
    ///     Creates a SpriteRenderer component with default values.
    /// </summary>
    public static SpriteRenderer CreateSpriteRenderer(string? textureName = null) => new SpriteRenderer
    {
        TextureName = textureName ?? "default_sprite",
        SourceRectangle = new Rectangle(0, 0, 16, 16),
        Color = Color.White,
        LayerDepth = 0.5f
    };

    /// <summary>
    ///     Creates a SpriteRenderer component for a player character.
    /// </summary>
    public static SpriteRenderer CreatePlayerSpriteRenderer() => new SpriteRenderer
    {
        TextureName = "player_spritesheet",
        SourceRectangle = new Rectangle(0, 0, 16, 16),
        Color = Color.White,
        LayerDepth = 0.6f
    };

    /// <summary>
    ///     Creates a SpriteRenderer component for an NPC.
    /// </summary>
    public static SpriteRenderer CreateNpcSpriteRenderer() => new SpriteRenderer
    {
        TextureName = "npc_spritesheet",
        SourceRectangle = new Rectangle(0, 0, 16, 16),
        Color = Color.White,
        LayerDepth = 0.5f
    };

    #endregion

    #region Collision Fixtures

    /// <summary>
    ///     Creates a Collider component with default size.
    /// </summary>
    public static Collider CreateDefaultCollider() => new Collider
    {
        Width = 16,
        Height = 16,
        IsSolid = true
    };

    /// <summary>
    ///     Creates a Collider component with custom size.
    /// </summary>
    public static Collider CreateCustomCollider(int width, int height, bool isSolid = true) => new Collider
    {
        Width = width,
        Height = height,
        IsSolid = isSolid
    };

    /// <summary>
    ///     Creates a trigger Collider (non-solid).
    /// </summary>
    public static Collider CreateTriggerCollider() => new Collider
    {
        Width = 16,
        Height = 16,
        IsSolid = false
    };

    #endregion

    #region Test Data Generators

    /// <summary>
    ///     Generates random positions within a grid range.
    /// </summary>
    public static IEnumerable<Position> GenerateRandomPositions(int count, int maxX, int maxY, int seed = 12345)
    {
        var random = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            yield return new Position(random.Next(0, maxX), random.Next(0, maxY));
        }
    }

    /// <summary>
    ///     Generates a grid of positions.
    /// </summary>
    public static IEnumerable<Position> GenerateGridPositions(int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                yield return new Position(x, y);
            }
        }
    }

    #endregion
}
