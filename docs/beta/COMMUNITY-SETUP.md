# Beta Community Setup Plan

Plan for establishing and managing community channels during the beta testing period.

## Discord Server Structure

### Channels Overview

```
📢 ANNOUNCEMENTS
  #beta-announcements (read-only) - Updates, milestones, important news

💬 GENERAL
  #beta-welcome - Introduce yourself
  #beta-general - General discussion
  #beta-random - Off-topic chat

🧪 TESTING
  #beta-testing - Testing discussion and coordination
  #beta-support - Get help with issues
  #beta-bugs - Bug reports and tracking
  #beta-feedback - General feedback and suggestions

🎨 SHOWCASE
  #beta-showcase - Share your mods!
  #beta-mod-ideas - Discuss mod concepts
  #beta-collaboration - Find mod partners

📚 RESOURCES
  #beta-docs - Documentation questions
  #beta-snippets - Share code snippets
  #beta-tutorials - Community tutorials
  #beta-faq - Frequently asked questions

🔊 VOICE
  Office Hours (voice)
  Pair Programming (voice)
  Casual Hangout (voice)

🛠️ INTERNAL (Staff Only)
  #staff-general
  #staff-bug-triage
  #staff-feedback-review
```

---

## Channel Details

### #beta-announcements (Read-Only)

**Purpose**: Official updates and important information

**Posted By**: Moderators/Maintainers only

**Content**:
- Beta program milestones
- New beta builds
- Bug fix notifications
- Feature updates
- Weekly summaries
- Office hours schedules
- Important deadlines

**Example Posts**:
```
🎉 Beta Week 1 Complete!

Thanks to all testers for a successful first week:
- 8 active testers
- 15 mods created
- 23 bugs reported
- 100% participation in feedback

Week 2 Focus: Multi-script composition and custom events.

Office hours: Tuesday 2-4 PM EST
```

---

### #beta-welcome

**Purpose**: Introduce new beta testers

**Guidelines**:
- Introduce yourself
- Share your background (C#, game dev, modding)
- What interests you about the beta
- What you hope to create

**Pinned Message**:
```
👋 Welcome to PokeSharp Beta!

Introduce yourself:
- Name/Handle
- Experience (C#, game dev, modding)
- What you're excited to test
- Mod ideas you want to try

Resources:
📖 Beta Guide: [link]
📝 Feedback Form: [link]
🐛 Bug Template: [link]
```

---

### #beta-general

**Purpose**: General beta discussion

**Guidelines**:
- Questions about the program
- General observations
- Non-technical discussion
- Community building

**Topics**:
- Beta timeline
- Testing strategies
- Community events
- Polls and surveys

---

### #beta-testing

**Purpose**: Active testing discussion

**Guidelines**:
- Share testing progress
- Coordinate testing efforts
- Discuss testing scenarios
- Ask technical questions

**Example Discussions**:
- "Just tested hot-reload with 5 mods - works great!"
- "Has anyone tested entity filtering performance?"
- "Let's all test multi-script mods today"

**Pinned Resources**:
```
🧪 Testing Resources

Checklist: /docs/beta/TESTING-CHECKLIST.md
Scenarios: /docs/beta/BETA-TESTING-GUIDE.md#testing-scenarios
Examples: /docs/beta/MOD-SHOWCASE.md

Current Focus: Week 2 - Custom Events
```

---

### #beta-support

**Purpose**: Get help with issues

**Guidelines**:
- Ask for help clearly
- Provide context (OS, .NET version, error messages)
- Share relevant code
- Help others when you can

**Support Categories**:
- 🔧 **Setup Issues**: Installation, build errors
- 💻 **Code Help**: API questions, syntax
- 🐛 **Bug Help**: Reproducing bugs, workarounds
- 📖 **Docs Help**: Finding information

**Pinned Message**:
```
🆘 Need Help?

Before asking:
1. Check FAQ: [link]
2. Search Discord history
3. Review documentation

When asking:
✅ OS and .NET version
✅ Error messages
✅ Code that's failing
✅ What you've tried

Response time: Usually <1 hour during office hours
```

---

### #beta-bugs

**Purpose**: Report and track bugs

**Guidelines**:
- Use bug report template
- Search for duplicates first
- Include reproduction steps
- Update if resolved

**Process**:
1. Tester posts bug with template
2. Staff adds emoji reactions:
   - 👀 = Acknowledged
   - 🔄 = In progress
   - ✅ = Fixed
   - ❌ = Won't fix / Duplicate
3. Staff creates GitHub issue if needed
4. Tester verifies fix

**Pinned Message**:
```
🐛 Bug Reporting

Template: /docs/beta/BUG-REPORT-TEMPLATE.md

Severity:
🔴 Critical - Crashes, data loss
🟠 High - Core feature broken
🟡 Medium - Has workaround
🟢 Low - Minor issue

Before reporting:
- Search for duplicates
- Try reproducing
- Collect logs/screenshots
```

---

### #beta-feedback

**Purpose**: General feedback and suggestions

**Guidelines**:
- Constructive feedback
- Be specific
- Explain why/how it would help
- Discuss others' feedback

**Categories**:
- 💡 Feature requests
- 📝 Documentation feedback
- 🎨 API design feedback
- 🔧 Tool requests
- ⚡ Performance feedback

---

### #beta-showcase

**Purpose**: Share mods and creations

**Guidelines**:
- Share working mods
- Explain what it does
- Include code or link
- Demo with screenshot/GIF/video
- React to others' work

**Showcase Format**:
```
🎮 [Mod Name]

**What it does**: Brief description

**Features**:
- Feature 1
- Feature 2

**Code**: [link or snippet]

**Demo**: [screenshot/GIF]

**Try it**: [installation instructions]
```

**Example**:
```
🎮 Shiny Hunter Mod

**What it does**: Tracks shiny encounters and plays special sound

**Features**:
- 1/4096 shiny rate
- Sparkle animation
- Shiny counter
- Achievement at 10 shinies

**Code**: https://github.com/...

**Demo**: [GIF of shiny encounter]

**Try it**: Copy ShinyHunter.csx to /mods/
```

---

### #beta-mod-ideas

**Purpose**: Discuss mod concepts and brainstorm

**Guidelines**:
- Share ideas even if not implemented
- Ask for feedback on concepts
- Collaborate on ideas
- No idea is too silly

**Discussion Format**:
```
💡 Mod Idea: [Name]

**Concept**: What it would do

**How it works**:
1. ...
2. ...

**Questions**:
- Is this possible with current API?
- Any suggestions?

**Looking for**: Collaborators / Feedback / Help
```

---

### #beta-collaboration

**Purpose**: Find partners for mod projects

**Guidelines**:
- Post what you're looking for
- Offer your skills
- Team up on projects
- Share work-in-progress

**Example Post**:
```
🤝 Looking to collaborate!

**Project**: Biome-based encounter system

**Skills needed**:
- Data modeling (encounter tables)
- State management
- Testing

**I can contribute**: Event system expertise, performance optimization

**Timeline**: Week 2-3 of beta

Interested? DM or reply! 👇
```

---

### #beta-docs

**Purpose**: Documentation questions and improvements

**Guidelines**:
- Ask specific documentation questions
- Report doc errors/gaps
- Suggest improvements
- Share helpful resources

**Topics**:
- "Where can I find info on X?"
- "This section is confusing: [link]"
- "Suggestion: Add example for Y"

---

### #beta-snippets

**Purpose**: Share useful code snippets

**Guidelines**:
- Share reusable code
- Explain what it does
- Keep snippets focused
- Credit sources

**Example**:
```csharp
// Useful timer pattern
public struct TimerState
{
    public float Elapsed;
    public float Duration;
    public bool Active;
}

// Use in Initialize():
Set("timer", new TimerState { Duration = 5.0f });

// Use in TickEvent:
var timer = Get<TimerState>("timer", default);
if (timer.Active) {
    timer.Elapsed += evt.DeltaTime;
    if (timer.Elapsed >= timer.Duration) {
        // Timer complete!
        timer.Active = false;
    }
    Set("timer", timer);
}
```

---

### #beta-tutorials

**Purpose**: Community-created tutorials

**Guidelines**:
- Share learning resources
- Write mini-tutorials
- Explain tricky concepts
- Link to external resources

**Example Tutorial**:
```
📚 Tutorial: State Machines in PokeSharp

State machines are perfect for complex behaviors like sliding on ice.

**Step 1: Define States**
```csharp
public enum SlideState { Idle, Sliding, Stopped }
```

**Step 2: Create State Data**
```csharp
public struct SlideData {
    public SlideState State;
    public int Direction;
}
```

**Step 3: Implement Transitions**
[full code example]

**When to use**: Multi-step behaviors, complex AI, puzzle mechanics
```

---

### #beta-faq

**Purpose**: Common questions and answers

**Maintained By**: Community + Moderators

**Format**:
```
Q: How do I filter events by entity?
A: Use OnEntity<EventType>(entity, handler)

Q: Why did my mod stop working after hot-reload?
A: State resets on hot-reload. This is expected behavior.

Q: Can I publish custom events?
A: Yes! Use Publish(new MyCustomEvent { ... })
```

**Categories**:
- Setup & Installation
- API Usage
- Hot-Reload
- Event System
- State Management
- Performance
- Debugging

---

## Voice Channels

### Office Hours (Tuesday & Thursday, 2-4 PM EST)

**Purpose**: Live support from maintainers

**Format**:
- Drop-in style (join anytime)
- Ask questions
- Pair programming help
- Demo features
- Discuss feedback

**Recording**: Yes (for those who can't attend)

---

### Pair Programming

**Purpose**: Code together in real-time

**Usage**:
- Schedule sessions
- Screen sharing
- Collaborative debugging
- Learning sessions

---

### Casual Hangout

**Purpose**: Social time, no agenda

**Usage**:
- Game together
- Chat about anything
- Build community

---

## Moderation Guidelines

### Code of Conduct

**Expected Behavior**:
- ✅ Be respectful and constructive
- ✅ Help others
- ✅ Stay on-topic in focused channels
- ✅ Provide detailed bug reports and feedback
- ✅ Credit others' work

**Prohibited Behavior**:
- ❌ Harassment or discrimination
- ❌ Spam or excessive self-promotion
- ❌ Sharing private beta materials publicly
- ❌ Malicious code or exploits
- ❌ Off-topic disruption in focused channels

### Moderator Roles

**Beta Lead**:
- Oversees program
- Makes decisions
- Handles escalations

**Community Moderators** (2-3):
- Answer questions
- Triage bugs
- Keep channels organized
- Enforce code of conduct

**Technical Experts** (optional):
- API questions
- Architecture discussions
- Performance optimization

---

## Moderation Tools

### Channel Management
- Pin important messages
- Archive resolved bug reports
- Create threads for long discussions
- Use reactions for status tracking

### Emoji System

**Bug Tracking**:
- 👀 = Acknowledged
- 🔄 = In Progress
- ✅ = Fixed
- ❌ = Closed (won't fix/duplicate)
- 🔥 = Critical

**Feedback**:
- 💡 = Great idea
- 🤔 = Under consideration
- ✅ = Planned
- 📋 = Backlog

**Community**:
- 🎉 = Achievement
- 🌟 = Exceptional contribution
- 🚀 = Launch-ready mod

---

## Engagement Strategies

### Daily Standup (Async)

Post in #beta-testing:
```
Good morning testers! 🌅

**Yesterday's Highlights**:
- Alice created shiny tracker mod 🌟
- Bob found critical hot-reload bug 🐛
- 3 new mods in #beta-showcase

**Today's Focus**: Custom events testing

**Question of the Day**: What mod are you most proud of?
```

### Weekly Recap

Post in #beta-announcements:
```
📊 Week 2 Recap

**Progress**:
- 10 mods created
- 18 bugs fixed
- 5 feature requests added to backlog

**Top Contributors** this week:
🥇 Alice - 8 bugs reported
🥈 Bob - 3 mods created
🥉 Carol - Excellent feedback on API

**Next Week**: Multi-script composition focus

**Office Hours**: Tue/Thu 2-4 PM EST
```

### Recognition

**Badges/Roles**:
- 🎖️ Beta Tester (all participants)
- 🌟 Bug Hunter (10+ bugs)
- 🛠️ Mod Creator (3+ mods)
- 📚 Doc Contributor (doc improvements)
- 🏆 Top Contributor (overall excellence)

**Shoutouts**:
- Daily highlight in #beta-testing
- Weekly spotlight in #beta-announcements
- Featured mods in #beta-showcase

---

## Communication Cadence

### From Maintainers

**Daily**:
- Monitor #beta-support
- Triage bugs
- Answer questions
- React to showcases

**Weekly**:
- Monday: Week kickoff post
- Tuesday/Thursday: Office hours
- Friday: Week recap
- Update FAQ

**Monthly**:
- Program milestone update
- Feedback synthesis
- Roadmap adjustments

### From Testers

**Expected**:
- Active in channels (as available)
- Weekly testing progress update
- Bug reports as found
- End-of-week feedback

**Optional**:
- Daily check-ins
- Help other testers
- Office hours attendance
- Mod showcases

---

## Success Metrics

### Engagement
- Daily active users (target: 60%+)
- Messages per day (target: 20+)
- Office hours attendance (target: 4+ per session)
- Showcase posts (target: 2+ per week)

### Quality
- Bug report quality (complete templates)
- Feedback specificity
- Constructive discussions
- Helpful community interactions

### Outcomes
- Bugs reported and fixed
- Mods created
- Documentation improvements
- Feature requests

---

## FAQ Setup

### Top 10 FAQs (to be created)

1. **How do I install PokeSharp?**
2. **What is ScriptBase?**
3. **How does hot-reload work?**
4. **How do I filter events by entity?**
5. **Why did my mod break after hot-reload?**
6. **How do I publish custom events?**
7. **What's the difference between On vs OnEntity?**
8. **How do I debug my mod?**
9. **Can I use external libraries?**
10. **How do I test multiple mods together?**

### FAQ Maintenance
- Update based on common questions
- Add new questions weekly
- Remove outdated answers
- Link to detailed docs

---

## Tools & Bots (Optional)

### Useful Bots

**MEE6** (Free):
- Reaction roles
- Custom commands (!docs, !bug-template)
- Leveling/XP system

**Dyno** (Free):
- Auto-moderation
- Custom embeds
- Announcement posting

**GitHub Bot**:
- Auto-post new issues
- Link PRs
- Notify on releases

---

## Backup Communication

### If Discord Down

**Primary Backup**: Email list
- Send updates to all beta testers
- Use email for urgent communications

**Secondary Backup**: GitHub Discussions
- Post updates
- Track async discussions

---

## Timeline

| Week | Community Actions |
|------|-------------------|
| Pre-beta | Create channels, write guidelines, prepare FAQs |
| Week 1 | Onboarding, welcome messages, first office hours |
| Week 2-3 | Daily engagement, weekly recaps, showcase highlights |
| Week 4 | Feedback collection focus, recognition posts |
| Week 5 | Thank you messages, final showcase, awards |

---

## Post-Beta

### Channel Transition
- Archive beta channels
- Create public channels
- Migrate FAQs
- Keep beta channel history for reference

### Community Growth
- Transition to public Discord
- Promote from Reddit/Twitter
- Launch community showcase
- Continue office hours

---

**Questions?** Contact community lead: [Discord handle] or [email]

**Let's build an amazing community!** 🚀
