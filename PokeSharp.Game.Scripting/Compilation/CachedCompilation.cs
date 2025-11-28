namespace PokeSharp.Game.Scripting.Compilation;

/// <summary>
///     Cached compilation entry with content hash and timestamp.
/// </summary>
internal class CachedCompilation
{
    public required Type CompiledType { get; init; }
    public required string ContentHash { get; init; }
    public DateTime CompiledAt { get; init; }
}
