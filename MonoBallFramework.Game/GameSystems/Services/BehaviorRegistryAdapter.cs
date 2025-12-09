using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameSystems.Services;

/// <summary>
///     Adapter that wraps TypeRegistry&lt;BehaviorDefinition&gt; to implement IBehaviorRegistry.
///     Provides a clean interface for behavior lookup without exposing the generic TypeRegistry.
/// </summary>
public class BehaviorRegistryAdapter(TypeRegistry<BehaviorDefinition> typeRegistry) : IBehaviorRegistry
{
    private readonly TypeRegistry<BehaviorDefinition> _typeRegistry =
        typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));

    /// <inheritdoc />
    public void RegisterBehavior(GameBehaviorId behaviorId, BehaviorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(behaviorId);
        ArgumentNullException.ThrowIfNull(definition);

        _typeRegistry.Register(definition);
    }

    /// <inheritdoc />
    public BehaviorDefinition? GetBehavior(GameBehaviorId behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            return null;

        return _typeRegistry.Get(behaviorId);
    }

    /// <inheritdoc />
    public bool HasBehavior(GameBehaviorId behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            return false;

        return _typeRegistry.Contains(behaviorId);
    }

    /// <inheritdoc />
    public bool RemoveBehavior(GameBehaviorId behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            return false;

        return _typeRegistry.Remove(behaviorId);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllBehaviorIds()
    {
        return _typeRegistry.GetAllTypeIds();
    }
}
