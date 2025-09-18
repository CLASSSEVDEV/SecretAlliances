# SecretAlliances Mod - Changelog

## v2.0.0 - Complete Rewrite (Current Development)

### 🚨 Critical Fixes
- **Battle Assist Bug Fixed**: Eliminated forced kingdom switching when helping allies in battle
  - Player clan now excluded from automatic defection logic
  - Players maintain full control over kingdom allegiance
  - Battle assistance works without unwanted political consequences

### 🏗️ Architecture Improvements
- **Project Structure Reorganized**: Clean namespace separation
  - `SecretAlliances.Core`: Domain models, configuration, persistence, utilities
  - `SecretAlliances.Campaign`: Game logic, AI, behaviors, events
  - `SecretAlliances.UI`: User interface components and display logic
  - `SecretAlliances`: Main module entry point
- **Build System Enhanced**: Flexible .csproj with environment variable support
  - `BannerlordPath` environment variable support
  - Multiple fallback paths for different Steam installations
  - `Private=False` prevents unnecessary DLL copying

### 🧠 Enhanced AI System
- **Utility-Based Decision Making**: Rational AI for alliance formation and betrayal
  - Military utility: Strength balance, geographic coverage, common enemies
  - Economic utility: Wealth compatibility, trade benefits, settlement synergy
  - Political utility: Relationships, kingdom alignment, cultural factors
  - Security utility: Threat assessment, protection value, network effects
- **Personality Integration**: Honor and calculating traits affect decisions
- **Smart Aid System**: Context-aware aid requests and provision decisions
- **Player Autonomy**: AI never makes decisions for player clan

### 🎨 UI Integration
- **Alliance Screen Manager**: Enhanced information display system
- **Conversation Integration**: New dialog options for alliance analysis
- **Detailed Information**: Clan and kingdom alliance overviews
- **Threat Assessment**: Kingdom stability analysis and risk evaluation

### ⚙️ Configuration System
- **Robust Config Loading**: Never fails, always provides defaults
- **JSON Configuration**: Easily customizable game parameters
- **Safe Fallbacks**: Graceful handling of missing or corrupt config files

### 🛡️ Stability Improvements
- **Error Handling**: Comprehensive try-catch blocks prevent crashes
- **Null Safety**: Defensive programming throughout codebase
- **Save Compatibility**: Proper serialization for all new systems
- **Performance**: Efficient algorithms and caching where appropriate

### 📚 Documentation
- **README**: Complete setup and building instructions
- **Code Comments**: Extensive documentation for maintainability
- **Debug Logging**: Configurable verbose logging for troubleshooting

### 🔧 Technical Specifications
- **Compatibility**: Mount & Blade II: Bannerlord v1.2.9
- **Framework**: .NET Framework 4.7.2, C# 7.3
- **API Usage**: Uses only v1.2.9 game API as specified
- **Save System**: Full integration with Bannerlord's save system

### ✨ Key Features Working
- ✅ Secret alliance formation with NPC creation/management
- ✅ Military pacts and trade agreements with AI usage
- ✅ Reputation system and secrecy mechanics
- ✅ Exposure risks and consequences when discovered
- ✅ Battle assistance without kingdom switching (FIXED)
- ✅ Utility-based AI for realistic decision making
- ✅ Proper conversation system with feedback
- ✅ Stable save/load functionality
- ✅ Performance-optimized with toggleable logging

### 🚧 Planned Future Enhancements
- [ ] Full Gauntlet UI screens (currently using information messages)
- [ ] Enhanced event notification system
- [ ] Additional treaty types and diplomatic options
- [ ] Advanced spy network mechanics
- [ ] Coalition warfare mechanics

### 🔄 Migration from v1.x
- All existing save games remain compatible
- Enhanced features activate automatically
- No manual migration required
- Original functionality preserved with improvements

---

## Development Philosophy

This rewrite follows a **minimal change strategy** to maintain stability and save compatibility while addressing critical issues and adding requested features. The code is organized for maintainability and follows Bannerlord modding best practices.

### Key Principles:
1. **Player Agency**: Never force unwanted actions on the player
2. **Rational AI**: NPCs make believable, utility-based decisions
3. **Save Compatibility**: All changes preserve existing save games
4. **Performance**: Efficient algorithms that don't impact game performance
5. **Maintainability**: Clean code structure for future development