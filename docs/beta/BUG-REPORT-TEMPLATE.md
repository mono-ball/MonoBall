# Bug Report Template

Thank you for helping improve PokeSharp! Please fill out this template as completely as possible.

---

## Bug Information

### Title
<!-- Short, descriptive title (e.g., "Hot-reload fails with custom events") -->

### Severity
<!-- Choose one: -->
- [ ] **Critical** - Crashes, data loss, security issue
- [ ] **High** - Core feature broken, unable to continue testing
- [ ] **Medium** - Feature has issues but workaround exists
- [ ] **Low** - Minor issue, cosmetic problem

### Category
<!-- Choose one: -->
- [ ] API / ScriptBase
- [ ] Event System
- [ ] Hot-Reload
- [ ] State Management
- [ ] Performance
- [ ] Documentation
- [ ] Installation / Setup
- [ ] Other: ___________

---

## Environment

### System Information
- **Operating System**: <!-- e.g., Windows 11, macOS 14.1, Ubuntu 22.04 -->
- **.NET Version**: <!-- Run: dotnet --version -->
- **PokeSharp Version**: <!-- Beta version number -->
- **IDE/Editor**: <!-- e.g., VS Code 1.85, Visual Studio 2022 -->

### Hardware (if relevant)
- **CPU**: <!-- e.g., Intel i7-9700K -->
- **RAM**: <!-- e.g., 16 GB -->
- **GPU**: <!-- e.g., NVIDIA GTX 1660 (if graphics-related) -->

---

## Bug Description

### What Happened?
<!-- Describe what went wrong -->

### Expected Behavior
<!-- What should have happened? -->

### Actual Behavior
<!-- What actually happened? -->

---

## Reproduction Steps

<!-- Provide detailed steps to reproduce the bug -->

1.
2.
3.
4.

### Reproducibility
<!-- How often does this happen? -->
- [ ] Always (100%)
- [ ] Frequently (>50%)
- [ ] Sometimes (10-50%)
- [ ] Rarely (<10%)
- [ ] Only once

---

## Code

### Mod Code
<!-- If applicable, paste your mod code -->

```csharp
// Paste your script code here
```

### Mod Configuration
<!-- If using manifest.json or config -->

```json
{
  // Paste config here
}
```

---

## Error Messages

### Console Output
<!-- Paste relevant console logs -->

```
[TIMESTAMP] [LEVEL] Message
```

### Exception / Stack Trace
<!-- If there's an exception, paste full stack trace -->

```
System.Exception: ...
  at PokeSharp.Module.Method() in File.cs:line 123
  at ...
```

### Error Logs
<!-- If error logs exist, paste relevant sections -->

```
# From logs/error.log
```

---

## Screenshots / Videos

### Screenshots
<!-- Attach or link to screenshots showing the issue -->
<!-- Use drag-and-drop or link to imgur, etc. -->

### Videos
<!-- If behavior is hard to describe, record a video -->
<!-- Link to YouTube, Loom, etc. -->

### GIFs
<!-- For short visual bugs, GIFs work great -->
<!-- Use tools like ScreenToGif, LICEcap -->

---

## Additional Context

### Other Mods Loaded
<!-- List other mods that were active when bug occurred -->
- Mod1.csx
- Mod2.csx
- ...

### Recent Changes
<!-- Did you just make any changes before the bug appeared? -->

### Workarounds
<!-- Have you found any way to work around this issue? -->

### Related Issues
<!-- Link to any related bugs or similar issues -->

---

## Proposed Solution (Optional)

<!-- If you have ideas on how to fix this, share them! -->

---

## Checklist

Before submitting, please verify:

- [ ] I've searched existing issues and this is not a duplicate
- [ ] I've provided .NET version and OS information
- [ ] I've included steps to reproduce
- [ ] I've included relevant code/config
- [ ] I've included error messages/logs
- [ ] I've indicated severity and category
- [ ] I've tested with only this mod enabled (if applicable)

---

## For Maintainers (Do Not Fill)

- **Assigned To**:
- **Priority**:
- **Milestone**:
- **Labels**:
- **Status**:

---

**Thank you for your contribution to making PokeSharp better!** 🙏
