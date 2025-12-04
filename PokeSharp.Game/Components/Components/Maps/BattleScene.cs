namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Battle scene/background for wild encounters (e.g., "MAP_BATTLE_SCENE_NORMAL").
/// </summary>
public struct BattleScene
{
    public string Value { get; set; }

    public BattleScene(string value)
    {
        Value = value;
    }
}
