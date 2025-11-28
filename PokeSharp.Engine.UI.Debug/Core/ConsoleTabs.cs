using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
/// Console tab definitions.
/// Centralizes tab metadata to avoid magic numbers scattered across the codebase.
/// </summary>
public static class ConsoleTabs
{
    /// <summary>
    /// Represents a console tab with its index, name, aliases, and keyboard shortcut.
    /// </summary>
    public sealed class TabDefinition
    {
        public int Index { get; }
        public string Name { get; }
        public string[] Aliases { get; }
        public Keys? Shortcut { get; }

        public TabDefinition(int index, string name, string[] aliases, Keys? shortcut = null)
        {
            Index = index;
            Name = name;
            Aliases = aliases;
            Shortcut = shortcut;
        }

        /// <summary>
        /// Checks if the given input matches this tab (by name, alias, or index).
        /// </summary>
        public bool Matches(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Check if input is the index number
            if (int.TryParse(input, out var idx) && idx == Index)
                return true;

            // Check name (case-insensitive)
            if (Name.Equals(input, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check aliases (case-insensitive)
            return Aliases.Any(a => a.Equals(input, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Tab definitions
    public static readonly TabDefinition Console = new(0, "Console", ["console", "con"], Keys.D1);
    public static readonly TabDefinition Watch = new(1, "Watch", ["watch", "w"], Keys.D2);
    public static readonly TabDefinition Logs = new(2, "Logs", ["logs", "log", "l"], Keys.D3);
    public static readonly TabDefinition Variables = new(3, "Variables", ["variables", "vars", "var", "v"], Keys.D4);
    public static readonly TabDefinition Entities = new(4, "Entities", ["entities", "entity", "e"], Keys.D5);
    public static readonly TabDefinition Profiler = new(5, "Profiler", ["profiler", "perf", "p"], Keys.D6);
    public static readonly TabDefinition Stats = new(6, "Stats", ["stats", "stat", "s", "perf"], Keys.D7);

    /// <summary>
    /// All tab definitions in order.
    /// </summary>
    public static readonly IReadOnlyList<TabDefinition> All = new[]
    {
        Console, Watch, Logs, Variables, Entities, Profiler, Stats
    };

    /// <summary>
    /// Total number of tabs.
    /// </summary>
    public static int Count => All.Count;

    /// <summary>
    /// Gets a tab definition by index.
    /// </summary>
    public static TabDefinition? GetByIndex(int index)
    {
        return index >= 0 && index < All.Count ? All[index] : null;
    }

    /// <summary>
    /// Tries to get a tab by name, alias, or index string.
    /// </summary>
    public static bool TryGet(string input, out TabDefinition? tab)
    {
        tab = All.FirstOrDefault(t => t.Matches(input));
        return tab != null;
    }

    /// <summary>
    /// Gets a tab by keyboard shortcut key.
    /// </summary>
    public static TabDefinition? GetByShortcut(Keys key)
    {
        return All.FirstOrDefault(t => t.Shortcut == key);
    }

    /// <summary>
    /// Gets all valid aliases for autocomplete/help.
    /// </summary>
    public static IEnumerable<string> GetAllAliases()
    {
        foreach (var tab in All)
        {
            yield return tab.Name.ToLowerInvariant();
            foreach (var alias in tab.Aliases)
            {
                yield return alias;
            }
            yield return tab.Index.ToString();
        }
    }
}

