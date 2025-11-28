# UI Controls - Component Documentation

This directory contains reusable UI controls for the PokeSharp debug framework.

## Text Editing Components

### TextEditor
**File:** `TextEditor.cs` (1,609 lines)
**Purpose:** Full-featured multi-line text editor with syntax highlighting, history, undo/redo

**Key Features:**
- Multi-line editing with line numbers
- Syntax highlighting for C# code
- Command history with persistence
- Undo/Redo support
- Text selection (mouse + keyboard)
- Auto-completion support
- Bracket matching

**Usage:**
```csharp
var editor = new TextEditor("my_editor")
{
    Prompt = NerdFontIcons.Prompt, // Or "> " for ASCII fallback
    MinVisibleLines = 1,
    MaxVisibleLines = 10
};

editor.OnSubmit += (text) => {
    Console.WriteLine($"Submitted: {text}");
};

editor.OnTextChanged += (text) => {
    // Update auto-completion
};
```

### TextEditorCursor
**File:** `TextEditorCursor.cs` (175 lines)
**Purpose:** Reusable cursor management for text editors

**Features:**
- Position tracking (line, column)
- Blink animation
- Movement operations
- Position validation

**Usage:**
```csharp
var cursor = new TextEditorCursor();
cursor.SetPosition(0, 0);
cursor.Update(gameTime); // Update blink animation

if (cursor.IsVisible)
{
    // Render cursor
}

cursor.MoveRight(currentLineLength);
```

### TextEditorSelection
**File:** `TextEditorSelection.cs` (234 lines)
**Purpose:** Reusable selection management for text editors

**Features:**
- Selection range tracking
- Word/line selection
- Selected text extraction
- Position containment checking

**Usage:**
```csharp
var selection = new TextEditorSelection();
selection.Start(0, 5);
selection.ExtendTo(0, 10);

if (selection.HasSelection)
{
    string selected = selection.GetSelectedText(lines);
}
```

### TextEditorHistory
**File:** `TextEditorHistory.cs` (214 lines)
**Purpose:** Reusable command history with navigation and persistence

**Features:**
- History storage with size limits
- Forward/backward navigation
- Duplicate prevention
- History search
- Disk persistence

**Usage:**
```csharp
var history = new TextEditorHistory { MaxSize = 100 };
history.LoadFromDisk();

history.Add("my command");

// Navigate history
string? previous = history.NavigatePrevious(currentText);
if (previous != null)
{
    SetText(previous);
}
```

### CommandInput
**File:** `CommandInput.cs` (732 lines)
**Purpose:** Single-line command input with auto-completion

**Features:**
- Single-line text editing
- Command history
- Auto-completion integration
- Keyboard shortcuts
- Submit on Enter

**Usage:**
```csharp
var input = new CommandInput("cmd_input")
{
    Placeholder = "Type a command..."
};

input.OnSubmit += (text) => {
    ExecuteCommand(text);
};
```

### TextBuffer
**File:** `TextBuffer.cs` (737 lines)
**Purpose:** Multi-line scrollable text display

**Implements:** `ITextDisplay`

**Features:**
- Colored text lines
- Category filtering
- Search highlighting
- Text selection (for copying)
- Auto-scroll
- Line limits

**Usage:**
```csharp
var buffer = new TextBuffer("output")
{
    AutoScroll = true,
    MaxLines = 5000
};

buffer.AppendLine("Hello, world!", Color.White);
buffer.AppendLine("Error occurred!", Color.Red);
```

## Dropdown Components

### SuggestionsDropdown
**File:** `SuggestionsDropdown.cs` (577 lines)
**Purpose:** Auto-completion dropdown with filtering

**Features:**
- Keyboard navigation (up/down/page up/page down)
- Mouse selection
- Category grouping
- Icons with colors
- Filtering
- Multi-column display

**Usage:**
```csharp
var dropdown = new SuggestionsDropdown("suggestions")
{
    MaxVisibleItems = 8
};

var suggestions = new List<SuggestionItem>
{
    new("Console", "Debug console", "API", Color.LightBlue),
    new("Player", "Player object", "API", Color.LightGreen)
};

dropdown.SetSuggestions(suggestions);
dropdown.Show();

dropdown.OnItemSelected += (item) => {
    InsertCompletion(item.Text);
};
```

## Helper Components

### HintBar
**File:** `HintBar.cs` (96 lines)
**Purpose:** Displays keyboard shortcut hints

**Usage:**
```csharp
var hints = new HintBar("hints");
hints.SetHints("Ctrl+F: Search | Esc: Close");
```

### SearchBar
**File:** `SearchBar.cs` (293 lines)
**Purpose:** Search input with navigation controls

**Features:**
- Text input with search icon
- Next/Previous buttons
- Match count display
- Case-sensitive toggle

**Usage:**
```csharp
var searchBar = new SearchBar("search");
searchBar.OnSearch += (query) => {
    FindMatches(query);
};

searchBar.OnNext += () => GoToNextMatch();
searchBar.OnPrevious += () => GoToPreviousMatch();
```

### ParameterHintTooltip
**File:** `ParameterHintTooltip.cs` (383 lines)
**Purpose:** Shows method signatures and parameters

**Features:**
- Multiple overload support
- Current parameter highlighting
- Overload cycling
- Positioned above cursor

**Usage:**
```csharp
var hints = new ParameterHintTooltip("param_hints");
hints.SetHints(methodHints, currentParameterIndex);
```

### DocumentationPopup
**File:** `DocumentationPopup.cs` (315 lines)
**Purpose:** Displays symbol documentation

**Features:**
- Title, summary, signature
- Parameter list
- Return type
- Examples
- Remarks
- Scrollable content

**Usage:**
```csharp
var docs = new DocumentationPopup("docs");
docs.SetDocumentation(new DocInfo
{
    Title = "Console.WriteLine",
    Summary = "Writes a line to the console",
    Signature = "void WriteLine(string text)",
    ReturnType = "void"
});
```

## Interfaces

### ITextInput
**File:** `Core/ITextInput.cs`
**Purpose:** Standard interface for text input components

```csharp
public interface ITextInput
{
    string Text { get; }
    void SetText(string text);
    void Clear();
    void Focus();
    event Action<string>? OnTextChanged;
    event Action<string>? OnSubmit;
}
```

**Implemented By:**
- `CommandInput`
- `TextEditor`

### ITextDisplay
**File:** `Core/ITextDisplay.cs`
**Purpose:** Standard interface for text display components

```csharp
public interface ITextDisplay
{
    void AppendLine(string text);
    void AppendLine(string text, Color color);
    void Clear();
    void ScrollToBottom();
}
```

**Implemented By:**
- `TextBuffer`

## Component Reusability

The extracted classes from `TextEditor` can be used by multiple components:

| Component | Cursor | Selection | History |
|-----------|--------|-----------|---------|
| TextEditor | ✅ | ✅ | ✅ |
| CommandInput | ✅ | ⚪ | ✅ |
| InputField | ✅ | ✅ | ⚪ |
| SearchBar | ✅ | ⚪ | ✅ |

✅ = Currently uses or should use
⚪ = Optionally could use

## Best Practices

### When to Use TextEditor vs CommandInput

**Use TextEditor when:**
- Multi-line editing needed
- Syntax highlighting required
- Complex text manipulation
- Code or script editing

**Use CommandInput when:**
- Single-line input sufficient
- Simple command entry
- Quick one-shot commands
- Space-constrained UI

### Performance Considerations

1. **TextBuffer Line Limits**
   - Always set `MaxLines` to prevent memory bloat
   - Default: 10,000 lines
   - Recommended: 5,000 for debug consoles

2. **Auto-Scroll**
   - Disable auto-scroll when user scrolls manually
   - Re-enable when scrolled to bottom

3. **Syntax Highlighting**
   - Only apply to visible lines
   - Cache highlighted results
   - Use incremental updates

## Future Improvements

### Potential Extractions
- `TextBufferRenderer` - Extract rendering logic from TextBuffer
- `SyntaxHighlighter` - Make syntax highlighting pluggable
- `KeyBindingManager` - Centralize keyboard shortcuts

### Missing Features
- Multi-cursor editing
- Code folding
- Find and replace
- Regex search
- Custom themes per component

