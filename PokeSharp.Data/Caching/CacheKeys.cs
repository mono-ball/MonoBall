namespace PokeSharp.Data.Caching;

/// <summary>
/// Strongly-typed cache keys for game data.
/// Using constants prevents typos and makes cache key management easier.
/// Format: "{category}:{identifier}"
/// </summary>
public static class CacheKeys
{
    // Species cache keys
    public static string Species(int id) => $"species:{id}";
    public static string SpeciesByName(string name) => $"species:name:{name.ToLowerInvariant()}";
    public static string AllSpecies => "species:all";

    // Move cache keys
    public static string Move(int id) => $"move:{id}";
    public static string MoveByName(string name) => $"move:name:{name.ToLowerInvariant()}";
    public static string AllMoves => "moves:all";
    public static string MovesByType(string typeId) => $"moves:type:{typeId}";
    public static string MovesByDamageClass(string damageClass) => $"moves:damageclass:{damageClass}";

    // Item cache keys
    public static string Item(int id) => $"item:{id}";
    public static string ItemByName(string name) => $"item:name:{name.ToLowerInvariant()}";
    public static string AllItems => "items:all";
    public static string ItemsByCategory(string category) => $"items:category:{category}";

    // Type effectiveness cache keys
    public static string TypeEffectiveness => "types:effectiveness";
    public static string TypeById(int id) => $"type:{id}";

    // Encounter cache keys
    public static string EncounterTable(string tableId) => $"encounter:table:{tableId}";
    public static string EncountersByMap(string mapId) => $"encounters:map:{mapId}";

    // Status condition cache keys
    public static string StatusCondition(int id) => $"status:{id}";
    public static string AllStatusConditions => "status:all";

    // Ability cache keys
    public static string Ability(int id) => $"ability:{id}";
    public static string AllAbilities => "abilities:all";

    // Evolution cache keys
    public static string EvolutionChain(int speciesId) => $"evolution:chain:{speciesId}";
    public static string EvolutionsBySpecies(int speciesId) => $"evolutions:species:{speciesId}";

    // Template cache keys (from PokeSharp.Core)
    public static string Template(string templateId) => $"template:{templateId}";
    public static string TemplatesByTag(string tag) => $"templates:tag:{tag}";
}
