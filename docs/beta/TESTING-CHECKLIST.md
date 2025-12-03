# Beta Testing Checklist

Complete this checklist to ensure comprehensive testing of the PokeSharp modding platform.

**Your Name**: _________________________

**Date Started**: _____________________

**Date Completed**: ___________________

---

## Setup & Installation (Week 1)

### Prerequisites
- [ ] .NET 9.0 SDK installed
- [ ] Verified installation: `dotnet --version`
- [ ] Text editor or IDE ready
- [ ] Git installed (optional)

### Project Setup
- [ ] Downloaded beta package
- [ ] Extracted to working directory
- [ ] Built project successfully: `dotnet build`
- [ ] Ran project successfully: `dotnet run`
- [ ] Verified game window opens
- [ ] Checked console shows mod system initialized

### Documentation Review
- [ ] Read BETA-TESTING-GUIDE.md
- [ ] Read MOD-SHOWCASE.md
- [ ] Reviewed /docs/modding/getting-started.md
- [ ] Bookmarked /docs/modding/event-reference.md

---

## Basic Mod Creation (Week 1)

### First Mod
- [ ] Created `/mods/MyFirstMod.csx`
- [ ] Implemented ScriptBase class
- [ ] Subscribed to TileSteppedOnEvent
- [ ] Added logging with Context.Logger
- [ ] Verified mod loads on game start
- [ ] Tested in-game behavior
- [ ] Checked console output

### Hot-Reload Test
- [ ] Modified log message in mod
- [ ] Saved file
- [ ] Verified automatic reload (check console)
- [ ] Tested updated behavior in-game
- [ ] No restart required

### Compilation Errors
- [ ] Intentionally introduced syntax error
- [ ] Checked console for error message
- [ ] Verified error message clarity
- [ ] Fixed error
- [ ] Verified mod reloaded successfully

---

## Event System Testing (Week 1-2)

### Movement Events
- [ ] MovementStartedEvent subscription
- [ ] MovementCompletedEvent subscription
- [ ] MovementBlockedEvent subscription
- [ ] Verified event order (Started → Completed)
- [ ] Tested movement cancellation (PreventDefault)
- [ ] Checked entity property data
- [ ] Tested direction values (0=S, 1=W, 2=E, 3=N)

### Tile Events
- [ ] TileSteppedOnEvent subscription
- [ ] TileSteppedOffEvent subscription
- [ ] Tested on different tile types (grass, water, etc.)
- [ ] Verified tile coordinates accuracy
- [ ] Tested tile type filtering
- [ ] Tested behavior flags

### Collision Events
- [ ] CollisionCheckEvent subscription
- [ ] CollisionDetectedEvent subscription
- [ ] CollisionResolvedEvent subscription
- [ ] Tested entity-entity collision
- [ ] Tested entity-tile collision
- [ ] Tested solid vs non-solid collisions

### System Events
- [ ] TickEvent subscription (be careful - high frequency!)
- [ ] Tested delta time accuracy
- [ ] Verified performance impact
- [ ] Tested tick-based timers

---

## Event Subscription Patterns (Week 2)

### Global Subscription
- [ ] Created `On<Event>(handler)`
- [ ] Verified receives all events of type
- [ ] Tested with multiple entities
- [ ] Checked performance with many events

### Entity Filtering
- [ ] Created `OnEntity<Event>(entity, handler)`
- [ ] Verified only fires for specific entity
- [ ] Tested with player entity
- [ ] Tested with NPC entities
- [ ] Compared performance to unfiltered

### Tile Filtering
- [ ] Created `OnTile<Event>(position, handler)`
- [ ] Verified only fires for specific tile
- [ ] Tested multiple tile positions
- [ ] Tested position accuracy

### Priority Handling
- [ ] Created high priority handler (1000+)
- [ ] Created normal priority handler (500)
- [ ] Created low priority handler (-1000)
- [ ] Verified execution order
- [ ] Tested event cancellation from high priority

---

## State Management (Week 2)

### Basic State
- [ ] Initialized state in `Initialize()`
- [ ] Used `Set<T>(key, value)`
- [ ] Used `Get<T>(key, defaultValue)`
- [ ] Verified state persistence during session
- [ ] Verified state reset on hot-reload

### Complex State
- [ ] Created custom struct for state
- [ ] Stored complex state object
- [ ] Retrieved and modified state
- [ ] Tested state with multiple properties

### State Patterns
- [ ] Implemented counter pattern
- [ ] Implemented timer pattern
- [ ] Implemented cooldown pattern
- [ ] Implemented state machine pattern

---

## Custom Events (Week 2-3)

### Event Definition
- [ ] Created custom event record
- [ ] Implemented IGameEvent interface
- [ ] Added EventId and Timestamp
- [ ] Added custom properties
- [ ] Documented event purpose

### Event Publishing
- [ ] Published custom event with `Publish(evt)`
- [ ] Verified other mods receive event
- [ ] Tested event data integrity
- [ ] Checked event timing

### Cancellable Custom Events
- [ ] Created ICancellableEvent
- [ ] Implemented PreventDefault()
- [ ] Tested cancellation from subscriber
- [ ] Verified cancellation reason

---

## Multi-Script Composition (Week 2-3)

### Multiple Scripts
- [ ] Created 2+ scripts for same functionality
- [ ] Verified both load successfully
- [ ] Tested scripts working independently
- [ ] Tested scripts working together

### Script Communication
- [ ] Script A publishes event
- [ ] Script B subscribes to event
- [ ] Verified communication works
- [ ] Tested bidirectional communication

### Script Conflicts
- [ ] Tested 5+ scripts simultaneously
- [ ] Checked for event handler conflicts
- [ ] Verified no performance degradation
- [ ] Tested hot-reload with multiple scripts

---

## Example Mods Testing (Week 2)

### Tall Grass Logger
- [ ] Enabled example mod
- [ ] Walked on tall grass
- [ ] Verified console logging
- [ ] Tested with player and NPCs
- [ ] Modified and hot-reloaded

### Random Encounters
- [ ] Enabled example mod
- [ ] Walked on tall grass repeatedly
- [ ] Verified random encounters (10% rate)
- [ ] Checked encounter count tracking
- [ ] Tested custom WildEncounterEvent
- [ ] Verified species randomization

### Ice Tile Sliding
- [ ] Enabled example mod
- [ ] Tested sliding on ice tiles
- [ ] Verified continuous sliding
- [ ] Tested collision with walls
- [ ] Checked state machine transitions
- [ ] Tested audio feedback

### All Examples Together
- [ ] Enabled all 3 example mods
- [ ] Tested each independently
- [ ] Verified no conflicts
- [ ] Checked performance
- [ ] Reviewed console output

---

## Advanced Features (Week 3)

### Performance Optimization
- [ ] Created mod with entity filtering
- [ ] Created mod with tile filtering
- [ ] Compared performance to unfiltered
- [ ] Tested with 10+ mods enabled
- [ ] Monitored frame rate
- [ ] Checked console for warnings

### Error Handling
- [ ] Threw exception in handler
- [ ] Verified game didn't crash
- [ ] Checked error logged to console
- [ ] Verified other mods unaffected

### Lifecycle Testing
- [ ] Tested Initialize() called once
- [ ] Tested RegisterEventHandlers() called once
- [ ] Tested OnUnload() on hot-reload
- [ ] Verified cleanup on unload

---

## Custom Mod Creation (Week 3)

### Simple Mod
- [ ] Designed simple mod concept
- [ ] Implemented mod
- [ ] Tested functionality
- [ ] Documented mod
- [ ] Shared in #beta-showcase

### Intermediate Mod
- [ ] Designed mod with state management
- [ ] Implemented with multiple events
- [ ] Tested edge cases
- [ ] Optimized performance
- [ ] Shared in #beta-showcase

### Advanced Mod (Optional)
- [ ] Designed complex mod concept
- [ ] Implemented multi-script architecture
- [ ] Published custom events
- [ ] Tested with other mods
- [ ] Documented thoroughly
- [ ] Shared in #beta-showcase

---

## Documentation Testing (Week 3-4)

### Getting Started Guide
- [ ] Followed tutorial step-by-step
- [ ] Verified all examples work
- [ ] Noted any unclear sections
- [ ] Reported documentation bugs

### Event Reference
- [ ] Looked up event documentation
- [ ] Tested examples from reference
- [ ] Verified event properties accurate
- [ ] Checked for missing events

### Advanced Guide
- [ ] Read advanced patterns
- [ ] Implemented 2+ patterns
- [ ] Verified pattern correctness
- [ ] Noted confusing sections

### API Reference
- [ ] Used API docs during development
- [ ] Verified method signatures
- [ ] Tested documented examples
- [ ] Reported any inaccuracies

---

## Edge Cases & Stress Testing (Week 3-4)

### Edge Cases
- [ ] Null entity test
- [ ] Invalid coordinates test
- [ ] Missing component test
- [ ] Empty event handler test
- [ ] Hot-reload during event processing

### Stress Testing
- [ ] 10+ mods enabled simultaneously
- [ ] 100+ event subscriptions
- [ ] Rapid movement testing
- [ ] Long play session (1+ hour)
- [ ] Memory leak check

### Boundary Testing
- [ ] Map edge movement
- [ ] Max entity count
- [ ] Max event handler count
- [ ] Long event handler chains
- [ ] Deeply nested custom events

---

## Bug Reporting (Ongoing)

### Bugs Found
- [ ] Documented each bug with BUG-REPORT-TEMPLATE.md
- [ ] Included reproduction steps
- [ ] Provided error logs
- [ ] Attached screenshots if relevant
- [ ] Submitted to GitHub/Discord/Email

**Number of Bugs Reported**: _________

**Critical Bugs**: _________
**High Priority Bugs**: _________
**Medium Priority Bugs**: _________
**Low Priority Bugs**: _________

---

## Feedback Submission (Week 4)

### Feedback Form
- [ ] Completed FEEDBACK-FORM.md
- [ ] Provided ratings (1-5) for all categories
- [ ] Answered open-ended questions
- [ ] Suggested feature requests
- [ ] Shared mod creations
- [ ] Submitted feedback

### Additional Feedback
- [ ] Participated in Discord discussions
- [ ] Attended office hours (if applicable)
- [ ] Shared insights with other testers
- [ ] Responded to follow-up questions

---

## Final Testing (Week 4-5)

### Bug Fix Verification
- [ ] Tested fixes for reported bugs
- [ ] Verified bugs are resolved
- [ ] Checked for regressions
- [ ] Confirmed no new issues introduced

### Documentation Updates
- [ ] Reviewed updated documentation
- [ ] Verified fixes to confusing sections
- [ ] Checked new examples work
- [ ] Confirmed API changes documented

### Final Mod Test
- [ ] Re-tested all created mods
- [ ] Verified everything still works
- [ ] Tested with latest beta build
- [ ] Confirmed hot-reload stability

---

## Completion Summary

### Statistics

**Total Testing Time**: _________ hours

**Mods Created**: _________

**Events Tested**: _________

**Bugs Reported**: _________

**Documentation Issues**: _________

**Feature Requests**: _________

### Overall Experience

**Most Enjoyable Aspect**:
```


```

**Most Frustrating Aspect**:
```


```

**Would Participate in Future Betas?**
- [ ] Definitely yes
- [ ] Probably yes
- [ ] Maybe
- [ ] Probably not
- [ ] Definitely not

---

## Recognition

✅ **Testing Complete!**

Thank you for your thorough testing! You will be:
- Listed in CONTRIBUTORS.md
- Given "Beta Tester" Discord role
- Acknowledged in launch announcement
- Invited to future beta programs

**Top contributors** (based on bugs found, feedback quality, and mod creations) will receive special recognition!

---

**Submission Date**: ___________________

**Submit Checklist To**:
- [ ] Email: beta@pokesharp.dev
- [ ] Discord: #beta-completed
- [ ] Include feedback form with submission

**Questions?** Contact us in #beta-support

**Thank you for being an incredible beta tester!** 🌟
