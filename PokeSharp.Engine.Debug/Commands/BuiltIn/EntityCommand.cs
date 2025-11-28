using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Models;
using TextCopy;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command for browsing and managing ECS entities.
/// </summary>
[ConsoleCommand("entity", "Browse and inspect ECS entities")]
public class EntityCommand : IConsoleCommand
{
    public string Name => "entity";
    public string Description => "Browse and inspect ECS entities";

    public string Usage =>
        @"entity <subcommand>

Subcommands:
  entity list              - List all entities (respects current filters)
  entity count             - Show entity count and statistics
  entity find <text>       - Search entities by name or ID
  entity inspect <id>      - Show detailed info for an entity
  entity filter <type> <value> - Set a filter
  entity clear             - Clear all filters
  entity refresh           - Refresh the entity list
  entity tags              - List all unique tags with counts
  entity components        - List all unique component names
  entity expand <id>       - Expand an entity to show components
  entity collapse <id>     - Collapse an entity
  entity expand-all        - Expand all entities
  entity collapse-all      - Collapse all entities
  entity pin <id>          - Pin an entity to the top
  entity unpin <id>        - Unpin an entity
  entity auto [on|off]     - Toggle or set auto-refresh
  entity interval <sec>    - Set refresh interval in seconds
  entity session           - Show session spawn/remove stats
  entity session clear     - Clear session stats
  entity copy              - Copy entity list to clipboard
  entity copy csv          - Copy as CSV format
  entity copy selected     - Copy selected entity info
  entity export            - Print entity list to console

Filter types:
  entity filter tag <tag>       - Filter by tag (e.g., Player, NPC, Tile)
  entity filter search <text>   - Filter by name or ID
  entity filter component <name> - Filter by component name

Keyboard Shortcuts (in Entities tab):
  Ctrl+5           Switch to Entities tab
  Up/Down          Navigate entity list
  Enter            Expand/collapse selected entity
  P                Pin/unpin selected entity
  Home/End         Jump to first/last entity
  PageUp/PageDown  Move selection by 10 items

Examples:
  entity list               - Show all entities
  entity filter tag Player  - Show only Player entities
  entity auto off           - Disable auto-refresh
  entity interval 0.5       - Set refresh to every 0.5 seconds
  entity session            - Show how many entities spawned/removed";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length == 0)
        {
            // Show usage
            context.WriteLine(Usage);
            return Task.CompletedTask;
        }

        string subcommand = args[0].ToLowerInvariant();

        switch (subcommand)
        {
            case "list":
                ListEntities(context, theme);
                break;

            case "count":
                ShowEntityCount(context, theme);
                break;

            case "find":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: entity find <text>", theme.Error);
                    return Task.CompletedTask;
                }

                FindEntities(context, theme, string.Join(" ", args.Skip(1)));
                break;

            case "inspect":
                if (args.Length < 2 || !int.TryParse(args[1], out int inspectId))
                {
                    context.WriteLine("Usage: entity inspect <id>", theme.Error);
                    return Task.CompletedTask;
                }

                InspectEntity(context, theme, inspectId);
                break;

            case "filter":
                HandleFilter(context, theme, args.Skip(1).ToArray());
                break;

            case "clear":
                context.Entities.ClearFilters();
                context.WriteLine("Entity filters cleared", theme.Success);
                break;

            case "refresh":
                context.Entities.Refresh();
                context.WriteLine("Entity list refreshed", theme.Success);
                break;

            case "tags":
                ListTags(context, theme);
                break;

            case "components":
                ListComponents(context, theme);
                break;

            case "expand":
                HandleExpand(context, theme, args.Skip(1).ToArray());
                break;

            case "collapse":
                HandleCollapse(context, theme, args.Skip(1).ToArray());
                break;

            case "expand-all":
                context.Entities.ExpandAll();
                context.WriteLine("All entities expanded", theme.Success);
                break;

            case "collapse-all":
                context.Entities.CollapseAll();
                context.WriteLine("All entities collapsed", theme.Success);
                break;

            case "pin":
                HandlePin(context, theme, args.Skip(1).ToArray());
                break;

            case "unpin":
                HandleUnpin(context, theme, args.Skip(1).ToArray());
                break;

            case "auto":
                HandleAutoRefresh(context, theme, args.Skip(1).ToArray());
                break;

            case "interval":
                HandleInterval(context, theme, args.Skip(1).ToArray());
                break;

            case "session":
                HandleSession(context, theme, args.Skip(1).ToArray());
                break;

            case "highlight":
                HandleHighlight(context, theme, args.Skip(1).ToArray());
                break;

            case "copy":
                HandleCopy(context, theme, args.Skip(1).ToArray());
                break;

            case "export":
                HandleExport(context, theme, args.Skip(1).ToArray());
                break;

            default:
                context.WriteLine($"Unknown subcommand: {subcommand}", theme.Error);
                context.WriteLine("Use 'entity' for usage information", theme.TextDim);
                break;
        }

        return Task.CompletedTask;
    }

    private void ListEntities(IConsoleContext context, UITheme theme)
    {
        (int Total, int Filtered, int Pinned, int Expanded) stats =
            context.Entities.GetStatistics();

        context.WriteLine($"Entities: {stats.Filtered} shown", theme.Info);

        if (stats.Total != stats.Filtered)
        {
            context.WriteLine(
                $"  ({stats.Total} total, filtered to {stats.Filtered})",
                theme.TextSecondary
            );
        }

        (string Tag, string Search, string Component) filters = context.Entities.GetFilters();
        if (
            !string.IsNullOrEmpty(filters.Tag)
            || !string.IsNullOrEmpty(filters.Search)
            || !string.IsNullOrEmpty(filters.Component)
        )
        {
            context.WriteLine("Active filters:", theme.TextSecondary);
            if (!string.IsNullOrEmpty(filters.Tag))
            {
                context.WriteLine($"  Tag: {filters.Tag}", theme.TextSecondary);
            }

            if (!string.IsNullOrEmpty(filters.Search))
            {
                context.WriteLine($"  Search: {filters.Search}", theme.TextSecondary);
            }

            if (!string.IsNullOrEmpty(filters.Component))
            {
                context.WriteLine($"  Component: {filters.Component}", theme.TextSecondary);
            }
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine("Use 'tab entities' to view the Entities tab", theme.TextDim);
    }

    private void ShowEntityCount(IConsoleContext context, UITheme theme)
    {
        (int Total, int Filtered, int Pinned, int Expanded) stats =
            context.Entities.GetStatistics();

        context.WriteLine("Entity Statistics:", theme.Info);
        context.WriteLine($"  Total: {stats.Total}", theme.TextPrimary);
        context.WriteLine($"  Filtered: {stats.Filtered}", theme.TextPrimary);
        context.WriteLine($"  Pinned: {stats.Pinned}", theme.TextPrimary);
        context.WriteLine($"  Expanded: {stats.Expanded}", theme.TextPrimary);

        // Show tag breakdown
        Dictionary<string, int> tagCounts = context.Entities.GetTagCounts();
        if (tagCounts.Count > 0)
        {
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("By Tag:", theme.Info);
            foreach (
                (string tag, int count) in tagCounts.OrderByDescending(kv => kv.Value).Take(10)
            )
            {
                context.WriteLine($"  {tag}: {count}", theme.TextSecondary);
            }

            if (tagCounts.Count > 10)
            {
                context.WriteLine($"  ... and {tagCounts.Count - 10} more tags", theme.TextDim);
            }
        }
    }

    private void FindEntities(IConsoleContext context, UITheme theme, string searchText)
    {
        var entities = context.Entities.FindByName(searchText).ToList();

        if (entities.Count == 0)
        {
            // Also try parsing as ID
            if (int.TryParse(searchText, out int id))
            {
                EntityInfo? entity = context.Entities.Find(id);
                if (entity != null)
                {
                    entities.Add(entity);
                }
            }
        }

        if (entities.Count == 0)
        {
            context.WriteLine($"No entities found matching '{searchText}'", theme.Warning);
            return;
        }

        context.WriteLine($"Found {entities.Count} entities:", theme.Success);
        foreach (EntityInfo entity in entities.Take(20))
        {
            string status = entity.IsActive ? "" : " (inactive)";
            context.WriteLine(
                $"  [{entity.Id}] {entity.Name}{status} - {entity.Components.Count} components",
                theme.TextPrimary
            );
        }

        if (entities.Count > 20)
        {
            context.WriteLine($"  ... and {entities.Count - 20} more", theme.TextDim);
        }
    }

    private void InspectEntity(IConsoleContext context, UITheme theme, int entityId)
    {
        EntityInfo? entity = context.Entities.Find(entityId);

        if (entity == null)
        {
            context.WriteLine($"Entity {entityId} not found", theme.Error);
            return;
        }

        context.WriteLine($"Entity {entityId}: {entity.Name}", theme.Info);
        context.WriteLine($"  Active: {entity.IsActive}", theme.TextPrimary);
        context.WriteLine($"  Tag: {entity.Tag ?? "(none)"}", theme.TextPrimary);

        if (entity.Properties.Count > 0)
        {
            context.WriteLine("  Properties:", theme.Success);
            foreach ((string key, string value) in entity.Properties)
            {
                context.WriteLine($"    {key}: {value}", theme.TextSecondary);
            }
        }

        context.WriteLine($"  Components ({entity.Components.Count}):", theme.Warning);
        foreach (string component in entity.Components)
        {
            context.WriteLine($"    • {component}", theme.TextSecondary);
        }

        // Also expand it in the panel
        context.Entities.Expand(entityId);
        context.WriteLine("", theme.TextPrimary);
        context.WriteLine("Entity expanded in Entities tab", theme.TextDim);
    }

    private void HandleFilter(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 2)
        {
            // Show current filters
            (string Tag, string Search, string Component) filters = context.Entities.GetFilters();
            context.WriteLine("Current filters:", theme.Info);
            context.WriteLine(
                $"  Tag: {(string.IsNullOrEmpty(filters.Tag) ? "(none)" : filters.Tag)}",
                theme.TextPrimary
            );
            context.WriteLine(
                $"  Search: {(string.IsNullOrEmpty(filters.Search) ? "(none)" : filters.Search)}",
                theme.TextPrimary
            );
            context.WriteLine(
                $"  Component: {(string.IsNullOrEmpty(filters.Component) ? "(none)" : filters.Component)}",
                theme.TextPrimary
            );
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Usage: entity filter <type> <value>", theme.TextDim);
            context.WriteLine("Types: tag, search, component", theme.TextDim);
            return;
        }

        string filterType = args[0].ToLowerInvariant();
        string filterValue = string.Join(" ", args.Skip(1));

        switch (filterType)
        {
            case "tag":
                context.Entities.SetTagFilter(filterValue);
                context.WriteLine($"Tag filter set to: {filterValue}", theme.Success);
                break;

            case "search":
                context.Entities.SetSearchFilter(filterValue);
                context.WriteLine($"Search filter set to: {filterValue}", theme.Success);
                break;

            case "component":
                context.Entities.SetComponentFilter(filterValue);
                context.WriteLine($"Component filter set to: {filterValue}", theme.Success);
                break;

            default:
                context.WriteLine($"Unknown filter type: {filterType}", theme.Error);
                context.WriteLine("Valid types: tag, search, component", theme.TextDim);
                break;
        }

        // Show updated count
        (int Total, int Filtered, int Pinned, int Expanded) stats =
            context.Entities.GetStatistics();
        context.WriteLine(
            $"Showing {stats.Filtered} of {stats.Total} entities",
            theme.TextSecondary
        );
    }

    private void ListTags(IConsoleContext context, UITheme theme)
    {
        Dictionary<string, int> tagCounts = context.Entities.GetTagCounts();

        if (tagCounts.Count == 0)
        {
            context.WriteLine("No entity tags found", theme.Warning);
            return;
        }

        context.WriteLine($"Entity Tags ({tagCounts.Count}):", theme.Info);
        foreach ((string tag, int count) in tagCounts.OrderByDescending(kv => kv.Value))
        {
            context.WriteLine($"  {tag}: {count}", theme.TextPrimary);
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine("Use 'entity filter tag <name>' to filter by tag", theme.TextDim);
    }

    private void ListComponents(IConsoleContext context, UITheme theme)
    {
        var components = context.Entities.GetComponentNames().ToList();

        if (components.Count == 0)
        {
            context.WriteLine("No components found", theme.Warning);
            return;
        }

        context.WriteLine($"Entity Components ({components.Count}):", theme.Info);
        foreach (string component in components)
        {
            context.WriteLine($"  • {component}", theme.TextPrimary);
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine(
            "Use 'entity filter component <name>' to filter by component",
            theme.TextDim
        );
    }

    private void HandleAutoRefresh(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length == 0)
        {
            // Toggle
            context.Entities.AutoRefresh = !context.Entities.AutoRefresh;
            string status = context.Entities.AutoRefresh ? "enabled" : "disabled";
            context.WriteLine($"Auto-refresh {status}", theme.Success);
        }
        else
        {
            string arg = args[0].ToLowerInvariant();
            if (arg == "on" || arg == "true" || arg == "1")
            {
                context.Entities.AutoRefresh = true;
                context.WriteLine("Auto-refresh enabled", theme.Success);
            }
            else if (arg == "off" || arg == "false" || arg == "0")
            {
                context.Entities.AutoRefresh = false;
                context.WriteLine("Auto-refresh disabled", theme.Success);
            }
            else
            {
                context.WriteLine("Usage: entity auto [on|off]", theme.Error);
            }
        }

        context.WriteLine(
            $"  Interval: {context.Entities.RefreshInterval:F1}s",
            theme.TextSecondary
        );
    }

    private void HandleSession(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length > 0 && args[0].ToLowerInvariant() == "clear")
        {
            context.Entities.ClearSessionStats();
            context.WriteLine("Session stats cleared", theme.Success);
            return;
        }

        (int Spawned, int Removed, int CurrentlyHighlighted) stats =
            context.Entities.GetSessionStats();
        (int Total, int Filtered, int Pinned, int Expanded) entityStats =
            context.Entities.GetStatistics();

        context.WriteLine("Entity Session Stats:", theme.Info);
        context.WriteLine($"  Current entities: {entityStats.Total}", theme.TextPrimary);
        context.WriteLine($"  Spawned this session: {stats.Spawned}", theme.Success);
        context.WriteLine($"  Removed this session: {stats.Removed}", theme.Warning);

        if (stats.CurrentlyHighlighted > 0)
        {
            context.WriteLine(
                $"  Currently highlighted: {stats.CurrentlyHighlighted} new entities",
                new Color(100, 255, 100)
            );
            var newIds = context.Entities.GetNewEntityIds().Take(10).ToList();
            if (newIds.Count > 0)
            {
                context.WriteLine($"    IDs: {string.Join(", ", newIds)}", theme.TextSecondary);
                if (stats.CurrentlyHighlighted > 10)
                {
                    context.WriteLine(
                        $"    ... and {stats.CurrentlyHighlighted - 10} more",
                        theme.TextDim
                    );
                }
            }
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine("Use 'entity session clear' to reset stats", theme.TextDim);
    }

    private void HandleCopy(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length > 0)
        {
            string format = args[0].ToLowerInvariant();

            if (format == "csv")
            {
                context.Entities.CopyToClipboard(true);
                (int Total, int Filtered, int Pinned, int Expanded) stats =
                    context.Entities.GetStatistics();
                context.WriteLine(
                    $"Copied {stats.Filtered} entities to clipboard (CSV format)",
                    theme.Success
                );
                return;
            }

            if (format == "selected")
            {
                string? selected = context.Entities.ExportSelected();
                if (selected != null)
                {
                    ClipboardService.SetText(selected);
                    context.WriteLine("Copied selected entity to clipboard", theme.Success);
                }
                else
                {
                    context.WriteLine("No entity selected", theme.Warning);
                }

                return;
            }
        }

        // Default: copy as text
        context.Entities.CopyToClipboard();
        (int Total, int Filtered, int Pinned, int Expanded) entityStats =
            context.Entities.GetStatistics();
        context.WriteLine($"Copied {entityStats.Filtered} entities to clipboard", theme.Success);
    }

    private void HandleExport(IConsoleContext context, UITheme theme, string[] args)
    {
        (int Total, int Filtered, int Pinned, int Expanded) stats =
            context.Entities.GetStatistics();

        if (stats.Total == 0)
        {
            context.WriteLine("No entities to export", theme.Warning);
            return;
        }

        bool includeComponents = true;
        bool includeProperties = true;

        // Parse optional flags
        foreach (string arg in args)
        {
            if (arg == "-nc" || arg == "--no-components")
            {
                includeComponents = false;
            }
            else if (arg == "-np" || arg == "--no-properties")
            {
                includeProperties = false;
            }
        }

        string export = context.Entities.ExportToText(includeComponents, includeProperties);

        // Print to console (limited to avoid flooding)
        string[] lines = export.Split('\n');
        int maxLines = 100;

        if (lines.Length <= maxLines)
        {
            foreach (string line in lines)
            {
                context.WriteLine(line, theme.TextPrimary);
            }
        }
        else
        {
            for (int i = 0; i < maxLines; i++)
            {
                context.WriteLine(lines[i], theme.TextPrimary);
            }

            context.WriteLine($"... ({lines.Length - maxLines} more lines)", theme.TextDim);
            context.WriteLine(
                "Use 'entity copy' to get full export to clipboard",
                theme.TextSecondary
            );
        }
    }

    private void HandleExpand(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int entityId))
        {
            context.WriteLine("Usage: entity expand <id>", theme.Error);
            return;
        }

        context.Entities.Expand(entityId);
        context.WriteLine($"Entity {entityId} expanded", theme.Success);
    }

    private void HandleCollapse(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int entityId))
        {
            context.WriteLine("Usage: entity collapse <id>", theme.Error);
            return;
        }

        context.Entities.Collapse(entityId);
        context.WriteLine($"Entity {entityId} collapsed", theme.Success);
    }

    private void HandlePin(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int entityId))
        {
            context.WriteLine("Usage: entity pin <id>", theme.Error);
            return;
        }

        context.Entities.Pin(entityId);
        context.WriteLine($"Entity {entityId} pinned", theme.Success);
    }

    private void HandleUnpin(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out int entityId))
        {
            context.WriteLine("Usage: entity unpin <id>", theme.Error);
            return;
        }

        context.Entities.Unpin(entityId);
        context.WriteLine($"Entity {entityId} unpinned", theme.Success);
    }

    private void HandleInterval(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float interval))
        {
            context.WriteLine(
                $"Current interval: {context.Entities.RefreshInterval:F1}s",
                theme.Info
            );
            context.WriteLine("Usage: entity interval <seconds>", theme.TextDim);
            return;
        }

        context.Entities.RefreshInterval = interval;
        context.WriteLine($"Refresh interval set to {interval:F1}s", theme.Success);
    }

    private void HandleHighlight(IConsoleContext context, UITheme theme, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float duration))
        {
            context.WriteLine(
                $"Current highlight duration: {context.Entities.HighlightDuration:F1}s",
                theme.Info
            );
            context.WriteLine("Usage: entity highlight <seconds>", theme.TextDim);
            return;
        }

        context.Entities.HighlightDuration = duration;
        context.WriteLine($"Highlight duration set to {duration:F1}s", theme.Success);
    }
}
