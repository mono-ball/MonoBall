using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Utilities;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Manages console aliases and macros for creating shortcuts to common commands.
///     Supports parameterized macros with $1, $2, etc. for argument substitution.
/// </summary>
public class AliasMacroManager
{
    private readonly Dictionary<string, string> _aliases = new();
    private readonly string _aliasesFilePath;
    private readonly ILogger? _logger;

    /// <summary>
    ///     Initializes a new instance of AliasMacroManager.
    /// </summary>
    /// <param name="aliasesFilePath">Path to save/load aliases from.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    public AliasMacroManager(string aliasesFilePath, ILogger? logger = null)
    {
        _aliasesFilePath =
            aliasesFilePath ?? throw new ArgumentNullException(nameof(aliasesFilePath));
        _logger = logger;
    }

    /// <summary>
    ///     Gets the number of defined aliases.
    /// </summary>
    public int Count => _aliases.Count;

    /// <summary>
    ///     Defines a new alias or updates an existing one.
    /// </summary>
    /// <param name="name">Alias name (e.g., "tp", "gm").</param>
    /// <param name="command">Command to execute (e.g., "Map.TransitionToMap", "Player.GiveMoney($1)").</param>
    /// <returns>True if added/updated, false if invalid.</returns>
    public bool DefineAlias(string name, string command)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        // Sanitize alias name (alphanumeric and underscore only)
        if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            _logger?.LogWarning(
                "Invalid alias name: {Name}. Must be alphanumeric with underscores.",
                name
            );
            return false;
        }

        _aliases[name] = command;
        _logger?.LogDebug("Alias defined: {Name} = {Command}", name, command);
        return true;
    }

    /// <summary>
    ///     Removes an alias.
    /// </summary>
    /// <param name="name">Alias name to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveAlias(string name)
    {
        if (_aliases.Remove(name))
        {
            _logger?.LogDebug("Alias removed: {Name}", name);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if an alias exists.
    /// </summary>
    public bool HasAlias(string name)
    {
        return _aliases.ContainsKey(name);
    }

    /// <summary>
    ///     Gets all defined aliases.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllAliases()
    {
        return _aliases;
    }

    /// <summary>
    ///     Expands an alias with optional arguments.
    /// </summary>
    /// <param name="input">User input (e.g., "tp 10 20" or "gm 1000").</param>
    /// <param name="expandedCommand">The expanded command if alias found.</param>
    /// <returns>True if input was an alias and was expanded.</returns>
    public bool TryExpandAlias(string input, out string expandedCommand)
    {
        expandedCommand = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Parse input: first word is potential alias, rest are arguments
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string aliasName = parts[0];

        if (!_aliases.TryGetValue(aliasName, out string? template))
        {
            return false;
        }

        // Extract arguments (everything after alias name)
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        // Expand parameters ($1, $2, etc.)
        expandedCommand = ExpandParameters(template, args);

        _logger?.LogDebug("Alias expanded: {Input} -> {Expanded}", input, expandedCommand);
        return true;
    }

    /// <summary>
    ///     Expands parameter placeholders ($1, $2, etc.) in a command template.
    /// </summary>
    private string ExpandParameters(string template, string[] args)
    {
        string result = template;

        // Replace $1, $2, ..., $9 with actual arguments
        for (int i = 0; i < args.Length && i < 9; i++)
        {
            string placeholder = $"${i + 1}";
            result = result.Replace(placeholder, args[i]);
        }

        // Check for missing parameters
        if (Regex.IsMatch(result, @"\$\d"))
        {
            _logger?.LogWarning("Macro has unfilled parameters: {Template}", result);
        }

        return result;
    }

    /// <summary>
    ///     Saves aliases to disk.
    /// </summary>
    public bool SaveAliases()
    {
        string content = string.Join(
            Environment.NewLine,
            _aliases.Select(kvp => $"{kvp.Key}={kvp.Value}")
        );
        if (FileUtilities.WriteTextFile(_aliasesFilePath, content, _logger))
        {
            _logger?.LogInformation(
                "Saved {Count} aliases to {Path}",
                _aliases.Count,
                _aliasesFilePath
            );
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Loads aliases from disk.
    /// </summary>
    public int LoadAliases()
    {
        string? content = FileUtilities.ReadTextFile(_aliasesFilePath, _logger);
        if (content == null)
        {
            _logger?.LogDebug(
                "Aliases file not found: {Path}. Starting with empty aliases.",
                _aliasesFilePath
            );
            return 0;
        }

        _aliases.Clear();
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int loaded = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            string[] parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                string name = parts[0].Trim();
                string command = parts[1].Trim();
                if (DefineAlias(name, command))
                {
                    loaded++;
                }
            }
        }

        _logger?.LogInformation("Loaded {Count} aliases from {Path}", loaded, _aliasesFilePath);
        return loaded;
    }

    /// <summary>
    ///     Clears all aliases.
    /// </summary>
    public void ClearAll()
    {
        _aliases.Clear();
        _logger?.LogDebug("All aliases cleared");
    }
}
