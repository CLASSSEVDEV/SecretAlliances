# Secret Alliances - Mega Feature Expansion

A Mount & Blade II: Bannerlord mod that adds sophisticated secret alliance mechanics with intelligence gathering, operations, betrayal systems, and complex political dynamics.

## Features Overview

### Core Alliance System
- **Secret Alliance Formation**: AI-driven formation based on mutual enemies, economic factors, and desperation
- **Trust & Secrecy Mechanics**: Dynamic trust building and secrecy maintenance with decay over time
- **Coalition Support**: Multi-clan alliances with group cohesion mechanics

### Operations System
Five distinct operation types with cooldowns and adaptive scheduling:

1. **Covert Aid** - Provides strength and trust boosts with moderate secrecy risks
2. **Spy Probe** - Gather intelligence on enemy clans with success/failure consequences  
3. **Recruitment Feelers** - Recruit new clans into existing alliances or coalitions
4. **Sabotage Raid** - Virtual sabotage operations against rival clans
5. **Counter-Intelligence** - Reduce enemy intel reliability and provide leak protection

### Intelligence & Leak System
- **Logistic Leak Probability**: Leak chances increase with each attempt, creating escalating risk
- **Intelligence Categories**: General rumors, trade evidence, military coordination, secret meetings, betrayal plots
- **Rumor API**: Public methods for retrieving rumors (`GetTopRumorString`, `GetRumorList`)
- **Intelligence Aging**: Reports become less reliable over time and eventually expire

### Betrayal & Defection
- **Pre-Battle Betrayal**: Alliances can break during battles when allied clans fight on opposing sides
- **Escalation Counter**: Near-miss betrayals increase future betrayal probability
- **Betrayal Cooldown**: 30-day cooling period after betrayal events
- **Political Pressure**: External factors influence betrayal likelihood

### Forced Revelation System
- **Strength/Secrecy Thresholds**: Very strong but poorly hidden alliances are automatically exposed
- **Kingdom-Wide Intelligence**: Exposed alliances generate intelligence for entire kingdoms
- **Reputation Consequences**: Revealed alliances suffer significant secrecy and trust penalties

### Configuration System
- **JSON Configuration**: `SecretAlliancesConfig.json` in module root with comprehensive settings
- **Runtime Generation**: Config file auto-created with sensible defaults if missing
- **Fallback Mechanisms**: Multiple path detection strategies for robust deployment

### Debug & Console Integration
- **Debug Console Commands**: Optional command system guarded behind config flag
- **Verbose Logging**: Detailed operation logging when debug mode enabled
- **Self-Diagnostics**: Alliance statistics and health checks on game load
- **Integrity Repair**: Automatic cleanup of duplicate alliances and missing data

### Player Notifications
- **Major Event Alerts**: Configurable notifications for alliance formations, betrayals, dissolutions
- **Alliance Status Updates**: Real-time feedback on significant alliance changes
- **Rumor Integration**: Seamless integration with existing game notification systems

## Configuration

The mod auto-generates a `SecretAlliancesConfig.json` file in the module root directory with the following key settings:

```json
{
  "FormationBaseChance": 0.07,
  "MaxDailyFormations": 3,
  "OperationIntervalDays": 7,
  "BetrayalBaseChance": 0.005,
  "DebugEnableConsoleCommands": false,
  "NotificationsEnabled": true,
  "MaxRumorsReturned": 3
}
```

## Technical Implementation

### Save Compatibility
- **Append-Only Fields**: New saveable fields (32-37) appended without disturbing existing saves
- **Optional Defaults**: All new fields have safe default values for backward compatibility
- **Version Resilience**: Graceful handling of missing configuration or data

### Performance Considerations
- **Efficient Algorithms**: O(n) or better complexity for most daily operations
- **Lazy Loading**: Intelligence and operation data loaded only when needed
- **Memory Management**: Automatic cleanup of expired intelligence and inactive alliances

### Integration Points
- **Campaign Events**: Hooks into daily ticks, battle events, political changes
- **Hero & Clan Systems**: Deep integration with existing game objects
- **Kingdom Mechanics**: Respects existing diplomatic and political systems

## Installation

1. Place the mod folder in your Bannerlord `Modules` directory
2. Enable the mod in the launcher
3. Start a campaign - configuration will be auto-generated on first run

## Development

### Adding New Operations
Operations are defined in the `OperationType` enum and implemented in the corresponding `Execute*` methods. Each operation should:
- Respect cooldown timings from configuration
- Generate appropriate intelligence on success/failure
- Update alliance statistics (strength, trust, secrecy)
- Log actions when debug verbose is enabled

### Extending Intelligence Types
New intelligence categories can be added to the `AllianceIntelType` enum. Remember to:
- Update the rumor generation logic in `GenerateRumorText`
- Consider reliability and severity calculations
- Add appropriate aging mechanisms

### Console Commands
When `DebugEnableConsoleCommands` is enabled, commands can be registered in the `RegisterConsoleCommands` method. Ensure all debug commands are properly guarded and provide helpful feedback.

---

*This mod significantly expands the political depth of Mount & Blade II: Bannerlord by introducing realistic secret alliance mechanics with full intelligence gathering, operation systems, and complex betrayal dynamics.*