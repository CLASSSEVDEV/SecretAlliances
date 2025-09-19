# Secret Alliances v2.0

A comprehensive Mount & Blade II: Bannerlord mod that adds secret alliances, formal pacts, battle assistance, and intelligent AI decision-making to the campaign layer.

## 🚀 **CRITICAL BUG FIX** 

**v2.0 completely eliminates the kingdom-switching bug** that forced players to leave their kingdom when assisting allies. The mod now uses a sophisticated covert assistance system that maintains your kingdom allegiance while still providing meaningful alliance benefits.

## Features

### 🤝 Secret Alliances
- **Multi-clan secret alliances** with proper leadership structure
- **Trust and secrecy mechanics** that evolve based on actions
- **Cross-kingdom alliances** for complex political maneuvering
- **Alliance history tracking** with detailed logs of all events

### ⚔️ Battle Assistance System (**BUG FIXED**)
- **Request/offer assistance** for battles, sieges, and raids
- **Covert assistance** that doesn't force kingdom changes
- **Risk/reward calculations** with exposure consequences
- **AI-driven assistance requests** based on circumstances

### 📜 Formal Pacts & Treaties
- **Military Pacts** for formalized cooperation
- **Non-Aggression Pacts** for diplomatic stability
- **Trade Agreements** for economic benefits
- **Intelligence Sharing** for strategic advantage

### 🕵️ Leak & Exposure System
- **Dynamic secrecy levels** that change based on actions
- **Leak probability calculations** considering clan traits and circumstances
- **Political consequences** when alliances are discovered
- **Counter-intelligence investments** to protect your secrets

### 🤖 Intelligent AI
- **Utility-based decision making** for all AI clans
- **Trait-driven personality** affecting alliance decisions
- **Strategic considerations** including power balance and relationships
- **Rational betrayal mechanics** (rare, heavily penalized for honorable characters)

### 💬 Dialog Integration
- **Natural conversation flow** for alliance proposals
- **Status checking** for existing alliances
- **Assistance requests** through dialog system
- **Clear feedback** on acceptance/rejection reasons

## Installation

1. Extract to your Bannerlord Modules folder: `Mount & Blade II Bannerlord/Modules/SecretAlliances/`
2. Enable the mod in the launcher
3. Start a new campaign or load an existing save

**Compatibility**: Bannerlord v1.2.9, .NET Framework 4.7.2

## Usage Guide

### Creating Secret Alliances

1. Talk to a clan leader and select diplomatic options
2. Choose "I'd like to discuss a... private arrangement between our clans"
3. The AI will evaluate based on:
   - Your relationship with the clan leader
   - Power balance between clans
   - Current political situation
   - Leader personality traits

### Managing Alliances

- **View Status**: Talk to allied clan leaders to check trust levels, secrecy, and recent activities
- **Request Assistance**: Ask for help with battles, raids, or other needs
- **Leave Alliance**: End arrangements that no longer serve your interests
- **Monitor Exposure**: Watch for rumors and leaks that might compromise your secrets

### Battle Assistance (No More Kingdom Switching!)

The new system works as follows:
1. When allies are in battle, you may receive assistance requests
2. Accepting provides **covert support** without changing your kingdom
3. Your assistance affects battle outcomes while maintaining your allegiance
4. Risk of exposure depends on battle size, location, and witnesses
5. Consequences (if discovered) include relationship penalties, not forced exile

### AI Behavior

AI clans make decisions based on:
- **Strategic Value**: Military strength, economic benefits, territorial advantages
- **Relationships**: Personal bonds between leaders
- **Personality Traits**: Honor affects betrayal likelihood, Calculating affects alliance formation
- **Circumstances**: Wars, financial pressure, recent events
- **Risk Assessment**: Exposure probability, political consequences

## Console Commands (for debugging)

- `sa.createAlliance [clan1] [clan2]` - Force create alliance between clans
- `sa.dumpAlliances` - Show all active alliances
- `sa.listIntel` - Display intelligence/leak information
- `sa.forceLeaks [alliance]` - Trigger leak for testing

## Technical Details

### Architecture

The mod uses a clean, modular architecture:

```
/Behaviors/
├── AllianceService.cs          # Core alliance management
├── RequestsBehavior.cs         # Assistance request system
├── PreBattleAssistBehavior.cs  # Battle assistance (no kingdom switching)
├── LeakBehavior.cs            # Secrecy and exposure mechanics
└── AiDecisionBehavior.cs      # Intelligent AI decision making

/Models/
├── Alliance.cs                # Alliance data structure
├── Pact.cs                   # Formal pact definitions
├── Request.cs                # Assistance request structure
└── UtilityModel.cs           # AI decision utilities
```

### Save/Load Compatibility

All new data structures use proper MBSaveLoad annotations for save game compatibility. The mod can be safely added to existing campaigns.

### Performance

- Optimized for O(n) complexity on daily ticks
- AI decisions distributed across multiple days to prevent lag
- Automatic cleanup of old data to prevent memory bloat
- Configurable decision frequency to balance realism and performance

## Configuration

The mod includes configurable settings (future UI integration planned):

- AI decision frequency
- Leak probability multipliers
- Exposure consequence severity
- Alliance size limits
- Request timeout durations

## Known Issues & Limitations

- UI integration is in progress (currently uses dialog system)
- Some advanced features (economic warfare, spy networks) are scaffolded
- Localization limited to English
- Testing needed for large-scale campaign performance

## Version History

### v2.0.0 (Current)
- **CRITICAL**: Fixed kingdom-switching bug in battle assistance
- Complete architectural rewrite with clean separation of concerns
- Implemented utility-based AI decision system
- Added comprehensive leak/exposure mechanics
- Improved dialog integration
- Added formal pact system

### v1.x (Legacy)
- Original implementation with basic alliance features
- Known kingdom-switching bug in battle assistance

## Contributing

This mod is designed with extensibility in mind. The modular architecture makes it easy to:
- Add new alliance types
- Implement additional AI behaviors
- Create custom UI screens
- Extend the utility model for new decision types

## Credits

Built for Mount & Blade II: Bannerlord v1.2.9 using the official modding API. Compatible with .NET Framework 4.7.2 and C# 7.3.

## Support

For issues, suggestions, or contributions, please use the GitHub repository issue tracker.

---

**🛡️ The kingdom-switching bug is FIXED! Enjoy secret alliances without losing your kingdom allegiance!**