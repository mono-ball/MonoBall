# PokeSharp Beta Testing Guide

Welcome to the PokeSharp modding platform beta! Thank you for volunteering to help test and improve the platform before the public release.

## What is PokeSharp?

PokeSharp is a C#-based Pokemon game engine with a powerful modding platform that allows developers to create custom behaviors, mechanics, and features using C# scripts. The modding platform uses an event-driven architecture that makes it easy to extend the game without modifying core code.

## What Are We Testing?

This beta focuses on the **modding platform** specifically:

### Core Features to Test
- **ScriptBase API**: The foundation for all mods
- **Event System**: 15+ built-in events for game interactions
- **Hot-Reload**: Make changes without restarting
- **Multi-Script Composition**: Multiple mods working together
- **State Management**: Persistent mod state
- **Entity/Tile Filtering**: Performance optimization
- **Custom Events**: Mod-to-mod communication

### Example Mods Included
1. **Tall Grass Logger**: Simple event logging
2. **Random Encounter System**: Configurable wild encounters
3. **Ice Tile Sliding**: Custom movement mechanics

## Installation and Setup

### Prerequisites
- **Operating System**: Windows, macOS, or Linux
- **.NET 9.0 SDK**: [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Text Editor**: VS Code, Visual Studio, Rider, or any text editor
- **Basic C# Knowledge**: Understanding of classes, methods, and events

### Installation Steps

1. **Download Beta Package**
   - Unzip to your preferred location (e.g., `C:\PokeSharp` or `~/Documents/PokeSharp`)

2. **Verify .NET Installation**
   ```bash
   dotnet --version
   # Should output: 9.0.x or higher
   ```

3. **Build the Project**
   ```bash
   cd PokeSharp
   dotnet build
   ```

4. **Run PokeSharp**
   ```bash
   dotnet run --project PokeSharp.Game
   ```

5. **Verify Installation**
   - Game window should open
   - Console should show "Modding system initialized"
   - Example mods should be loaded

## Testing Scenarios

### Scenario 1: Create Your First Mod (15 minutes)

**Goal**: Create a simple mod from scratch

**Steps**:
1. Create `/mods/MyFirstMod.csx`
2. Copy this code:
   ```csharp
   using PokeSharp.Game.Scripting.Runtime;
   using PokeSharp.Engine.Core.Events.Tile;

   public class MyFirstMod : ScriptBase
   {
       public override void RegisterEventHandlers(ScriptContext ctx)
       {
           On<TileSteppedOnEvent>(evt =>
           {
               Context.Logger.LogInformation("Step on {Type} at ({X}, {Y})",
                   evt.TileType, evt.TileX, evt.TileY);
           });
       }
   }
   ```
3. Save the file
4. Check console for: "MyFirstMod loaded successfully"
5. Move in-game and watch logs

**Expected Result**: You should see log messages for every tile you step on

**Report**: Any compilation errors, missing features, unclear error messages

---

### Scenario 2: Test Hot-Reload (10 minutes)

**Goal**: Verify mods reload without restarting

**Steps**:
1. Open your mod file from Scenario 1
2. Change the log message to: "I stepped on a tile!"
3. Save the file (Ctrl+S / Cmd+S)
4. Move in-game
5. Verify new message appears

**Expected Result**: Modified behavior takes effect immediately without restarting game

**Report**: Any cases where hot-reload fails or requires restart

---

### Scenario 3: Subscribe to Multiple Events (15 minutes)

**Goal**: Test event system with multiple subscriptions

**Steps**:
1. Create `/mods/MultiEventTest.csx`
2. Subscribe to:
   - `TileSteppedOnEvent`
   - `MovementCompletedEvent`
   - `MovementStartedEvent`
3. Log each event with relevant details
4. Test in-game

**Expected Result**: All three events fire in correct order:
1. MovementStartedEvent (before move)
2. MovementCompletedEvent (after move)
3. TileSteppedOnEvent (on new tile)

**Report**: Missing events, incorrect order, missing event data

---

### Scenario 4: Create Custom Events (20 minutes)

**Goal**: Test mod-to-mod communication

**Steps**:
1. Create **Mod A** that publishes custom event when on grass:
   ```csharp
   public sealed record GrassStepEvent : IGameEvent
   {
       public Guid EventId { get; init; } = Guid.NewGuid();
       public DateTime Timestamp { get; init; } = DateTime.UtcNow;
       public required Entity Entity { get; init; }
   }
   ```
2. Create **Mod B** that subscribes to `GrassStepEvent`
3. Test both mods together

**Expected Result**: Mod B receives events published by Mod A

**Report**: Issues with custom events, missing subscriptions, type conflicts

---

### Scenario 5: Test Example Mods (25 minutes)

**Goal**: Verify example mods work correctly

**Steps**:
1. Enable all example mods in `/mods/examples/`
2. Test Tall Grass Logger:
   - Walk on tall grass
   - Verify console logs
3. Test Random Encounters:
   - Walk on tall grass repeatedly
   - Verify encounters trigger (10% rate)
4. Test Ice Sliding:
   - Walk on ice tiles
   - Verify sliding behavior

**Expected Result**: All examples work as documented

**Report**: Broken examples, incorrect behavior, documentation mismatch

---

### Scenario 6: Test Multiple Mods Together (20 minutes)

**Goal**: Verify mods don't conflict

**Steps**:
1. Enable 3-5 different mods simultaneously
2. Test all major game actions:
   - Movement
   - Tile stepping
   - Collisions (if applicable)
3. Check console for errors
4. Verify all mods function correctly

**Expected Result**: All mods work independently without conflicts

**Report**: Conflicts, crashes, performance issues

---

### Scenario 7: Test State Management (15 minutes)

**Goal**: Verify mod state persists correctly

**Steps**:
1. Create mod that counts steps using `Get<T>()` and `Set<T>()`
2. Walk 10 steps
3. Verify count increments correctly
4. Hot-reload the mod
5. Verify count resets (expected behavior)

**Expected Result**: State works during session, resets on hot-reload

**Report**: State not persisting, incorrect reset behavior

---

### Scenario 8: Test Performance with Multiple Mods (15 minutes)

**Goal**: Identify performance issues

**Steps**:
1. Enable 5+ mods that subscribe to `MovementCompletedEvent`
2. Move rapidly around the map
3. Monitor console for performance warnings
4. Check for lag or frame drops

**Expected Result**: Smooth gameplay with no noticeable lag

**Report**: Frame drops, lag spikes, memory issues

---

### Scenario 9: Test Entity and Tile Filtering (15 minutes)

**Goal**: Verify filtering optimizations work

**Steps**:
1. Create mod using `OnEntity<Event>(player, evt => ...)`
2. Create mod using `OnTile<Event>(position, evt => ...)`
3. Verify filtered handlers only fire for correct entities/tiles
4. Compare performance to unfiltered handlers

**Expected Result**: Filters work correctly, better performance

**Report**: Filters not working, performance issues

---

### Scenario 10: Test Documentation (20 minutes)

**Goal**: Verify documentation accuracy

**Steps**:
1. Follow `/docs/modding/getting-started.md`
2. Try examples from `/docs/modding/event-reference.md`
3. Use patterns from `/docs/modding/advanced-guide.md`
4. Note any:
   - Broken examples
   - Unclear explanations
   - Missing information
   - Incorrect API usage

**Expected Result**: All documentation examples work as shown

**Report**: Documentation errors, missing explanations, outdated information

---

## How to Report Bugs

### Bug Report Template

Use the [BUG-REPORT-TEMPLATE.md](./BUG-REPORT-TEMPLATE.md) to report issues:

**Required Information**:
- Environment (OS, .NET version)
- Steps to reproduce
- Expected vs actual behavior
- Error messages/logs
- Mod code (if applicable)

### Where to Report

- **GitHub Issues**: [Link to issues]
- **Discord**: #beta-testing channel
- **Email**: beta@pokesharp.dev

### Bug Priority

- **Critical**: Crashes, data loss, security issues
- **High**: Core features broken, unable to test
- **Medium**: Features work but have issues
- Low: Minor issues, cosmetic problems

## Providing Feedback

### What We Want to Know

1. **Ease of Use**: How intuitive is the API?
2. **Documentation**: Is it clear and helpful?
3. **Performance**: Any lag or slowness?
4. **Features**: What's missing? What's confusing?
5. **Developer Experience**: What would make modding easier?

### Feedback Form

Please fill out [FEEDBACK-FORM.md](./FEEDBACK-FORM.md) after testing.

### Suggestions Welcome

We want to hear:
- Feature requests
- API improvements
- Documentation enhancements
- Workflow improvements
- Tool requests

## Testing Timeline

### Week 1: Onboarding (Current Week)
- Install and setup
- Create first mod
- Test basic features
- Report initial issues

### Week 2-3: Active Testing
- Test all scenarios
- Create custom mods
- Test mod interactions
- Report bugs and feedback

### Week 4: Feedback Integration
- Test bug fixes
- Verify improvements
- Final feedback round
- Prepare for public release

### Week 5: Beta Close
- Final bug fixes
- Documentation updates
- Success metrics review
- Thank you to beta testers!

## Communication Channels

### Discord
- **#beta-announcements**: Updates and news
- **#beta-testing**: Testing discussion
- **#beta-support**: Get help
- **#beta-showcase**: Share your mods!

### Office Hours
- **When**: Tuesdays and Thursdays, 2-4 PM EST
- **Where**: Discord voice chat
- **What**: Live support, Q&A, pair programming

### Email Updates
- Weekly progress reports
- Bug fix notifications
- Feature update announcements

## Beta Tester Recognition

### Hall of Fame
All beta testers will be:
- Listed in CREDITS.md
- Mentioned in launch announcement
- Given "Beta Tester" Discord role
- Invited to future beta programs

### Top Contributors
Most helpful beta testers receive:
- Exclusive beta tester badge
- Early access to future features
- Special Discord role with benefits

## Success Criteria

We consider beta successful when:
- ✅ 5+ beta testers recruited
- ✅ 5+ custom mods created by testers
- ✅ Critical bugs reported and fixed
- ✅ Documentation improved based on feedback
- ✅ Average satisfaction rating >4/5
- ✅ Performance validated with 10+ mods

## Tips for Effective Testing

### Be Creative
- Try unusual mod combinations
- Test edge cases
- Break things intentionally
- Think like an end-user

### Document Everything
- Take screenshots of errors
- Save error logs
- Record reproduction steps
- Note environment details

### Communicate
- Ask questions in Discord
- Share findings early
- Help other testers
- Provide constructive feedback

### Have Fun!
- Experiment with mod ideas
- Share cool creations
- Learn from others
- Enjoy the process

## FAQ

**Q: How much time should I commit?**
A: 2-5 hours per week is ideal, but any contribution helps!

**Q: Do I need to test everything?**
A: No, focus on scenarios that interest you.

**Q: Can I share my mods publicly?**
A: Yes! Share in #beta-showcase.

**Q: What if I find a security issue?**
A: Email security@pokesharp.dev immediately (do not post publicly).

**Q: Can I invite others to beta?**
A: Please ask first - we're limiting beta size.

**Q: Will my feedback be implemented?**
A: We'll consider all feedback, but can't guarantee every request.

**Q: What happens after beta?**
A: Public release! Your contributions will be acknowledged.

## Thank You!

Your participation in this beta is invaluable. You're helping shape the future of PokeSharp modding!

**Questions?** Reach out in Discord or email beta@pokesharp.dev

**Happy Modding!** 🎮✨
