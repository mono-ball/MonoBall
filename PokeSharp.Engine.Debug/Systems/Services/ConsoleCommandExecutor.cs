using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Console.UI;
using PokeSharp.Engine.Debug.Scripting;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Executes console commands (built-in, aliases, scripts).
/// Extracted from ConsoleSystem to follow Single Responsibility Principle.
/// </summary>
public class ConsoleCommandExecutor : IConsoleCommandExecutor
{
    private readonly QuakeConsole _console;
    private readonly ConsoleScriptEvaluator _evaluator;
    private readonly ConsoleGlobals _globals;
    private readonly AliasMacroManager _aliasMacroManager;
    private readonly ScriptManager _scriptManager;
    private readonly OutputExporter _outputExporter;
    private readonly ConsoleCommandHistory _history;
    private readonly BookmarkedCommandsManager? _bookmarksManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the ConsoleCommandExecutor.
    /// </summary>
    public ConsoleCommandExecutor(
        QuakeConsole console,
        ConsoleScriptEvaluator evaluator,
        ConsoleGlobals globals,
        AliasMacroManager aliasMacroManager,
        ScriptManager scriptManager,
        OutputExporter outputExporter,
        ConsoleCommandHistory history,
        ILogger logger,
        BookmarkedCommandsManager? bookmarksManager = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _aliasMacroManager = aliasMacroManager ?? throw new ArgumentNullException(nameof(aliasMacroManager));
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _outputExporter = outputExporter ?? throw new ArgumentNullException(nameof(outputExporter));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _bookmarksManager = bookmarksManager;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set up globals output action
        _globals.OutputAction = (text) => _console.AppendOutput(text, Color.White);
    }

    /// <summary>
    /// Executes a command and returns the result.
    /// </summary>
    public async Task<CommandExecutionResult> ExecuteAsync(string command)
    {
        command = command.Trim();

        if (string.IsNullOrWhiteSpace(command))
        {
            return CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
        }

        try
        {
            // Begin a section for this command
            string displayCommand = command;

            // Check for alias expansion
            if (_aliasMacroManager.TryExpandAlias(command, out var expandedCommand))
            {
                _logger.LogDebug("Alias expanded: {Original} -> {Expanded}", command, expandedCommand);
                displayCommand = $"{command} [alias]";
                command = expandedCommand;
            }

            // Begin section with command as header
            _console.Output.BeginSection(displayCommand, PokeSharp.Engine.Debug.Console.UI.SectionType.Command);

            // Display alias expansion if applicable
            if (displayCommand.Contains("[alias]"))
            {
                _console.AppendOutput($"  > {command}", Output_AliasExpansion);
            }

            // Handle built-in commands
            if (IsBuiltInCommand(command, out var builtInResult))
            {
                _console.Output.EndSection();
                return builtInResult!;
            }

            // Execute as C# script
            var result = await _evaluator.EvaluateAsync(command, _globals);

            // Handle compilation errors with detailed formatting
            if (result.IsCompilationError)
            {
                DisplayCompilationErrors(result.Errors!, result.SourceCode!);
                _console.Output.EndSection();
                return CommandExecutionResult.Failure(new Exception("Compilation error"));
            }

            // Handle runtime errors
            if (result.IsRuntimeError)
            {
                DisplayRuntimeError(result.RuntimeException!);
                _console.Output.EndSection();
                return CommandExecutionResult.Failure(result.RuntimeException!);
            }

            // Handle successful execution
            if (string.IsNullOrWhiteSpace(result.Output) || result.Output == "null")
            {
                _console.Output.EndSection();
                return CommandExecutionResult.SuccessNoOutput();
            }

            _console.AppendOutput(result.Output, Success);
            _console.Output.EndSection();
            return CommandExecutionResult.SuccessOutput(result.Output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing command: {Command}", command);
            _console.AppendOutput($"Unexpected Error: {ex.Message}", Output_Error);
            _console.Output.EndSection();
            return CommandExecutionResult.Failure(ex);
        }
    }

    /// <summary>
    /// Checks if the command is a built-in command and executes it.
    /// </summary>
    private bool IsBuiltInCommand(string command, out CommandExecutionResult? result)
    {
        result = null;

        // clear
        if (command.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            _console.ClearOutput();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // reset
        if (command.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            _evaluator.Reset();
            _console.AppendOutput("Script state reset", Success);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // help
        if (command.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            DisplayHelp();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // history
        if (command.StartsWith("history", StringComparison.OrdinalIgnoreCase))
        {
            HandleHistoryCommand(command);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // scripts
        if (command.Equals("scripts", StringComparison.OrdinalIgnoreCase))
        {
            ListScripts();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // size commands
        if (command.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
        {
            HandleSizeCommand(command);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // log commands
        if (command.StartsWith("log ", StringComparison.OrdinalIgnoreCase))
        {
            HandleLogCommand(command.Substring(4).Trim());
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // filter commands
        if (command.StartsWith("filter ", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("filter", StringComparison.OrdinalIgnoreCase))
        {
            var args = command.Length > 6 ? command.Substring(7).Trim() : "";
            HandleFilterCommand(args);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // fold commands
        if (command.StartsWith("fold", StringComparison.OrdinalIgnoreCase))
        {
            var args = command.Length > 4 ? command.Substring(4).Trim() : "";
            HandleFoldCommand(args);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // unfold commands
        if (command.StartsWith("unfold", StringComparison.OrdinalIgnoreCase))
        {
            var args = command.Length > 6 ? command.Substring(6).Trim() : "";
            HandleUnfoldCommand(args);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // load command
        if (command.StartsWith("load", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Equals("load", StringComparison.OrdinalIgnoreCase))
            {
                // No filename provided
                _console.AppendOutput("Usage: load <filename>", Output_Warning);
                _console.AppendOutput("Example: load startup.csx", Color.LightGray);
                _console.AppendOutput("Use 'scripts' to list available scripts.", Color.LightGray);
                result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
                return true;
            }
            
            if (command.StartsWith("load ", StringComparison.OrdinalIgnoreCase))
            {
                var scriptName = command.Substring(5).Trim();
                if (string.IsNullOrWhiteSpace(scriptName))
                {
                    _console.AppendOutput("Usage: load <filename>", Output_Warning);
                    _console.AppendOutput("Example: load startup.csx", Color.LightGray);
                }
                else
                {
                    LoadScript(scriptName);
                }
                result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
                return true;
            }
        }

        // save command
        if (command.StartsWith("save", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Equals("save", StringComparison.OrdinalIgnoreCase))
            {
                // No filename provided
                _console.AppendOutput("Usage: save <filename>", Output_Warning);
                _console.AppendOutput("Example: save myscript.csx", Color.LightGray);
                _console.AppendOutput("Saves the current multi-line input to a script file.", Color.LightGray);
                result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
                return true;
            }
            
            if (command.StartsWith("save ", StringComparison.OrdinalIgnoreCase))
            {
                var scriptName = command.Substring(5).Trim();
                if (string.IsNullOrWhiteSpace(scriptName))
                {
                    _console.AppendOutput("Usage: save <filename>", Output_Warning);
                    _console.AppendOutput("Example: save myscript.csx", Color.LightGray);
                }
                else
                {
                    SaveScript(scriptName);
                }
                result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
                return true;
            }
        }

        // alias commands
        if (command.StartsWith("alias ", StringComparison.OrdinalIgnoreCase))
        {
            HandleAliasCommand(command.Substring(6).Trim());
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        if (command.Equals("aliases", StringComparison.OrdinalIgnoreCase))
        {
            ListAliases();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        if (command.StartsWith("unalias ", StringComparison.OrdinalIgnoreCase))
        {
            var aliasName = command.Substring(8).Trim();
            RemoveAlias(aliasName);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // bookmark commands
        if (command.StartsWith("bookmark ", StringComparison.OrdinalIgnoreCase))
        {
            HandleBookmarkCommand(command.Substring(9).Trim());
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        if (command.Equals("bookmarks", StringComparison.OrdinalIgnoreCase))
        {
            ListBookmarks();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        if (command.StartsWith("unbookmark ", StringComparison.OrdinalIgnoreCase))
        {
            var fKeyStr = command.Substring(11).Trim();
            RemoveBookmark(fKeyStr);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // export command (visible output)
        if (command.StartsWith("export ", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("export", StringComparison.OrdinalIgnoreCase))
        {
            var args = command.Length > 6 ? command.Substring(6).Trim() : null;
            HandleExportCommand(args, exportAll: false);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // export-all command (entire buffer)
        if (command.StartsWith("export-all ", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("export-all", StringComparison.OrdinalIgnoreCase))
        {
            var args = command.Length > 10 ? command.Substring(10).Trim() : null;
            HandleExportCommand(args, exportAll: true);
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        // exports command (list exports)
        if (command.Equals("exports", StringComparison.OrdinalIgnoreCase))
        {
            ListExports();
            result = CommandExecutionResult.SuccessNoOutput(isBuiltIn: true);
            return true;
        }

        return false;
    }

    private void DisplayHelp()
    {
        var titleColor = Info_Dim;
        var headerColor = Primary;
        var textColor = Text_Secondary;
        var exampleColor = Success;
        var separatorColor = Text_Disabled;

        // Title
        _console.AppendOutput("╔════════════════════════════════════════════════════════════╗", headerColor);
        _console.AppendOutput("║           PokeSharp Debug Console - Help                  ║", titleColor);
        _console.AppendOutput("╚════════════════════════════════════════════════════════════╝", headerColor);
        _console.AppendOutput("", Color.White);

        // Quick Reference
        _console.AppendOutput("═══ QUICK REFERENCE ═══", headerColor);
        _console.AppendOutput("  Execute any C# code in real-time with full game API access.", textColor);
        _console.AppendOutput("  Type 'Help()' (uppercase H) for API methods and global objects.", textColor);
        _console.AppendOutput("", Color.White);

        // Console Commands
        _console.AppendOutput("═══ CONSOLE COMMANDS ═══", headerColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Information:", Primary);
        _console.AppendOutput("    help                      Show this help message", textColor);
        _console.AppendOutput("    Help()                    Show API methods (Player, Map, etc.)", textColor);
        _console.AppendOutput("    history                   Show command history (see 'history help')", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Display:", Primary);
        _console.AppendOutput("    clear                     Clear console output", textColor);
        _console.AppendOutput("    size <small|medium|full>  Change console size", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Script Management:", Primary);
        _console.AppendOutput("    scripts                   List available .csx scripts", textColor);
        _console.AppendOutput("    load <file>               Load and execute a script", textColor);
        _console.AppendOutput("    save <file>               Save current input to script", textColor);
        _console.AppendOutput("    reset                     Reset script state (clear variables)", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Aliases:", Primary);
        _console.AppendOutput("    alias <name> <command>    Create command shortcut", textColor);
        _console.AppendOutput("    aliases                   List all defined aliases", textColor);
        _console.AppendOutput("    unalias <name>            Remove an alias", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Bookmarks (F1-F12 Shortcuts):", Primary);
        _console.AppendOutput("    bookmark <F1-F12> <cmd>   Assign command to F-key", textColor);
        _console.AppendOutput("    bookmarks                 List all bookmarked commands", textColor);
        _console.AppendOutput("    unbookmark <F1-F12>       Remove a bookmark", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Logging:", Primary);
        _console.AppendOutput("    log status                Show logging status", textColor);
        _console.AppendOutput("    log on                    Enable game log output to console", textColor);
        _console.AppendOutput("    log off                   Disable game log output", textColor);
        _console.AppendOutput("    log filter <level>        Filter by level (Debug/Info/Warning/Error)", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Output Filtering:", Primary);
        _console.AppendOutput("    filter                    Show filter status", textColor);
        _console.AppendOutput("    filter level <lvl> <on|off>  Toggle log level visibility", textColor);
        _console.AppendOutput("    filter search <text>      Search in output", textColor);
        _console.AppendOutput("    filter regex <pattern>    Regex filter", textColor);
        _console.AppendOutput("    filter clear              Clear all filters", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Section Folding:", Primary);
        _console.AppendOutput("    fold [all|commands|errors]    Collapse sections", textColor);
        _console.AppendOutput("    unfold [all|commands|errors]  Expand sections", textColor);
        _console.AppendOutput("    Click section headers         Toggle individual sections", textColor);
        _console.AppendOutput("    Alt+[                         Collapse all sections", textColor);
        _console.AppendOutput("    Alt+]                         Expand all sections", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Export:", Primary);
        _console.AppendOutput("    export [filename]         Save visible output to file", textColor);
        _console.AppendOutput("    export-all [filename]     Save entire buffer to file", textColor);
        _console.AppendOutput("    exports                   List exported files", textColor);
        _console.AppendOutput("", Color.White);

        // Keyboard Shortcuts
        _console.AppendOutput("═══ KEYBOARD SHORTCUTS ═══", headerColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Execution:", Primary);
        _console.AppendOutput("    Enter                     Execute command or accept suggestion", textColor);
        _console.AppendOutput("    Shift + Enter             New line with auto-indentation", Success);
        _console.AppendOutput("    Escape                    Close suggestions or console", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Multi-line Editing:", Primary);
        _console.AppendOutput("    • Auto-indentation after { [ (", exampleColor);
        _console.AppendOutput("    • Smart dedenting for closing braces", textColor);
        _console.AppendOutput("    • Line numbers (shown when multi-line)", textColor);
        _console.AppendOutput("    • Bracket matching (green/red highlight)", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Auto-Complete:", Primary);
        _console.AppendOutput("    Tab / Ctrl + Space        Trigger auto-complete", textColor);
        _console.AppendOutput("    Up / Down                 Navigate suggestions", textColor);
        _console.AppendOutput("    Page Up / Page Down       Scroll suggestions (if many)", textColor);
            _console.AppendOutput("    F1                        Show detailed docs for selection", headerColor);
            _console.AppendOutput("    @ prefix                  History suggestions (from past commands)", exampleColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Navigation:", Primary);
        _console.AppendOutput("    Up / Down                 Navigate command history", textColor);
        _console.AppendOutput("    Page Up / Page Down       Scroll console output", textColor);
        _console.AppendOutput("    Ctrl + Home / End         Jump to top/bottom of output", textColor);
        _console.AppendOutput("    Left / Right / Home / End Move cursor in input", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Left / Right   Jump between words", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Text Selection:", Primary);
        _console.AppendOutput("    Shift + Arrows            Select text", textColor);
        _console.AppendOutput("    Shift + Home / End        Select to start/end", textColor);
        _console.AppendOutput("    Shift + Ctrl/Cmd + Arrows Select words", textColor);
        _console.AppendOutput("    Ctrl/Cmd + A              Select all", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Clipboard:", Primary);
        _console.AppendOutput("    Ctrl/Cmd + C              Copy selection", textColor);
        _console.AppendOutput("    Ctrl/Cmd + X              Cut selection", textColor);
        _console.AppendOutput("    Ctrl/Cmd + V              Paste", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Text Editing:", Primary);
        _console.AppendOutput("    Backspace / Delete        Delete characters", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Backspace/Del  Delete words", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Z              Undo", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Y / Shift+Z    Redo", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Search:", Primary);
        _console.AppendOutput("    Ctrl/Cmd + F              Open search in output", textColor);
        _console.AppendOutput("    F3 / Enter (in search)    Next match", textColor);
        _console.AppendOutput("    Shift + F3                Previous match", textColor);
            _console.AppendOutput("    Ctrl/Cmd + R              Reverse-i-search (history search)", headerColor);
        _console.AppendOutput("    Ctrl/Cmd + S (in rev-search)  Previous match", textColor);
        _console.AppendOutput("    Escape (in search)        Exit search", textColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Console Control:", Primary);
        _console.AppendOutput("    ~ (tilde)                 Toggle console open/close", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Plus           Increase font size", textColor);
        _console.AppendOutput("    Ctrl/Cmd + Minus          Decrease font size", textColor);
        _console.AppendOutput("    Ctrl/Cmd + 0              Reset font size to default", textColor);
        _console.AppendOutput("", Color.White);

        // Examples
        _console.AppendOutput("═══ QUICK EXAMPLES ═══", headerColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Player API:", Primary);
        _console.AppendOutput("    Player.GetMoney()", exampleColor);
        _console.AppendOutput("    Player.AddMoney(1000)", exampleColor);
        _console.AppendOutput("    Player.GetPosition()", exampleColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Map & State:", Primary);
        _console.AppendOutput("    Map.TransitionToMap(1, 10, 10)", exampleColor);
        _console.AppendOutput("    GameState.SetFlag(\"myFlag\", true)", exampleColor);
        _console.AppendOutput("    GameState.GetFlag(\"myFlag\")", exampleColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Entity Queries:", Primary);
        _console.AppendOutput("    CountEntities()", exampleColor);
        _console.AppendOutput("    ListEntities()", exampleColor);
        _console.AppendOutput("    Inspect(GetPlayer())", exampleColor);
        _console.AppendOutput("", Color.White);
        _console.AppendOutput("  Scripts:", Primary);
        _console.AppendOutput("    load debug-info", exampleColor);
        _console.AppendOutput("    load teleport-player 5 10", exampleColor);
        _console.AppendOutput("", Color.White);

        // Footer
        _console.AppendOutput("────────────────────────────────────────────────────────────", separatorColor);
        _console.AppendOutput("  TIP: Auto-complete works on everything - try typing 'Player.' and press Tab!", Info_Dim);
        _console.AppendOutput("────────────────────────────────────────────────────────────", separatorColor);
        _console.AppendOutput("", Color.White);
    }

    private void HandleSizeCommand(string command)
    {
        if (command.Equals("size small", StringComparison.OrdinalIgnoreCase))
        {
            _console.UpdateConfig(_console.Config.WithSize(ConsoleSize.Small));
            _console.AppendOutput("Console size set to Small (25%)", Success);
        }
        else if (command.Equals("size medium", StringComparison.OrdinalIgnoreCase))
        {
            _console.UpdateConfig(_console.Config.WithSize(ConsoleSize.Medium));
            _console.AppendOutput("Console size set to Medium (50%)", Success);
        }
        else if (command.Equals("size full", StringComparison.OrdinalIgnoreCase))
        {
            _console.UpdateConfig(_console.Config.WithSize(ConsoleSize.Full));
            _console.AppendOutput("Console size set to Full (100%)", Success);
        }
        else
        {
            _console.AppendOutput("Usage: size <small|medium|full>", Color.Orange);
        }
    }

    private void HandleLogCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            _console.AppendOutput("Usage: log <on|off|filter|status>", Color.Orange);
            return;
        }

        var subCommand = parts[0].ToLower();

        switch (subCommand)
        {
            case "on":
                _console.UpdateConfig(_console.Config.WithLogging(true));
                _console.AppendOutput("[+] Console logging enabled", Success);
                _console.AppendOutput($"   Showing logs >= {_console.Config.MinimumLogLevel}", Color.LightGray);
                break;

            case "off":
                _console.UpdateConfig(_console.Config.WithLogging(false));
                _console.AppendOutput("[-] Console logging disabled", Output_Error);
                break;

            case "filter":
                if (parts.Length < 2)
                {
                    _console.AppendOutput("Usage: log filter <level>", Color.Orange);
                    _console.AppendOutput("Levels: Trace, Debug, Information, Warning, Error, Critical", Color.LightGray);
                    return;
                }

                var levelString = parts[1];
                if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(levelString, true, out var logLevel))
                {
                    _console.UpdateConfig(_console.Config.WithMinimumLogLevel(logLevel));
                    _console.AppendOutput($"[+] Log filter set to: {logLevel}", Success);
                }
                else
                {
                    _console.AppendOutput($"[!] Unknown log level: {levelString}", Color.Orange);
                    _console.AppendOutput("Valid levels: Trace, Debug, Information, Warning, Error, Critical", Color.LightGray);
                }
                break;

            case "status":
                _console.AppendOutput("=== Console Logging Status ===", Info_Dim);
                _console.AppendOutput($"  Enabled: {(_console.Config.LoggingEnabled ? "[+] Yes" : "[-] No")}", Color.LightGray);
                _console.AppendOutput($"  Min Level: {_console.Config.MinimumLogLevel}", Color.LightGray);
                break;

            default:
                _console.AppendOutput($"Unknown log command: {subCommand}", Color.Orange);
                _console.AppendOutput("Usage: log <on|off|filter|status>", Color.LightGray);
                break;
        }
    }

    private void HandleFilterCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            // Show filter status
            _console.AppendOutput("=== Output Filters ===", Info_Dim);

            var summary = _console.Output.GetFilterSummary();
            _console.AppendOutput($"  {summary}", Color.LightGray);

            var filteredCount = _console.Output.GetFilteredLineCount();
            var totalCount = _console.Output.TotalLines;
            _console.AppendOutput($"  Showing {filteredCount} of {totalCount} lines", Primary);

            _console.AppendOutput("", Color.White);
            _console.AppendOutput("Usage:", Primary);
            _console.AppendOutput("  filter level <Debug|Info|Warning|Error|System> <on|off>", Color.LightGray);
            _console.AppendOutput("  filter search <text>          Search in output", Color.LightGray);
            _console.AppendOutput("  filter regex <pattern>        Regex filter", Color.LightGray);
            _console.AppendOutput("  filter clear                  Clear all filters", Color.LightGray);
            return;
        }

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subCommand = parts[0].ToLower();

        switch (subCommand)
        {
            case "level":
                if (parts.Length < 3)
                {
                    _console.AppendOutput("Usage: filter level <Debug|Info|Warning|Error|System> <on|off>", Color.Orange);
                    return;
                }

                var levelStr = parts[1];
                var enabledStr = parts[2].ToLower();

                if (!Enum.TryParse<PokeSharp.Engine.Debug.Console.UI.LogLevel>(levelStr, true, out var level))
                {
                    _console.AppendOutput($"[!] Unknown log level: {levelStr}", Color.Orange);
                    _console.AppendOutput("Valid levels: Debug, Info, Warning, Error, System", Color.LightGray);
                    return;
                }

                bool enabled = enabledStr == "on";
                _console.Output.SetLogLevelFilter(level, enabled);

                var status = enabled ? "enabled" : "disabled";
                var statusColor = enabled ? Success : Output_Error;
                _console.AppendOutput($"[{(enabled ? "+" : "-")}] {level} level {status}", statusColor);
                break;

            case "search":
                if (parts.Length < 2)
                {
                    _console.Output.SetSearchFilter(null);
                    _console.AppendOutput("[+] Search filter cleared", Success);
                }
                else
                {
                    var searchText = string.Join(" ", parts.Skip(1));
                    _console.Output.SetSearchFilter(searchText);
                    var matchCount = _console.Output.GetFilteredLineCount();
                    _console.AppendOutput($"[+] Search filter set: \"{searchText}\" ({matchCount} matches)", Success);
                }
                break;

            case "regex":
                if (parts.Length < 2)
                {
                    _console.Output.SetRegexFilter(null);
                    _console.AppendOutput("[+] Regex filter cleared", Success);
                }
                else
                {
                    var pattern = string.Join(" ", parts.Skip(1));
                    if (_console.Output.SetRegexFilter(pattern))
                    {
                        var matchCount = _console.Output.GetFilteredLineCount();
                        _console.AppendOutput($"[+] Regex filter set: /{pattern}/ ({matchCount} matches)", Success);
                    }
                    else
                    {
                        _console.AppendOutput($"[!] Invalid regex pattern: {pattern}", Output_Error);
                    }
                }
                break;

            case "clear":
                _console.Output.ClearAllFilters();
                _console.AppendOutput("[+] All filters cleared", Success);
                break;

            default:
                _console.AppendOutput($"Unknown filter command: {subCommand}", Color.Orange);
                _console.AppendOutput("Usage: filter <level|search|regex|clear>", Color.LightGray);
                break;
        }
    }

    /// <summary>
    /// Handles fold command (collapse sections).
    /// </summary>
    private void HandleFoldCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            // Collapse all sections
            _console.Output.CollapseAllSections();
            var sections = _console.Output.GetAllSections();
            _console.AppendOutput($"[+] Collapsed all {sections.Count} sections", Success);
            return;
        }

        args = args.ToLower().Trim();

        switch (args)
        {
            case "all":
                _console.Output.CollapseAllSections();
                _console.AppendOutput("[+] Collapsed all sections", Success);
                break;

            case "commands":
                _console.Output.CollapseAllSections(PokeSharp.Engine.Debug.Console.UI.SectionType.Command);
                var commandSections = _console.Output.GetAllSections().Count(s => s.Type == PokeSharp.Engine.Debug.Console.UI.SectionType.Command);
                _console.AppendOutput($"[+] Collapsed {commandSections} command sections", Success);
                break;

            case "errors":
                _console.Output.CollapseAllSections(PokeSharp.Engine.Debug.Console.UI.SectionType.Error);
                var errorSections = _console.Output.GetAllSections().Count(s => s.Type == PokeSharp.Engine.Debug.Console.UI.SectionType.Error);
                _console.AppendOutput($"[+] Collapsed {errorSections} error sections", Success);
                break;

            default:
                _console.AppendOutput($"Unknown fold target: {args}", Color.Orange);
                _console.AppendOutput("Usage: fold [all|commands|errors]", Color.LightGray);
                break;
        }
    }

    /// <summary>
    /// Handles unfold command (expand sections).
    /// </summary>
    private void HandleUnfoldCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            // Expand all sections
            _console.Output.ExpandAllSections();
            var sections = _console.Output.GetAllSections();
            _console.AppendOutput($"[+] Expanded all {sections.Count} sections", Success);
            return;
        }

        args = args.ToLower().Trim();

        switch (args)
        {
            case "all":
                _console.Output.ExpandAllSections();
                _console.AppendOutput("[+] Expanded all sections", Success);
                break;

            case "commands":
                _console.Output.ExpandAllSections(PokeSharp.Engine.Debug.Console.UI.SectionType.Command);
                var commandSections = _console.Output.GetAllSections().Count(s => s.Type == PokeSharp.Engine.Debug.Console.UI.SectionType.Command);
                _console.AppendOutput($"[+] Expanded {commandSections} command sections", Success);
                break;

            case "errors":
                _console.Output.ExpandAllSections(PokeSharp.Engine.Debug.Console.UI.SectionType.Error);
                var errorSections = _console.Output.GetAllSections().Count(s => s.Type == PokeSharp.Engine.Debug.Console.UI.SectionType.Error);
                _console.AppendOutput($"[+] Expanded {errorSections} error sections", Success);
                break;

            default:
                _console.AppendOutput($"Unknown unfold target: {args}", Color.Orange);
                _console.AppendOutput("Usage: unfold [all|commands|errors]", Color.LightGray);
                break;
        }
    }

    private void ListScripts()
    {
        var scripts = _scriptManager.ListScripts();

        if (scripts.Count == 0)
        {
            _console.AppendOutput("No scripts found in Scripts directory.", Color.Orange);
            return;
        }

        _console.AppendOutput($"=== Available Scripts ({scripts.Count}) ===", Info_Dim);
        foreach (var script in scripts)
        {
            _console.AppendOutput($"  {script}", Color.LightGray);
        }
        _console.AppendOutput("Use 'load <filename>' to execute a script.", Color.LightGray);
    }

    private void LoadScript(string scriptName)
    {
        var scriptResult = _scriptManager.LoadScript(scriptName);

        if (!scriptResult.IsSuccess)
        {
            _console.AppendOutput($"Error: {scriptResult.Error}", Output_Error);
            return;
        }

        _console.AppendOutput($"Loading script: {scriptName}", Success);
        _console.AppendOutput($"--- Start {scriptName} ---", Color.LightGray);

        // Execute the script (will be handled async by the caller)
        _ = _evaluator.EvaluateAsync(scriptResult.Value!, _globals).ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                var result = task.Result;

                if (result.IsCompilationError)
                {
                    DisplayCompilationErrors(result.Errors!, result.SourceCode!);
                    _console.AppendOutput($"--- End {scriptName} (compilation error) ---", Color.LightGray);
                }
                else if (result.IsRuntimeError)
                {
                    DisplayRuntimeError(result.RuntimeException!);
                    _console.AppendOutput($"--- End {scriptName} (runtime error) ---", Color.LightGray);
                }
                else if (result.IsSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(result.Output) && result.Output != "null")
                {
                        _console.AppendOutput(result.Output, Output_Success);
                }
                _console.AppendOutput($"--- End {scriptName} ---", Color.LightGray);
                }
            }
            else if (task.IsFaulted)
            {
                _console.AppendOutput($"Script error: {task.Exception?.GetBaseException().Message}", Output_Error);
                _console.AppendOutput($"--- End {scriptName} (error) ---", Color.LightGray);
            }
        });
    }

    private void SaveScript(string scriptName)
    {
        var content = _console.GetInputText();

        if (string.IsNullOrWhiteSpace(content))
        {
            _console.AppendOutput("Nothing to save (input is empty).", Color.Orange);
            return;
        }

        var result = _scriptManager.SaveScript(scriptName, content);
        if (result.IsSuccess)
        {
            _console.AppendOutput($"Script saved: {scriptName}", Success);
        }
        else
        {
            _console.AppendOutput($"Error: {result.Error}", Output_Error);
        }
    }

    private void HandleAliasCommand(string args)
    {
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            _console.AppendOutput("Usage: alias <name> <command>", Color.Orange);
            _console.AppendOutput("Example: alias tp Player.SetPosition($1, $2)", Color.LightGray);
            return;
        }

        var name = parts[0];
        var command = parts[1];

        if (_aliasMacroManager.DefineAlias(name, command))
        {
            _console.AppendOutput($"[+] Alias '{name}' created", Success);
            _console.AppendOutput($"   > {command}", Color.LightGray);
            _aliasMacroManager.SaveAliases();
        }
        else
        {
            _console.AppendOutput($"[!] Failed to create alias '{name}'", Output_Error);
            _console.AppendOutput("Alias name must start with a letter or underscore.", Color.LightGray);
        }
    }

    private void ListAliases()
    {
        var aliases = _aliasMacroManager.GetAllAliases();

        if (aliases.Count == 0)
        {
            _console.AppendOutput("No aliases defined.", Color.Orange);
            _console.AppendOutput("Create one with: alias <name> <command>", Color.LightGray);
            return;
        }

        _console.AppendOutput($"=== Defined Aliases ({aliases.Count}) ===", Info_Dim);
        foreach (var (name, command) in aliases.OrderBy(x => x.Key))
        {
            _console.AppendOutput($"  {name} > {command}", Color.LightGray);
        }
    }

    private void RemoveAlias(string name)
    {
        if (_aliasMacroManager.RemoveAlias(name))
        {
            _console.AppendOutput($"[+] Alias '{name}' removed", Success);
            _aliasMacroManager.SaveAliases();
        }
        else
        {
            _console.AppendOutput($"[!] Alias '{name}' not found", Output_Error);
        }
    }

    private void HandleBookmarkCommand(string args)
    {
        if (_bookmarksManager == null)
        {
            _console.AppendOutput("[!] Bookmarks not available", Output_Error);
            return;
        }

        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            _console.AppendOutput("Usage: bookmark <F1-F12> <command>", Color.Orange);
            _console.AppendOutput("Example: bookmark F5 Player.GetMoney()", Color.LightGray);
            return;
        }

        var fKeyStr = parts[0].ToUpper();
        var command = parts[1];

        // Parse F-key (e.g., "F1" -> 1)
        if (!fKeyStr.StartsWith("F") || !int.TryParse(fKeyStr.Substring(1), out var fKeyNumber) ||
            fKeyNumber < 1 || fKeyNumber > BookmarkedCommandsManager.MaxBookmarks)
        {
            _console.AppendOutput($"[!] Invalid F-key: {parts[0]}. Must be F1-F12.", Output_Error);
            return;
        }

        if (_bookmarksManager.BookmarkCommand(fKeyNumber, command))
        {
            _console.AppendOutput($"[+] Bookmark {fKeyStr} created", Success);
            _console.AppendOutput($"   > Press {fKeyStr} to execute: {command}", Color.LightGray);
            _bookmarksManager.SaveBookmarks();
        }
        else
        {
            _console.AppendOutput($"[!] Failed to create bookmark {fKeyStr}", Output_Error);
        }
    }

    private void ListBookmarks()
    {
        if (_bookmarksManager == null)
        {
            _console.AppendOutput("[!] Bookmarks not available", Output_Error);
            return;
        }

        var bookmarks = _bookmarksManager.GetAllBookmarks();

        if (bookmarks.Count == 0)
        {
            _console.AppendOutput("No bookmarks defined.", Color.Orange);
            _console.AppendOutput("Create one with: bookmark <F1-F12> <command>", Color.LightGray);
            return;
        }

        _console.AppendOutput($"=== Bookmarked Commands ({bookmarks.Count}) ===", Info_Dim);
        foreach (var (fKey, command) in bookmarks.OrderBy(x => x.Key))
        {
            _console.AppendOutput($"  F{fKey} > {command}", Color.LightGray);
        }
    }

    private void RemoveBookmark(string fKeyStr)
    {
        if (_bookmarksManager == null)
        {
            _console.AppendOutput("[!] Bookmarks not available", Output_Error);
            return;
        }

        fKeyStr = fKeyStr.ToUpper();

        // Parse F-key (e.g., "F1" -> 1)
        if (!fKeyStr.StartsWith("F") || !int.TryParse(fKeyStr.Substring(1), out var fKeyNumber))
        {
            _console.AppendOutput($"[!] Invalid F-key: {fKeyStr}. Must be F1-F12.", Output_Error);
            return;
        }

        if (_bookmarksManager.RemoveBookmark(fKeyNumber))
        {
            _console.AppendOutput($"[+] Bookmark {fKeyStr} removed", Success);
            _bookmarksManager.SaveBookmarks();
        }
        else
        {
            _console.AppendOutput($"[!] Bookmark {fKeyStr} not found", Output_Error);
        }
    }

    /// <summary>
    /// Displays compilation errors with context and formatting.
    /// </summary>
    private void DisplayCompilationErrors(List<PokeSharp.Engine.Debug.Console.Scripting.FormattedError> errors, string sourceCode)
    {
        var errorColor = Output_Error;
        var lineNumberColor = Text_Disabled;
        var contextColor = Text_Secondary;
        var caretColor = Warning;
        var headerColor = Error;

        _console.AppendOutput("", Color.White);

        foreach (var error in errors)
        {
            // Error header with code and location
            var locationText = error.Line == error.EndLine
                ? $"Line {error.Line}, Column {error.Column}"
                : $"Lines {error.Line}-{error.EndLine}";

            _console.AppendOutput($"[{error.ErrorCode}] {locationText}", headerColor);
            _console.AppendOutput(error.Message, errorColor);
            _console.AppendOutput("", Color.White);

            // Show context lines
            foreach (var contextLine in error.Context)
            {
                var lineNumStr = contextLine.LineNumber.ToString().PadLeft(4);
                var prefix = contextLine.IsErrorLine ? " >" : "  ";
                var color = contextLine.IsErrorLine ? errorColor : contextColor;

                _console.AppendOutput($"{prefix}{lineNumStr} | {contextLine.Text}", color);

                // Add caret line for error line
                if (contextLine.IsErrorLine && contextLine.LineNumber == error.Line)
                {
                    var caretLine = PokeSharp.Engine.Debug.Console.Scripting.ErrorFormatter.GenerateCaretLine(
                        error.Column,
                        Math.Max(1, error.EndColumn - error.Column));

                    // Add spacing for line number prefix
                    var spacingPrefix = "       | "; // Matches the line number formatting
                    _console.AppendOutput($"{spacingPrefix}{caretLine}", caretColor);
                }
            }

            _console.AppendOutput("", Color.White);
        }
    }

    /// <summary>
    /// Displays runtime errors with stack trace.
    /// </summary>
    private void DisplayRuntimeError(Exception exception)
    {
        var errorColor = Output_Error;
        var headerColor = Error;
        var stackColor = Text_Secondary;

        _console.AppendOutput("", Color.White);
        _console.AppendOutput("╔══════════════════════════════════════════════════════════╗", errorColor);
        _console.AppendOutput("║                   RUNTIME ERROR                          ║", errorColor);
        _console.AppendOutput("╚══════════════════════════════════════════════════════════╝", errorColor);
        _console.AppendOutput("", Color.White);

        // Exception type and message
        _console.AppendOutput($"[{exception.GetType().Name}]", headerColor);
        _console.AppendOutput(exception.Message, errorColor);
        _console.AppendOutput("", Color.White);

        // Stack trace (first few relevant lines)
        if (!string.IsNullOrEmpty(exception.StackTrace))
        {
            _console.AppendOutput("Stack Trace:", Text_Secondary);
            var stackLines = exception.StackTrace.Split('\n').Take(5);
            foreach (var line in stackLines)
            {
                _console.AppendOutput($"  {line.Trim()}", stackColor);
            }
            _console.AppendOutput("", Color.White);
        }

        // Inner exception if present
        if (exception.InnerException != null)
        {
            _console.AppendOutput($"Inner Exception: {exception.InnerException.GetType().Name}", headerColor);
            _console.AppendOutput(exception.InnerException.Message, errorColor);
            _console.AppendOutput("", Color.White);
        }
    }

    /// <summary>
    /// Handles export command to save console output to file.
    /// </summary>
    private void HandleExportCommand(string? filename, bool exportAll)
    {
        var result = _outputExporter.ExportOutput(_console.Output, filename, exportAll, includeMetadata: true);

        if (result.Success)
        {
            var typeText = exportAll ? "full buffer" : "visible output";
            _console.AppendOutput($"[+] Exported {typeText} ({result.LinesExported} lines) to:", Success);
            _console.AppendOutput($"  {result.FilePath}", Primary);
            _logger.LogInformation("Exported {Lines} lines to {Path}", result.LinesExported, result.FilePath);
        }
        else
        {
            _console.AppendOutput($"[!] Export failed: {result.ErrorMessage}", Output_Error);
            _logger.LogError("Export failed: {Error}", result.ErrorMessage);
        }
    }

    /// <summary>
    /// Handles the history command with various subcommands.
    /// </summary>
    private void HandleHistoryCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subcommand = parts.Length > 1 ? parts[1].ToLower() : "";

        var headerColor = Primary;
        var textColor = Text_Secondary;
        var highlightColor = Success;

        // history (no args) - show recent 20
        if (string.IsNullOrEmpty(subcommand))
        {
            _console.AppendOutput("", Color.White);
            _console.AppendOutput("Recent Command History:", headerColor);
            _console.AppendOutput("", Color.White);

            var entries = _history.GetRecent(20);
            int index = _history.Count;

            foreach (var entry in entries)
            {
                var timeAgo = GetTimeAgo(entry.LastUsed);
                var countStr = entry.UseCount > 1 ? $" (×{entry.UseCount})" : "";
                _console.AppendOutput($"  {index,3}  {timeAgo,-12}  {entry.Command}{countStr}", textColor);
                index--;
            }

            _console.AppendOutput("", Color.White);
            _console.AppendOutput($"Total: {_history.Count} commands  •  Use 'history help' for more options", Text_Disabled);
            _console.AppendOutput("", Color.White);
        }
        // history stats - show statistics
        else if (subcommand == "stats")
        {
            _console.AppendOutput("", Color.White);
            _console.AppendOutput("Command History Statistics:", headerColor);
            _console.AppendOutput("", Color.White);

            var mostUsed = _history.GetMostUsed(10).ToList();

            _console.AppendOutput($"  Total Commands: {_history.Count}", textColor);
            _console.AppendOutput($"  Total Executions: {_history.GetAllEntries().Sum(e => e.UseCount)}", textColor);
            _console.AppendOutput("", Color.White);

            if (mostUsed.Any())
            {
                _console.AppendOutput("  Most Used Commands:", highlightColor);
                foreach (var entry in mostUsed)
                {
                    _console.AppendOutput($"    {entry.UseCount,3}×  {entry.Command}", textColor);
                }
            }

            _console.AppendOutput("", Color.White);
        }
        // history search <term> - search history
        else if (subcommand == "search" && parts.Length > 2)
        {
            var searchTerm = string.Join(" ", parts.Skip(2));
            var matches = _history.Search(searchTerm).Take(20).ToList();

            _console.AppendOutput("", Color.White);
            _console.AppendOutput($"History Search: '{searchTerm}'", headerColor);
            _console.AppendOutput("", Color.White);

            if (matches.Any())
            {
                foreach (var entry in matches)
                {
                    var timeAgo = GetTimeAgo(entry.LastUsed);
                    var countStr = entry.UseCount > 1 ? $" (×{entry.UseCount})" : "";

                    // Highlight the search term in the command
                    var cmd = entry.Command;
                    var matchIndex = cmd.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
                    if (matchIndex >= 0)
                    {
                        var before = cmd.Substring(0, matchIndex);
                        var match = cmd.Substring(matchIndex, searchTerm.Length);
                        var after = cmd.Substring(matchIndex + searchTerm.Length);
                        _console.AppendOutput($"  {timeAgo,-12}  {before}", textColor);
                        _console.AppendOutput(match, Info_Dim);
                        _console.AppendOutput(after + countStr, textColor);
                    }
                    else
                    {
                        _console.AppendOutput($"  {timeAgo,-12}  {cmd}{countStr}", textColor);
                    }
                }

                _console.AppendOutput("", Color.White);
                _console.AppendOutput($"Found {matches.Count} matches", highlightColor);
            }
            else
            {
                _console.AppendOutput("  No matches found", Warning);
            }

            _console.AppendOutput("", Color.White);
        }
        // history clear - clear all history
        else if (subcommand == "clear")
        {
            _history.Clear();
            _console.AppendOutput("[+] Command history cleared", highlightColor);
        }
        // history help - show help
        else
        {
            _console.AppendOutput("", Color.White);
            _console.AppendOutput("History Commands:", headerColor);
            _console.AppendOutput("", Color.White);
            _console.AppendOutput("  history             Show recent 20 commands", textColor);
            _console.AppendOutput("  history stats       Show usage statistics", textColor);
            _console.AppendOutput("  history search <term>  Search history for a term", textColor);
            _console.AppendOutput("  history clear       Clear all history", textColor);
            _console.AppendOutput("  history help        Show this help", textColor);
            _console.AppendOutput("", Color.White);
            _console.AppendOutput("Tip: Use Ctrl+R for interactive reverse-i-search!", Primary);
            _console.AppendOutput("", Color.White);
        }
    }

    /// <summary>
    /// Gets a human-readable "time ago" string.
    /// </summary>
    private string GetTimeAgo(DateTime timestamp)
    {
        var span = DateTime.Now - timestamp;

        if (span.TotalSeconds < 60)
            return "just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays}d ago";

        return timestamp.ToString("MM/dd");
    }

    /// <summary>
    /// Lists all exported files.
    /// </summary>
    private void ListExports()
    {
        var exports = _outputExporter.ListExports();
        var exportsDir = _outputExporter.GetExportsDirectory();

        _console.AppendOutput("", Color.White);
        _console.AppendOutput("═══ EXPORTED FILES ═══", Primary);
        _console.AppendOutput($"Directory: {exportsDir}", Text_Disabled);
        _console.AppendOutput("", Color.White);

        if (exports.Count == 0)
        {
            _console.AppendOutput("  No exports found", Text_Disabled);
        }
        else
        {
            foreach (var export in exports)
            {
                _console.AppendOutput($"  • {export}", Text_Secondary);
            }
            _console.AppendOutput("", Color.White);
            _console.AppendOutput($"Total: {exports.Count} file{(exports.Count != 1 ? "s" : "")}", Success);
        }

        _console.AppendOutput("", Color.White);
        _console.AppendOutput("Commands:", Text_Disabled);
        _console.AppendOutput("  export [filename]       - Export visible output", Text_Secondary);
        _console.AppendOutput("  export-all [filename]   - Export entire buffer", Text_Secondary);
        _console.AppendOutput("", Color.White);
    }
}