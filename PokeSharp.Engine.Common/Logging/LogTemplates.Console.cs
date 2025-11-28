using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Common.Logging;

/// <summary>
///     LogTemplates for Debug Console operations.
///     Event ID Range: 8000-8099 (Debug Console category)
/// </summary>
public static partial class LogTemplates
{
    // ====================================================================
    // Auto-complete Events (8000-8019)
    // ====================================================================

    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Auto-complete triggered | code='{Code}' | pos={Position}"
    )]
    public static partial void LogAutoCompleteTriggered(
        this ILogger logger,
        string code,
        int position
    );

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Got {Count} suggestions | filter='{FilterText}'"
    )]
    public static partial void LogAutoCompleteSuggestionsReceived(
        this ILogger logger,
        int count,
        string filterText
    );

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Filtered suggestions | '{FilterWord}': {MatchCount} matches"
    )]
    public static partial void LogAutoCompleteSuggestionsFiltered(
        this ILogger logger,
        string filterWord,
        int matchCount
    );

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Globals set | type={TypeName}"
    )]
    public static partial void LogAutoCompleteGlobalsSet(this ILogger logger, string typeName);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] ScriptState updated | {VariableCount} variables"
    )]
    public static partial void LogAutoCompleteScriptStateUpdated(
        this ILogger logger,
        int variableCount
    );

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Member access detected | '{MemberName}'"
    )]
    public static partial void LogAutoCompleteMemberAccess(this ILogger logger, string memberName);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Found {Count} members for '{ObjectName}'"
    )]
    public static partial void LogAutoCompleteMembersFound(
        this ILogger logger,
        int count,
        string objectName
    );

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Providing global completions | {Count} items"
    )]
    public static partial void LogAutoCompleteGlobalsProvided(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 8008,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Variable found in ScriptState | '{VariableName}' type={TypeName}"
    )]
    public static partial void LogAutoCompleteVariableFound(
        this ILogger logger,
        string variableName,
        string typeName
    );

    [LoggerMessage(
        EventId = 8009,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Property found in globals | '{PropertyName}' type={TypeName}"
    )]
    public static partial void LogAutoCompletePropertyFound(
        this ILogger logger,
        string propertyName,
        string typeName
    );

    [LoggerMessage(
        EventId = 8010,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Auto-complete failed | {ErrorMessage}"
    )]
    public static partial void LogAutoCompleteFailed(
        this ILogger logger,
        Exception ex,
        string errorMessage
    );

    // ====================================================================
    // Script Execution Events (8020-8039)
    // ====================================================================

    [LoggerMessage(
        EventId = 8020,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Script loaded | {Filename} | {CharCount} chars"
    )]
    public static partial void LogConsoleScriptLoaded(
        this ILogger logger,
        string filename,
        int charCount
    );

    [LoggerMessage(
        EventId = 8021,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Executing command | '{Command}'"
    )]
    public static partial void LogConsoleCommandExecuting(this ILogger logger, string command);

    [LoggerMessage(
        EventId = 8022,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Script execution failed | {Filename}"
    )]
    public static partial void LogConsoleScriptFailed(
        this ILogger logger,
        Exception ex,
        string filename
    );

    [LoggerMessage(
        EventId = 8023,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Script executed | {Filename}"
    )]
    public static partial void LogConsoleScriptExecuted(this ILogger logger, string filename);

    [LoggerMessage(
        EventId = 8024,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Loading script | {Filename} with {ArgCount} arguments"
    )]
    public static partial void LogConsoleScriptLoading(
        this ILogger logger,
        string filename,
        int argCount
    );

    [LoggerMessage(
        EventId = 8025,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Script not found | {Filename}"
    )]
    public static partial void LogConsoleScriptNotFound(this ILogger logger, string filename);

    [LoggerMessage(
        EventId = 8026,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Startup script not found | {ScriptName} | skipping"
    )]
    public static partial void LogConsoleStartupScriptNotFound(
        this ILogger logger,
        string scriptName
    );

    [LoggerMessage(
        EventId = 8027,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Loading startup script | {ScriptName}"
    )]
    public static partial void LogConsoleStartupScriptLoading(
        this ILogger logger,
        string scriptName
    );

    [LoggerMessage(
        EventId = 8028,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Startup script failed | {ScriptName}"
    )]
    public static partial void LogConsoleStartupScriptError(this ILogger logger, string scriptName);

    [LoggerMessage(
        EventId = 8029,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Startup script executed | {ScriptName}"
    )]
    public static partial void LogConsoleStartupScriptExecuted(
        this ILogger logger,
        string scriptName
    );

    // ====================================================================
    // Alias Events (8040-8059)
    // ====================================================================

    [LoggerMessage(
        EventId = 8040,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Alias expanded | '{Original}' → '{Expanded}'"
    )]
    public static partial void LogAliasExpanded(
        this ILogger logger,
        string original,
        string expanded
    );

    [LoggerMessage(
        EventId = 8041,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Alias defined | {Name} = {Command}"
    )]
    public static partial void LogAliasDefined(this ILogger logger, string name, string command);

    [LoggerMessage(
        EventId = 8042,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Loaded {Count} aliases from {Path}"
    )]
    public static partial void LogAliasesLoaded(this ILogger logger, int count, string path);

    [LoggerMessage(
        EventId = 8043,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Invalid alias name | {Name}"
    )]
    public static partial void LogAliasInvalidName(this ILogger logger, string name);

    [LoggerMessage(
        EventId = 8044,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Alias removed | {Name}"
    )]
    public static partial void LogAliasRemoved(this ILogger logger, string name);

    [LoggerMessage(
        EventId = 8045,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Saved {Count} aliases to {Path}"
    )]
    public static partial void LogAliasesSaved(this ILogger logger, int count, string path);

    [LoggerMessage(
        EventId = 8046,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to save aliases | {Path}"
    )]
    public static partial void LogAliasesSaveFailed(this ILogger logger, Exception ex, string path);

    [LoggerMessage(
        EventId = 8047,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to load aliases | {Path}"
    )]
    public static partial void LogAliasesLoadFailed(this ILogger logger, Exception ex, string path);

    [LoggerMessage(
        EventId = 8048,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Macro has unfilled parameters | {Template}"
    )]
    public static partial void LogAliasMacroUnfilledParameters(
        this ILogger logger,
        string template
    );

    [LoggerMessage(
        EventId = 8049,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] All aliases cleared"
    )]
    public static partial void LogAliasesCleared(this ILogger logger);

    // ====================================================================
    // Console Lifecycle Events (8060-8079)
    // ====================================================================

    [LoggerMessage(
        EventId = 8060,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]▶[/] Console initialized | v{Version} | {FeatureCount} features"
    )]
    public static partial void LogConsoleInitialized(
        this ILogger logger,
        string version,
        int featureCount
    );

    [LoggerMessage(
        EventId = 8061,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Console toggled | visible={IsVisible}"
    )]
    public static partial void LogConsoleToggled(this ILogger logger, bool isVisible);

    [LoggerMessage(
        EventId = 8062,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] History loaded | {Count} commands"
    )]
    public static partial void LogConsoleHistoryLoaded(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 8063,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] History saved | {Count} commands"
    )]
    public static partial void LogConsoleHistorySaved(this ILogger logger, int count);

    [LoggerMessage(
        EventId = 8064,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Failed to save console history"
    )]
    public static partial void LogConsoleHistorySaveFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8065,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Failed to load console history"
    )]
    public static partial void LogConsoleHistoryLoadFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8066,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] History file not found | starting with empty history"
    )]
    public static partial void LogConsoleHistoryFileNotFound(this ILogger logger);

    [LoggerMessage(
        EventId = 8067,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] History file cleared"
    )]
    public static partial void LogConsoleHistoryCleared(this ILogger logger);

    [LoggerMessage(
        EventId = 8068,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Console size changed | {Size}"
    )]
    public static partial void LogConsoleSizeChanged(this ILogger logger, string size);

    [LoggerMessage(
        EventId = 8069,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Console logging {Status} | min level={MinLevel}"
    )]
    public static partial void LogConsoleLoggingStatusChanged(
        this ILogger logger,
        string status,
        string minLevel
    );

    // ====================================================================
    // Script Manager Events (8070-8089)
    // ====================================================================

    [LoggerMessage(
        EventId = 8070,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Scripts directory initialized | {Directory}"
    )]
    public static partial void LogScriptManagerInitialized(this ILogger logger, string directory);

    [LoggerMessage(
        EventId = 8071,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Script saved | {Filename} | {CharCount} chars"
    )]
    public static partial void LogScriptSaved(this ILogger logger, string filename, int charCount);

    [LoggerMessage(
        EventId = 8072,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to save script | {Filename}"
    )]
    public static partial void LogScriptSaveFailed(
        this ILogger logger,
        Exception ex,
        string filename
    );

    [LoggerMessage(
        EventId = 8073,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] Script file not found | {Path}"
    )]
    public static partial void LogScriptFileNotFound(this ILogger logger, string path);

    [LoggerMessage(
        EventId = 8074,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to load script | {Filename}"
    )]
    public static partial void LogScriptLoadFailed(
        this ILogger logger,
        Exception ex,
        string filename
    );

    [LoggerMessage(
        EventId = 8075,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Found {Count} scripts in {Directory}"
    )]
    public static partial void LogScriptsListed(this ILogger logger, int count, string directory);

    [LoggerMessage(
        EventId = 8076,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to list scripts | {Directory}"
    )]
    public static partial void LogScriptsListFailed(
        this ILogger logger,
        Exception ex,
        string directory
    );

    [LoggerMessage(
        EventId = 8077,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Script deleted | {Filename}"
    )]
    public static partial void LogScriptDeleted(this ILogger logger, string filename);

    [LoggerMessage(
        EventId = 8078,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to delete script | {Filename}"
    )]
    public static partial void LogScriptDeleteFailed(
        this ILogger logger,
        Exception ex,
        string filename
    );

    [LoggerMessage(
        EventId = 8079,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] Script file not found for deletion | {Path}"
    )]
    public static partial void LogScriptDeleteNotFound(this ILogger logger, string path);

    [LoggerMessage(
        EventId = 8080,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Created scripts directory | {Directory}"
    )]
    public static partial void LogScriptsDirectoryCreated(this ILogger logger, string directory);

    [LoggerMessage(
        EventId = 8081,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Failed to create scripts directory | {Directory}"
    )]
    public static partial void LogScriptsDirectoryCreationFailed(
        this ILogger logger,
        Exception ex,
        string directory
    );

    [LoggerMessage(
        EventId = 8082,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] [green]✓[/] Created example script | example.csx"
    )]
    public static partial void LogExampleScriptCreated(this ILogger logger);

    [LoggerMessage(
        EventId = 8083,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Failed to create example script"
    )]
    public static partial void LogExampleScriptCreationFailed(this ILogger logger, Exception ex);

    // ====================================================================
    // Console Auto-complete UI Events (8084-8089)
    // ====================================================================

    [LoggerMessage(
        EventId = 8084,
        Level = LogLevel.Debug,
        Message = "[deepskyblue1]CONS[/] Set {TotalCount} suggestions | {FilteredCount} after filter"
    )]
    public static partial void LogAutoCompleteSuggestionsSet(
        this ILogger logger,
        int totalCount,
        int filteredCount
    );

    [LoggerMessage(
        EventId = 8085,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Roslyn returned 0 suggestions | code='{Code}'"
    )]
    public static partial void LogAutoCompleteNoSuggestions(this ILogger logger, string code);

    // ====================================================================
    // Console System Events (8090-8099)
    // ====================================================================

    [LoggerMessage(
        EventId = 8090,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Console is null in Update()"
    )]
    public static partial void LogConsoleNullInUpdate(this ILogger logger);

    [LoggerMessage(
        EventId = 8091,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Console is null in Render()"
    )]
    public static partial void LogConsoleNullInRender(this ILogger logger);

    [LoggerMessage(
        EventId = 8092,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Error updating console system"
    )]
    public static partial void LogConsoleUpdateError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8093,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Error rendering console"
    )]
    public static partial void LogConsoleRenderError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8094,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Error executing console command"
    )]
    public static partial void LogConsoleCommandError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8095,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Error loading startup script"
    )]
    public static partial void LogConsoleStartupScriptLoadError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8096,
        Level = LogLevel.Warning,
        Message = "[deepskyblue1]CONS[/] [yellow]⚠[/] Failed to paste from clipboard"
    )]
    public static partial void LogConsolePasteFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8097,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Pasting {Length} characters from clipboard"
    )]
    public static partial void LogConsolePasting(this ILogger logger, int length);

    [LoggerMessage(
        EventId = 8098,
        Level = LogLevel.Error,
        Message = "[deepskyblue1]CONS[/] [red]✗[/] Error getting auto-complete suggestions"
    )]
    public static partial void LogAutoCompleteError(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 8099,
        Level = LogLevel.Information,
        Message = "[deepskyblue1]CONS[/] Got {Count} auto-complete suggestions"
    )]
    public static partial void LogAutoCompleteSuggestionsCount(this ILogger logger, int count);
}
