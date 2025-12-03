using System.Text.RegularExpressions;

namespace PokeSharp.Game.Scripting.Modding;

/// <summary>
///     Resolves mod dependencies and determines load order using topological sort.
///     Handles semantic versioning and circular dependency detection.
/// </summary>
public class ModDependencyResolver
{
    private static readonly Regex DependencyPattern = new(
        @"^(?<id>[\w-]+)\s*(?<operator>>=|==|>|<|<=)?\s*(?<version>[\d\.]+(?:-[\w\.]+)?)$",
        RegexOptions.Compiled
    );

    /// <summary>
    ///     Resolves mod dependencies and returns mods in load order (dependencies first).
    /// </summary>
    /// <param name="manifests">List of mod manifests to resolve.</param>
    /// <returns>Ordered list of manifests ready to load.</returns>
    /// <exception cref="ModDependencyException">Thrown when dependencies cannot be resolved.</exception>
    public List<ModManifest> ResolveDependencies(IEnumerable<ModManifest> manifests)
    {
        var modList = manifests.ToList();
        var modLookup = modList.ToDictionary(m => m.Id, m => m);

        // Validate all dependencies exist and versions match
        ValidateDependencies(modList, modLookup);

        // Build dependency graph
        var dependencyGraph = BuildDependencyGraph(modList, modLookup);

        // Detect circular dependencies
        DetectCircularDependencies(dependencyGraph);

        // Perform topological sort
        return TopologicalSort(modList, dependencyGraph);
    }

    /// <summary>
    ///     Validates that all dependencies exist and version constraints are satisfied.
    /// </summary>
    private void ValidateDependencies(
        List<ModManifest> mods,
        Dictionary<string, ModManifest> modLookup
    )
    {
        foreach (ModManifest mod in mods)
        {
            foreach (string dependency in mod.Dependencies)
            {
                if (!TryParseDependency(dependency, out string? depId, out string? op, out string? version))
                {
                    throw new ModDependencyException(
                        $"Mod '{mod.Id}' has invalid dependency format: '{dependency}'. " +
                        "Expected format: 'mod-id >= version' or 'mod-id == version'"
                    );
                }

                if (depId == null)
                {
                    throw new ModDependencyException(
                        $"Mod '{mod.Id}' has invalid dependency: '{dependency}' - dependency ID is null"
                    );
                }

                if (!modLookup.TryGetValue(depId, out ModManifest? depMod))
                {
                    throw new ModDependencyException(
                        $"Mod '{mod.Id}' depends on '{depId}' which is not installed"
                    );
                }

                // Validate version constraint
                if (!string.IsNullOrEmpty(version) && !ValidateVersionConstraint(depMod.Version, op, version))
                {
                    throw new ModDependencyException(
                        $"Mod '{mod.Id}' requires '{depId} {op} {version}' but found version {depMod.Version}"
                    );
                }
            }
        }
    }

    /// <summary>
    ///     Builds a dependency graph (mod ID -> list of dependency mod IDs).
    /// </summary>
    private Dictionary<string, List<string>> BuildDependencyGraph(
        List<ModManifest> mods,
        Dictionary<string, ModManifest> modLookup
    )
    {
        var graph = new Dictionary<string, List<string>>();

        foreach (ModManifest mod in mods)
        {
            graph[mod.Id] = new List<string>();

            foreach (string dependency in mod.Dependencies)
            {
                if (TryParseDependency(dependency, out string? depId, out _, out _) && depId != null)
                {
                    graph[mod.Id].Add(depId);
                }
            }
        }

        return graph;
    }

    /// <summary>
    ///     Detects circular dependencies using depth-first search.
    /// </summary>
    private void DetectCircularDependencies(Dictionary<string, List<string>> graph)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (string modId in graph.Keys)
        {
            if (HasCircularDependency(modId, graph, visited, recursionStack, out string? cycle))
            {
                throw new ModDependencyException(
                    $"Circular dependency detected: {cycle}"
                );
            }
        }
    }

    /// <summary>
    ///     DFS helper to detect cycles.
    /// </summary>
    private bool HasCircularDependency(
        string modId,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        out string? cycle
    )
    {
        if (recursionStack.Contains(modId))
        {
            cycle = modId;
            return true;
        }

        if (visited.Contains(modId))
        {
            cycle = null;
            return false;
        }

        visited.Add(modId);
        recursionStack.Add(modId);

        if (graph.TryGetValue(modId, out List<string>? dependencies))
        {
            foreach (string depId in dependencies)
            {
                if (HasCircularDependency(depId, graph, visited, recursionStack, out cycle))
                {
                    cycle = $"{depId} -> {cycle}";
                    return true;
                }
            }
        }

        recursionStack.Remove(modId);
        cycle = null;
        return false;
    }

    /// <summary>
    ///     Performs topological sort using Kahn's algorithm.
    ///     Also considers priority for ordering when no dependencies exist.
    /// </summary>
    private List<ModManifest> TopologicalSort(
        List<ModManifest> mods,
        Dictionary<string, List<string>> graph
    )
    {
        var result = new List<ModManifest>();
        var modLookup = mods.ToDictionary(m => m.Id, m => m);
        var inDegree = new Dictionary<string, int>();

        // Calculate in-degrees
        foreach (string modId in graph.Keys)
        {
            inDegree[modId] = 0;
        }

        foreach (List<string> dependencies in graph.Values)
        {
            foreach (string depId in dependencies)
            {
                inDegree[depId]++;
            }
        }

        // Queue nodes with no dependencies (sorted by priority descending)
        var queue = new PriorityQueue<string, int>(
            Comparer<int>.Create((a, b) => b.CompareTo(a)) // Higher priority first
        );

        foreach (string modId in inDegree.Keys.Where(id => inDegree[id] == 0))
        {
            int priority = modLookup[modId].Priority;
            queue.Enqueue(modId, priority);
        }

        // Process queue
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            result.Add(modLookup[current]);

            // Reduce in-degree for dependents
            foreach (string dependent in graph.Keys.Where(k => graph[k].Contains(current)))
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    int priority = modLookup[dependent].Priority;
                    queue.Enqueue(dependent, priority);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Parses a dependency string into components.
    /// </summary>
    private bool TryParseDependency(
        string dependency,
        out string? id,
        out string? op,
        out string? version
    )
    {
        Match match = DependencyPattern.Match(dependency);
        if (match.Success)
        {
            id = match.Groups["id"].Value;
            op = match.Groups["operator"].Success ? match.Groups["operator"].Value : ">=";
            version = match.Groups["version"].Value;
            return true;
        }

        id = null;
        op = null;
        version = null;
        return false;
    }

    /// <summary>
    ///     Validates a version constraint using semantic versioning comparison.
    /// </summary>
    private bool ValidateVersionConstraint(string actualVersion, string? op, string? requiredVersion)
    {
        if (string.IsNullOrEmpty(requiredVersion))
        {
            return true;
        }

        if (!TryParseVersion(actualVersion, out var actual) ||
            !TryParseVersion(requiredVersion, out var required))
        {
            return false;
        }

        int comparison = CompareVersions(actual, required);

        return op switch
        {
            ">=" => comparison >= 0,
            ">" => comparison > 0,
            "==" => comparison == 0,
            "<=" => comparison <= 0,
            "<" => comparison < 0,
            _ => true
        };
    }

    /// <summary>
    ///     Parses a semantic version string into components.
    /// </summary>
    private bool TryParseVersion(string version, out (int major, int minor, int patch, string prerelease) result)
    {
        result = (0, 0, 0, string.Empty);

        var parts = version.Split('-');
        var versionPart = parts[0];
        var prerelease = parts.Length > 1 ? parts[1] : string.Empty;

        var numbers = versionPart.Split('.');
        if (numbers.Length < 1 || numbers.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(numbers[0], out int major))
        {
            return false;
        }

        int minor = numbers.Length > 1 && int.TryParse(numbers[1], out int m) ? m : 0;
        int patch = numbers.Length > 2 && int.TryParse(numbers[2], out int p) ? p : 0;

        result = (major, minor, patch, prerelease);
        return true;
    }

    /// <summary>
    ///     Compares two semantic versions.
    /// </summary>
    private int CompareVersions(
        (int major, int minor, int patch, string prerelease) v1,
        (int major, int minor, int patch, string prerelease) v2
    )
    {
        if (v1.major != v2.major) return v1.major.CompareTo(v2.major);
        if (v1.minor != v2.minor) return v1.minor.CompareTo(v2.minor);
        if (v1.patch != v2.patch) return v1.patch.CompareTo(v2.patch);

        // Prerelease comparison (empty string = stable, comes after prerelease)
        if (string.IsNullOrEmpty(v1.prerelease) && !string.IsNullOrEmpty(v2.prerelease))
        {
            return 1;
        }
        if (!string.IsNullOrEmpty(v1.prerelease) && string.IsNullOrEmpty(v2.prerelease))
        {
            return -1;
        }

        return string.Compare(v1.prerelease, v2.prerelease, StringComparison.Ordinal);
    }
}

/// <summary>
///     Exception thrown when mod dependencies cannot be resolved.
/// </summary>
public class ModDependencyException : Exception
{
    public ModDependencyException(string message) : base(message) { }
    public ModDependencyException(string message, Exception innerException) : base(message, innerException) { }
}
