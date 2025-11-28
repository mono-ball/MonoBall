using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Input.Components;
using PokeSharp.Engine.Input.Services;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;

namespace PokeSharp.Engine.Input.Systems;

/// <summary>
///     System that processes keyboard and gamepad input and converts it to movement requests.
///     Implements Pokemon-style grid-locked input with queue-based buffering for responsive controls.
///     Movement validation and collision checking happens in MovementSystem.
///     Input blocking is handled via IInputBlocker (e.g., when console has ExclusiveInput=true).
/// </summary>
public class InputSystem(
    int maxBufferSize = 5,
    float bufferTimeout = 0.2f,
    ILogger<InputSystem>? logger = null,
    IInputBlocker? inputBlocker = null
) : SystemBase, IUpdateSystem
{
    private readonly InputBuffer _inputBuffer = new(maxBufferSize, bufferTimeout);
    private readonly ILogger<InputSystem>? _logger = logger;
    private readonly IInputBlocker? _inputBlocker = inputBlocker;

    // Cache query description to avoid allocation every frame
    private readonly QueryDescription _playerQuery = QueryCache.Get<
        Player,
        Position,
        GridMovement,
        InputState,
        Direction
    >();

    private GamePadState _gamepadState;
    private int _inputEventsProcessed;

    // Cache input states to avoid redundant polling
    private KeyboardState _keyboardState;
    private Direction _lastBufferedDirection = Direction.None;
    private float _lastBufferTime = -1f;
    private KeyboardState _prevKeyboardState;
    private float _totalTime;

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Input executes at priority 0, before all other update systems.
    /// </summary>
    public int UpdatePriority => SystemPriority.Input;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Input;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        _totalTime += deltaTime;

        // Check if input is blocked by a higher-priority scene (e.g., console with ExclusiveInput=true)
        if (_inputBlocker?.IsInputBlocked == true)
        {
            // Still update previous state to avoid stale key detection when input unblocks
            _prevKeyboardState = _keyboardState;
            return;
        }

        // Poll input states once per frame (not per entity)
        _prevKeyboardState = _keyboardState;
        _keyboardState = Keyboard.GetState();
        _gamepadState = GamePad.GetState(PlayerIndex.One);

        // Process input for all players (cached query, cached input states)
        world.Query(
            in _playerQuery,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref InputState input
            ) =>
            {
                if (!input.InputEnabled)
                    return;

                // Get current input direction (uses cached input states)
                var currentDirection = GetInputDirection(_keyboardState, _gamepadState);

                // Update pressed direction if input detected
                if (currentDirection != Direction.None)
                {
                    input.PressedDirection = currentDirection;

                    // Synchronize Direction component with input direction
                    ref var direction = ref entity.Get<Direction>();
                    direction = currentDirection;

                    // Buffer input if:
                    // 1. Not currently moving (allows holding keys for continuous movement), OR
                    // 2. Direction changed (allows queuing direction changes during movement)
                    // But only if we haven't buffered this exact direction very recently (prevents duplicates)
                    var shouldBuffer =
                        !movement.IsMoving || currentDirection != _lastBufferedDirection;

                    // Also prevent buffering the same direction multiple times per frame
                    var isDifferentTiming =
                        _totalTime != _lastBufferTime || currentDirection != _lastBufferedDirection;

                    if (shouldBuffer && isDifferentTiming)
                        if (_inputBuffer.AddInput(currentDirection, _totalTime))
                        {
                            _lastBufferedDirection = currentDirection;
                            _lastBufferTime = _totalTime;
                            _logger?.LogTrace(
                                "Buffered input direction: {Direction}",
                                currentDirection
                            );
                        }
                }

                // Check for action button (uses cached input states)
                input.ActionPressed =
                    _keyboardState.IsKeyDown(Keys.Space)
                    || _keyboardState.IsKeyDown(Keys.Enter)
                    || _keyboardState.IsKeyDown(Keys.Z)
                    || _gamepadState.Buttons.A == ButtonState.Pressed;

                // Try to consume buffered input if not currently moving
                // Check if there's buffered input and no active movement request
                if (
                    !movement.IsMoving
                    && _inputBuffer.TryConsumeInput(_totalTime, out var bufferedDirection)
                )
                {
                    // Use component pooling: reuse existing component or add new one
                    if (entity.Has<MovementRequest>())
                    {
                        ref var request = ref entity.Get<MovementRequest>();
                        if (!request.Active)
                        {
                            request.Direction = bufferedDirection;
                            request.Active = true;
                            _inputEventsProcessed++;
                            _logger?.LogTrace(
                                "Consumed buffered input: {Direction}",
                                bufferedDirection
                            );
                            _lastBufferedDirection = Direction.None;
                        }
                    }
                    else
                    {
                        world.Add(entity, new MovementRequest(bufferedDirection));
                        _inputEventsProcessed++;
                        _logger?.LogTrace(
                            "Consumed buffered input: {Direction}",
                            bufferedDirection
                        );
                        _lastBufferedDirection = Direction.None;
                    }
                }
            }
        );
    }

    private static Direction GetInputDirection(KeyboardState keyboard, GamePadState gamepad)
    {
        // Keyboard input (priority: most recently pressed)
        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W))
            return Direction.North;
        if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S))
            return Direction.South;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            return Direction.West;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            return Direction.East;

        // Gamepad input
        var thumbstick = gamepad.ThumbSticks.Left;
        if (thumbstick.Y > 0.5f || gamepad.DPad.Up == ButtonState.Pressed)
            return Direction.North;
        if (thumbstick.Y < -0.5f || gamepad.DPad.Down == ButtonState.Pressed)
            return Direction.South;
        if (thumbstick.X < -0.5f || gamepad.DPad.Left == ButtonState.Pressed)
            return Direction.West;
        if (thumbstick.X > 0.5f || gamepad.DPad.Right == ButtonState.Pressed)
            return Direction.East;

        return Direction.None;
    }
}
