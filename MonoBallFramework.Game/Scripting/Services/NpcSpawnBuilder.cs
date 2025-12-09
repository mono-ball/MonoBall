using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.GameState;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Fluent builder implementation for spawning NPCs.
/// </summary>
internal sealed class NpcSpawnBuilder : INpcSpawnBuilder
{
    private static int _dynamicIdCounter;

    private readonly World _world;
    private readonly NpcApiService? _npcService;
    private readonly ILogger? _logger;
    private readonly int _x;
    private readonly int _y;

    // Configuration
    private GameNpcId? _npcId;
    private GameSpriteId? _spriteId;
    private GameBehaviorId? _behaviorId;
    private string? _displayName;
    private bool _visible = true;
    private Direction _facing = Direction.South;
    private Point[]? _pathWaypoints;
    private bool _pathLoop;
    private GameFlagId? _visibilityFlagId;
    private bool _hideWhenFlagTrue = true;
    private bool _isTrainer;
    private int _viewRange;
    private bool _isDefeated;
    private int? _rangeX;
    private int? _rangeY;
    private float _movementSpeed = 3.75f;
    private byte _elevation = Elevation.Default;
    private string? _animationName;
    private bool _isSolid = true;
    private bool _hasInteraction;
    private int _interactionRange = 1;
    private bool _requiresFacing = true;
    private bool _interactionEnabled = true;
    private string? _dialogueScript;
    private string? _interactionEvent;
    private GameMapId? _mapId;
    private int _tileSize = 16;
    private Entity? _parentEntity;

    public NpcSpawnBuilder(
        World world,
        NpcApiService npcService,
        ILogger logger,
        int x,
        int y)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _npcService = npcService ?? throw new ArgumentNullException(nameof(npcService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _x = x;
        _y = y;
    }

    /// <summary>
    ///     Creates a builder for map loading context (without full scripting services).
    /// </summary>
    internal NpcSpawnBuilder(World world, int x, int y, ILogger? logger = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _npcService = null;
        _logger = logger;
        _x = x;
        _y = y;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder FromDefinition(GameNpcId npcId)
    {
        _npcId = npcId ?? throw new ArgumentNullException(nameof(npcId));
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithSprite(GameSpriteId spriteId)
    {
        _spriteId = spriteId ?? throw new ArgumentNullException(nameof(spriteId));
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithBehavior(GameBehaviorId behaviorId)
    {
        _behaviorId = behaviorId ?? throw new ArgumentNullException(nameof(behaviorId));
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithVisibilityFlag(GameFlagId flagId, bool hideWhenTrue = true)
    {
        _visibilityFlagId = flagId ?? throw new ArgumentNullException(nameof(flagId));
        _hideWhenFlagTrue = hideWhenTrue;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder HideWhenFlagSet(GameFlagId flagId)
    {
        return WithVisibilityFlag(flagId, hideWhenTrue: true);
    }

    /// <inheritdoc />
    public INpcSpawnBuilder ShowWhenFlagSet(GameFlagId flagId)
    {
        return WithVisibilityFlag(flagId, hideWhenTrue: false);
    }

    /// <inheritdoc />
    public INpcSpawnBuilder Facing(Direction direction)
    {
        _facing = direction;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithPath(Point[] waypoints, bool loop = false)
    {
        _pathWaypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
        _pathLoop = loop;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithAnimation(string animationName)
    {
        _animationName = animationName ?? throw new ArgumentNullException(nameof(animationName));
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithCollision(bool isSolid = true)
    {
        _isSolid = isSolid;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder Solid()
    {
        _isSolid = true;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder NonSolid()
    {
        _isSolid = false;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder AsTrainer(int viewRange = 5)
    {
        _isTrainer = true;
        _viewRange = viewRange;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithViewRange(int tiles)
    {
        _viewRange = tiles;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder AlreadyDefeated()
    {
        _isDefeated = true;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithMovementRange(int rangeX, int rangeY)
    {
        _rangeX = rangeX;
        _rangeY = rangeY;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithMovementSpeed(float tilesPerSecond)
    {
        _movementSpeed = tilesPerSecond;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder AtElevation(byte elevation)
    {
        _elevation = elevation;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithInteraction(int range = 1, bool requiresFacing = true)
    {
        _hasInteraction = true;
        _interactionRange = range;
        _requiresFacing = requiresFacing;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithDialogue(string scriptPath)
    {
        _dialogueScript = scriptPath ?? throw new ArgumentNullException(nameof(scriptPath));
        _hasInteraction = true;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder OnInteract(string eventName)
    {
        _interactionEvent = eventName ?? throw new ArgumentNullException(nameof(eventName));
        _hasInteraction = true;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder Interactable(bool enabled = true)
    {
        _interactionEnabled = enabled;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder OnMap(GameMapId mapId, int tileSize = 16)
    {
        _mapId = mapId ?? throw new ArgumentNullException(nameof(mapId));
        _tileSize = tileSize;
        return this;
    }

    /// <inheritdoc />
    public INpcSpawnBuilder WithParent(Entity parentEntity)
    {
        _parentEntity = parentEntity;
        return this;
    }

    /// <inheritdoc />
    public Entity Spawn()
    {
        // Create the entity with base components
        var entity = _world.Create(
            new Position(_x, _y, _mapId, _tileSize),
            _facing,
            new GridMovement(_movementSpeed) { FacingDirection = _facing }
        );

        // Add NPC component - always generate an ID if not provided
        var npcId = _npcId ?? GameNpcId.Create($"spawned_{Interlocked.Increment(ref _dynamicIdCounter)}", "dynamic");
        var npcComponent = new Npc(npcId)
        {
            IsTrainer = _isTrainer,
            IsDefeated = _isDefeated,
            ViewRange = _viewRange
        };
        _world.Add(entity, npcComponent);

        // Add sprite if specified
        if (_spriteId != null)
        {
            _world.Add(entity, new Sprite(_spriteId));
        }

        // Add behavior if specified
        if (_behaviorId != null)
        {
            _world.Add(entity, new Behavior(_behaviorId.Value));
        }

        // Add name if specified
        if (!string.IsNullOrEmpty(_displayName))
        {
            _world.Add(entity, new Name(_displayName));
        }

        // Add visibility
        if (_visible)
        {
            _world.Add<Visible>(entity);
        }

        // Add visibility flag if specified
        if (_visibilityFlagId != null)
        {
            _world.Add(entity, new VisibilityFlag(_visibilityFlagId, _hideWhenFlagTrue));
        }

        // Add elevation for rendering (default elevation = 3, standard ground level)
        _world.Add(entity, new Elevation(_elevation));

        // Add movement range if specified
        if (_rangeX.HasValue && _rangeY.HasValue)
        {
            _world.Add(entity, new MovementRange(_rangeX.Value, _rangeY.Value, _x, _y));
        }

        // Add animation - default to direction-based if not specified
        var animationName = _animationName ?? GetDefaultAnimationForDirection(_facing);
        _world.Add(entity, new Animation(animationName));

        // Add collision component
        _world.Add(entity, new Collision(_isSolid));

        // Add interaction if configured
        if (_hasInteraction || _dialogueScript != null || _interactionEvent != null)
        {
            _world.Add(entity, new Interaction
            {
                InteractionRange = _interactionRange,
                RequiresFacing = _requiresFacing,
                IsEnabled = _interactionEnabled,
                DialogueScript = _dialogueScript,
                InteractionEvent = _interactionEvent
            });
        }

        // Set path if specified
        if (_pathWaypoints != null && _pathWaypoints.Length > 0)
        {
            if (_npcService != null)
            {
                _npcService.SetNpcPath(entity, _pathWaypoints, _pathLoop);
            }
            else
            {
                // Direct component addition when NpcApiService is not available (map loading context)
                _world.Add(entity, new MovementRoute(_pathWaypoints, _pathLoop));
            }
        }

        // Add parent relationship if specified
        if (_parentEntity.HasValue)
        {
            _parentEntity.Value.AddRelationship(entity, new ParentOf());
        }

        _logger?.LogDebug(
            "Spawned NPC at ({X}, {Y}) with sprite={Sprite}, behavior={Behavior}, name={Name}",
            _x, _y,
            _spriteId?.Value ?? "none",
            _behaviorId?.Value ?? "none",
            _displayName ?? "none"
        );

        return entity;
    }

    /// <inheritdoc />
    public INpcContext SpawnAndConfigure()
    {
        if (_npcService == null)
        {
            throw new InvalidOperationException(
                "SpawnAndConfigure() requires NpcApiService. Use the full constructor or call Spawn() instead.");
        }

        var entity = Spawn();
        return _npcService.For(entity);
    }

    /// <summary>
    ///     Get the default animation name based on facing direction.
    /// </summary>
    private static string GetDefaultAnimationForDirection(Direction direction)
    {
        return direction switch
        {
            Direction.North => "face_north",
            Direction.South => "face_south",
            Direction.East => "face_east",
            Direction.West => "face_west",
            _ => "face_south"
        };
    }

}
