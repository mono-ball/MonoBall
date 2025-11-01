using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Input.Components;

namespace PokeSharp.Input.Systems;

/// <summary>
/// System that processes keyboard and gamepad input and converts it to movement commands.
/// Implements Pokemon-style grid-locked input with buffering for responsive controls.
/// </summary>
public class InputSystem : BaseSystem
{
    private const int TileSize = 16;
    private const float InputBufferDuration = 0.1f; // 100ms input buffer

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Input;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        var keyboardState = Keyboard.GetState();
        var gamepadState = GamePad.GetState(PlayerIndex.One);

        // Query player entities with input state and direction component
        var query = new QueryDescription()
            .WithAll<Player, Position, GridMovement, InputState, Direction>();

        world.Query(in query, (Entity entity, ref Position position, ref GridMovement movement, ref InputState input) =>
        {
            if (!input.InputEnabled)
            {
                return;
            }

            // Decrease input buffer time
            if (input.InputBufferTime > 0)
            {
                input.InputBufferTime -= deltaTime;
            }

            // Get current input direction
            var currentDirection = GetInputDirection(keyboardState, gamepadState);

            // Update pressed direction if input detected
            if (currentDirection != Direction.None)
            {
                input.PressedDirection = currentDirection;
                input.InputBufferTime = InputBufferDuration;

                // Synchronize Direction component with input direction
                ref var direction = ref entity.Get<Direction>();
                direction = currentDirection;
            }

            // Check for action button
            input.ActionPressed = keyboardState.IsKeyDown(Keys.Space) ||
                                 keyboardState.IsKeyDown(Keys.Enter) ||
                                 keyboardState.IsKeyDown(Keys.Z) ||
                                 gamepadState.Buttons.A == ButtonState.Pressed;

            // Process movement if not currently moving and we have buffered input
            if (!movement.IsMoving && input.InputBufferTime > 0 && input.PressedDirection != Direction.None)
            {
                StartMovement(world, ref position, ref movement, input.PressedDirection);
                input.InputBufferTime = 0f; // Consume buffered input
            }
        });
    }

    private static Direction GetInputDirection(KeyboardState keyboard, GamePadState gamepad)
    {
        // Keyboard input (priority: most recently pressed)
        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
            return Direction.Up;
        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
            return Direction.Down;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            return Direction.Left;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            return Direction.Right;

        // Gamepad input
        var thumbstick = gamepad.ThumbSticks.Left;
        if (thumbstick.Y > 0.5f || gamepad.DPad.Up == ButtonState.Pressed)
            return Direction.Up;
        if (thumbstick.Y < -0.5f || gamepad.DPad.Down == ButtonState.Pressed)
            return Direction.Down;
        if (thumbstick.X < -0.5f || gamepad.DPad.Left == ButtonState.Pressed)
            return Direction.Left;
        if (thumbstick.X > 0.5f || gamepad.DPad.Right == ButtonState.Pressed)
            return Direction.Right;

        return Direction.None;
    }

    private static void StartMovement(World world, ref Position position, ref GridMovement movement, Direction direction)
    {
        // Calculate target grid position
        int targetX = position.X;
        int targetY = position.Y;

        switch (direction)
        {
            case Direction.Up:
                targetY--;
                break;
            case Direction.Down:
                targetY++;
                break;
            case Direction.Left:
                targetX--;
                break;
            case Direction.Right:
                targetX++;
                break;
        }

        // Check if target tile is a Pokemon ledge
        if (CollisionSystem.IsLedge(world, targetX, targetY))
        {
            // Get the allowed jump direction for this ledge
            Direction allowedJumpDir = CollisionSystem.GetLedgeJumpDirection(world, targetX, targetY);

            // Only allow jumping in the specified direction
            if (direction == allowedJumpDir)
            {
                // Calculate landing position (2 tiles in jump direction)
                int jumpLandX = targetX;
                int jumpLandY = targetY;

                switch (allowedJumpDir)
                {
                    case Direction.Down:
                        jumpLandY++;
                        break;
                    case Direction.Up:
                        jumpLandY--;
                        break;
                    case Direction.Left:
                        jumpLandX--;
                        break;
                    case Direction.Right:
                        jumpLandX++;
                        break;
                }

                // Check if landing position is valid
                if (!CollisionSystem.IsPositionWalkable(world, jumpLandX, jumpLandY, Direction.None))
                {
                    return; // Can't jump if landing is blocked
                }

                // Perform the jump (2 tiles in jump direction)
                var jumpStart = new Vector2(position.PixelX, position.PixelY);
                var jumpEnd = new Vector2(jumpLandX * TileSize, jumpLandY * TileSize);
                movement.StartMovement(jumpStart, jumpEnd);
                return;
            }
            else
            {
                // Block all other directions
                return;
            }
        }

        // Check collision with directional blocking (for Pokemon ledges)
        // Pass the movement direction to check if ledges block this move
        if (!CollisionSystem.IsPositionWalkable(world, targetX, targetY, direction))
        {
            return; // Position is blocked from this direction, don't start movement
        }

        // Start the grid movement
        var startPixels = new Vector2(position.PixelX, position.PixelY);
        var targetPixels = new Vector2(targetX * TileSize, targetY * TileSize);
        movement.StartMovement(startPixels, targetPixels);
    }
}
