using System.Reflection;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Entities;

/// <summary>
///     Factory for creating a DebugComponentRegistry with auto-discovered components.
///     Scans assemblies for struct types in PokeSharp.Game.Components namespaces.
/// </summary>
public static class DebugComponentRegistryFactory
{
    /// <summary>
    ///     Creates a DebugComponentRegistry with auto-discovered components.
    ///     Scans loaded assemblies for component types and registers them automatically.
    /// </summary>
    public static DebugComponentRegistry CreateDefault(ILogger? logger = null)
    {
        var registry = new DebugComponentRegistry();

        // Auto-discover all component types from loaded assemblies
        AutoDiscoverComponents(registry, logger);

        return registry;
    }

    /// <summary>
    ///     Auto-discovers component types from assemblies matching PokeSharp.Game.Components namespace.
    /// </summary>
    private static void AutoDiscoverComponents(DebugComponentRegistry registry, ILogger? logger)
    {
        int registered = 0;

        // Get all loaded assemblies
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                // Skip non-PokeSharp assemblies for performance
                string? assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !assemblyName.StartsWith("PokeSharp"))
                {
                    continue;
                }

                foreach (Type type in assembly.GetTypes())
                {
                    // Only register structs in component namespaces
                    if (!type.IsValueType || type.IsEnum || type.IsPrimitive)
                    {
                        continue;
                    }

                    string? ns = type.Namespace;
                    if (ns == null || !IsComponentNamespace(ns))
                    {
                        continue;
                    }

                    // Register this component type
                    string category = DetermineCategoryFromNamespace(ns);
                    int priority = DeterminePriorityFromType(type);

                    registry.RegisterDynamic(type, type.Name, category, priority);
                    registered++;
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies may have unloadable types - skip them
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error scanning assembly {Assembly} for components", assembly.GetName().Name);
            }
        }

        logger?.LogDebug("Auto-discovered {Count} component types for debug registry", registered);
    }

    /// <summary>
    ///     Checks if a namespace contains component types.
    /// </summary>
    private static bool IsComponentNamespace(string ns)
    {
        return ns.StartsWith("PokeSharp.Game.Components") ||
               ns.StartsWith("PokeSharp.Engine.Core.Types");
    }

    /// <summary>
    ///     Determines the debug category from a namespace.
    /// </summary>
    private static string DetermineCategoryFromNamespace(string ns)
    {
        // Extract the last part of the namespace as category
        // e.g., "PokeSharp.Game.Components.Maps" -> "Maps"
        // e.g., "PokeSharp.Game.Components.Movement" -> "Movement"
        string[] parts = ns.Split('.');
        if (parts.Length > 3 && parts[^1] != "Components")
        {
            return parts[^1];
        }

        return "General";
    }

    /// <summary>
    ///     Determines priority for entity naming based on component type.
    ///     Higher priority = used for naming first.
    /// </summary>
    private static int DeterminePriorityFromType(Type type)
    {
        // High priority for entity-identifying components
        return type.Name switch
        {
            "Player" => 100,
            "Npc" => 90,
            "MapInfo" => 80,
            "WarpPoint" => 75,
            "MapWarps" => 70,
            "TileSprite" => 50,
            "AnimatedTile" => 45,
            "Sprite" => 20,
            "Animation" => 15,
            "Position" => 10,
            "TilePosition" => 9,
            "GridMovement" => 8,
            "Elevation" => 5,
            "BelongsToMap" => 5,
            "Collision" => 4,
            "Pooled" => 1,
            _ => 0
        };
    }
}
