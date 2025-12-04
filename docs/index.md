# MonoBall Framework Documentation Index

**Last Updated:** November 25, 2025
**Status:** Cleaned up and organized
**Location:** `/docs/` (with subdirectories)

---

## 📚 Essential Documentation (10 Files)

All documentation has been organized into subdirectories:
- 📖 `/docs/guides/` - User-facing guides (3 files)
- 📚 `/docs/reference/` - Quick references (2 files)
- 🏗️ `/docs/architecture/` - Technical architecture (2 files)
- 📋 `/docs/planning/` - Project planning & tracking (3 files)

---

### 📋 Planning & Tracking (`/docs/planning/`)

#### **CONSOLE_TODO.md** ⭐ PRIMARY DOCUMENT
**Location:** `/docs/planning/CONSOLE_TODO.md`
**Purpose:** Complete feature tracking for console system
**Contains:**
- Status of all console features (Console, Watch, Logs, Variables tabs)
- Missing features for parity with old console
- Implementation priorities and time estimates
- Recommended sprint planning

**Use this for:** Planning next features to implement

---

#### **REFACTORING_ACTION_PLAN.md**
**Location:** `/docs/planning/REFACTORING_ACTION_PLAN.md`
**Purpose:** Overall architecture and refactoring roadmap
**Contains:**
- Theme consolidation strategy
- Architecture improvements
- Phase-by-phase implementation plan

**Use this for:** Understanding the overall architecture vision

---

#### **THREADED_WATCH_EVALUATION_DESIGN.md** ⚡ PERFORMANCE
**Location:** `/docs/planning/THREADED_WATCH_EVALUATION_DESIGN.md`
**Purpose:** Design for threaded watch evaluation system
**Contains:**
- Problem statement (frame drops from expensive watches)
- Architecture options (background thread vs async)
- Detailed implementation plan
- Thread safety considerations
- Performance tracking

**Use this for:** Implementing threaded watch evaluation to eliminate frame drops

---

### 📖 User Guides (`/docs/guides/`)

#### **NEW_CONSOLE_QUICKSTART.md**
**Location:** `/docs/guides/NEW_CONSOLE_QUICKSTART.md`
**Purpose:** How to use the console
**Contains:**
- How to open the console (Ctrl+~)
- Basic commands and examples
- Troubleshooting tips

**Use this for:** Getting started with the console

---

#### **CONSOLE_KEY_BINDINGS.md**
**Location:** `/docs/guides/CONSOLE_KEY_BINDINGS.md`
**Purpose:** Complete key binding reference
**Contains:**
- All console hotkeys
- Tab switching shortcuts
- Debug overlay keys
- Key handling priority

**Use this for:** Quick reference for all keyboard shortcuts

---

#### **CONSOLE_TESTING_GUIDE.md**
**Location:** `/docs/guides/CONSOLE_TESTING_GUIDE.md`
**Purpose:** Testing procedures and test cases
**Contains:**
- How to test console features
- Expected behaviors
- Pass/fail criteria
- Manual testing procedures

**Use this for:** Verifying features work correctly

---

### 📚 Reference Documentation (`/docs/reference/`)

#### **HIGH_VALUE_WATCH_FEATURES.md** ⭐ WATCH SYSTEM GUIDE
**Location:** `/docs/reference/HIGH_VALUE_WATCH_FEATURES.md`
**Purpose:** Complete watch system documentation
**Contains:**
- All watch features (monitoring, alerts, comparisons, presets)
- Usage examples and best practices
- Command reference
- Implementation details

**Use this for:** Understanding and using the watch system (100% complete!)

---

#### **WATCH_QUICK_REFERENCE.md**
**Location:** `/docs/reference/WATCH_QUICK_REFERENCE.md`
**Purpose:** Quick command reference for watch system
**Contains:**
- Quick command syntax
- Common patterns
- Cheat sheet format

**Use this for:** Fast lookup of watch commands

---

### 🏗️ Architecture Documentation (`/docs/architecture/`)

#### **INPUT_CONSUMPTION_PATTERN.md**
**Location:** `/docs/architecture/INPUT_CONSUMPTION_PATTERN.md`
**Purpose:** Input handling architecture
**Contains:**
- Event bubbling pattern
- Input capture mechanism
- How components consume input
- Best practices for input handling

**Use this for:** Understanding input system architecture

---

#### **KEY_REPEAT_ARCHITECTURE.md**
**Location:** `/docs/architecture/KEY_REPEAT_ARCHITECTURE.md`
**Purpose:** Key repeat system architecture
**Contains:**
- How key repeat works
- Initial delay and repeat rate
- Implementation details
- Performance considerations

**Use this for:** Understanding keyboard input timing

---

## 🗑️ Cleaned Up (78 Files Deleted)

The following types of temporary documents were removed:

### Implementation Summaries
- Feature implementation tracking documents
- Bug fix summaries
- Migration completion documents
- Phase completion reports

### Analysis Documents
- Code analysis reports
- Architecture analysis
- UX consistency analyses
- Layout consistency analyses

### Debug & Troubleshooting
- Debug guides for specific issues
- Troubleshooting documents
- Investigation reports
- Bug fix tracking

### Development Tracking
- Console migration status
- Mouse support progress
- Tab system implementation
- Watch feature development

**Why deleted:** These were temporary tracking documents used during development. The work is complete and the information is either in code or consolidated into the main tracking documents above.

---

## 📂 Current Organization

Documentation is now organized into subdirectories:

```
/docs
├── DOCUMENTATION_INDEX.md   # This file (navigation guide)
│
├── /guides                  # User-facing guides (3 files)
│   ├── NEW_CONSOLE_QUICKSTART.md
│   ├── CONSOLE_KEY_BINDINGS.md
│   └── CONSOLE_TESTING_GUIDE.md
│
├── /reference               # Quick references (2 files)
│   ├── HIGH_VALUE_WATCH_FEATURES.md
│   └── WATCH_QUICK_REFERENCE.md
│
├── /architecture            # Technical architecture docs (2 files)
│   ├── INPUT_CONSUMPTION_PATTERN.md
│   └── KEY_REPEAT_ARCHITECTURE.md
│
└── /planning                # Project planning & tracking (3 files)
    ├── CONSOLE_TODO.md
    ├── REFACTORING_ACTION_PLAN.md
    └── THREADED_WATCH_EVALUATION_DESIGN.md  ⚡ NEW
```

**Note:** The `/docs` directory also contains other project documentation (architecture, testing, profiling, etc.) that was already organized.

---

## 🎯 Quick Navigation

**Want to implement a feature?**
→ `/docs/planning/CONSOLE_TODO.md`

**Want to learn the watch system?**
→ `/docs/reference/HIGH_VALUE_WATCH_FEATURES.md`

**Need to improve watch performance?** ⚡
→ `/docs/planning/THREADED_WATCH_EVALUATION_DESIGN.md`

**Need keyboard shortcuts?**
→ `/docs/guides/CONSOLE_KEY_BINDINGS.md`

**Understanding architecture?**
→ `/docs/architecture/INPUT_CONSUMPTION_PATTERN.md`
→ `/docs/architecture/KEY_REPEAT_ARCHITECTURE.md`

**Getting started as a user?**
→ `/docs/guides/NEW_CONSOLE_QUICKSTART.md`

---

## ✅ Benefits of Cleanup

**Before:** 87 markdown files (mostly temporary tracking documents)
**After:** 10 essential documents

**Results:**
- ✅ 88% reduction in documentation clutter
- ✅ Easy to find relevant information
- ✅ Clear purpose for each document
- ✅ No duplicate or outdated information
- ✅ Focused on what matters

---

**The documentation is now clean, organized, and focused on essential information!** 🎉

