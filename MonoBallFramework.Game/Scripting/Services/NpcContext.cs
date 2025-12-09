using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Fluent context implementation for chaining NPC operations.
/// </summary>
internal sealed class NpcContext : INpcContext
{
    private readonly NpcApiService _npcService;

    public NpcContext(Entity entity, NpcApiService npcService)
    {
        Entity = entity;
        _npcService = npcService ?? throw new ArgumentNullException(nameof(npcService));
    }

    /// <inheritdoc />
    public Entity Entity { get; }

    #region Movement

    /// <inheritdoc />
    public INpcContext Move(Direction direction)
    {
        _npcService.MoveNpc(Entity, direction);
        return this;
    }

    /// <inheritdoc />
    public INpcContext Face(Direction direction)
    {
        _npcService.FaceDirection(Entity, direction);
        return this;
    }

    /// <inheritdoc />
    public INpcContext FaceEntity(Entity target)
    {
        _npcService.FaceEntity(Entity, target);
        return this;
    }

    /// <inheritdoc />
    public INpcContext Stop()
    {
        _npcService.StopNpc(Entity);
        return this;
    }

    #endregion

    #region Path/Patrol

    /// <inheritdoc />
    public INpcContext SetPath(Point[] waypoints, bool loop = false)
    {
        _npcService.SetNpcPath(Entity, waypoints, loop);
        return this;
    }

    /// <inheritdoc />
    public INpcContext ClearPath()
    {
        _npcService.ClearNpcPath(Entity);
        return this;
    }

    /// <inheritdoc />
    public INpcContext PausePath()
    {
        _npcService.PauseNpcPath(Entity);
        return this;
    }

    /// <inheritdoc />
    public INpcContext ResumePath(float waitTime = 0f)
    {
        _npcService.ResumeNpcPath(Entity, waitTime);
        return this;
    }

    #endregion

    #region Appearance

    /// <inheritdoc />
    public INpcContext SetSprite(GameSpriteId spriteId)
    {
        _npcService.SetNpcSprite(Entity, spriteId);
        return this;
    }

    /// <inheritdoc />
    public INpcContext SetVisible(bool visible)
    {
        _npcService.SetNpcVisible(Entity, visible);
        return this;
    }

    /// <inheritdoc />
    public INpcContext Show()
    {
        return SetVisible(true);
    }

    /// <inheritdoc />
    public INpcContext Hide()
    {
        return SetVisible(false);
    }

    #endregion

    #region Behavior

    /// <inheritdoc />
    public INpcContext SetBehavior(GameBehaviorId behaviorId)
    {
        _npcService.SetNpcBehavior(Entity, behaviorId);
        return this;
    }

    /// <inheritdoc />
    public INpcContext ActivateBehavior()
    {
        _npcService.ActivateBehavior(Entity);
        return this;
    }

    /// <inheritdoc />
    public INpcContext DeactivateBehavior()
    {
        _npcService.DeactivateBehavior(Entity);
        return this;
    }

    #endregion

    #region Identity

    /// <inheritdoc />
    public INpcContext SetDisplayName(string displayName)
    {
        _npcService.SetNpcDisplayName(Entity, displayName);
        return this;
    }

    #endregion
}
