# Secret Alliances - Debug Guide

This guide covers debugging, testing, and configuration tuning for the Secret Alliances Mega Feature Expansion.

## Console Commands

All console commands use the prefix `sa.` and can be executed in-game via the developer console (Ctrl+~ or configured key).

### Basic Information Commands

#### `sa.dumpAlliances`
Lists all active alliances with core statistics.

**Usage:** `sa.dumpAlliances`

**Output Example:**
```
[SecretAlliances] Active Alliances (3):
  Clan_A <-> Clan_B: S=0.45, Sec=0.73, Trust=0.62, Group=1, Pacts=[TM], Ops=7
  Clan_C <-> Clan_D: S=0.31, Sec=0.89, Trust=0.28, Group=0, Pacts=[T], Ops=2
```

**Legend:**
- S = Strength, Sec = Secrecy, Trust = Trust Level
- Pacts: T=Trade, M=Military
- Ops = Successful Operations count

#### `sa.listIntel [clanStringId]`
Display intelligence records, optionally filtered by clan.

**Usage:** 
- `sa.listIntel` - Show all intelligence
- `sa.listIntel empire_south` - Show intelligence involving empire_south

**Output Example:**
```
[SecretAlliances] Intelligence (5 entries):
  Clan_A <-> Clan_B: Rel=0.73, Sev=0.85, Age=12d, Cat=5, Informer=Lord_X
  Clan_C <-> Clan_D: Rel=0.62, Sev=0.45, Age=3d, Cat=2, Informer=Lady_Y
```

### Testing and Manipulation Commands

#### `sa.createAlliance clanA clanB`
Create a test alliance between two clans.

**Usage:** `sa.createAlliance empire_south khuzait_khan`

Creates an alliance with randomized starting values:
- Strength: 0.2-0.4
- Secrecy: 0.6-0.9  
- Trust: 0.3-0.5

#### `sa.forceLeak clanA clanB`
Force a leak generation for an existing alliance.

**Usage:** `sa.forceLeak empire_south khuzait_khan`

Immediately generates intelligence based on:
- Current alliance pact types
- Informer reliability
- Severity calculation model

#### `sa.addTrust clanA clanB amount`
Modify trust levels for testing alliance dynamics.

**Usage:** 
- `sa.addTrust empire_south khuzait_khan 0.2` - Add 20% trust
- `sa.addTrust empire_south khuzait_khan -0.1` - Reduce 10% trust

Trust levels are automatically clamped between 0.0 and 1.0.

#### `sa.runOperation clanA clanB opType`
Execute specific operations for testing.

**Usage:** `sa.runOperation empire_south khuzait_khan 2`

**Operation Types:**
1. **CovertAid** - Low risk support operation
2. **SpyProbe** - Intelligence gathering on third parties  
3. **RecruitmentFeelers** - Attempt to recruit new coalition member
4. **SabotageRaid** - High-risk sabotage against enemies
5. **CounterIntelligence** - Reduce existing intelligence reliability

#### `sa.forceReveal clanA clanB`
Force alliance revelation for testing forced reveal mechanics.

**Usage:** `sa.forceReveal empire_south khuzait_khan`

Effects:
- Sets BetrayalRevealed flag
- Generates high-severity intelligence for all related kingdom clans
- Demonstrates forced reveal consequences

### Configuration Management

#### `sa.config`
Display all configuration values.

**Usage:** `sa.config`

**Output Example:**
```
[SecretAlliances] Configuration:
  FormationBaseChance: 0.050
  MaxDailyFormations: 2
  OperationIntervalDays: 15
  LeakBaseChance: 0.008
  TradeFlowMultiplier: 1.50
  BetrayalBaseChance: 0.020
  DebugVerbose: False
```

#### `sa.config property`
Show specific property value.

**Usage:** `sa.config FormationBaseChance`

#### `sa.config property value`
Modify configuration values at runtime.

**Usage:** 
- `sa.config FormationBaseChance 0.1` - Double alliance formation rate
- `sa.config DebugVerbose true` - Enable verbose logging
- `sa.config LeakBaseChance 0.02` - Increase leak probability

Changes are automatically validated, clamped, and saved to the JSON file.

## Configuration Tuning

### Core Alliance Formation
```json
{
  "FormationBaseChance": 0.05,     // 5% daily base chance per clan
  "MaxDailyFormations": 2,         // Global limit to prevent explosion
}
```

**Tuning Notes:**
- Increase FormationBaseChance for more active alliance formation
- Higher MaxDailyFormations creates more complex political webs
- Monitor with `sa.dumpAlliances` to avoid too many simultaneous alliances

### Operations Framework
```json
{
  "OperationIntervalDays": 15,        // Base days between operations
  "OperationAdaptiveMinDays": 7,      // Minimum interval under pressure
  "SpyProbeCooldownDays": 20,         // Individual operation cooldowns
  "SabotageCooldownDays": 30,
  "CounterIntelCooldownDays": 25,
  "RecruitmentCooldownDays": 45
}
```

**Tuning Notes:**
- Lower OperationIntervalDays for more frequent operations
- Adjust individual cooldowns to balance operation frequency
- Monitor operation success rates and adjust difficulty in code if needed

### Leak and Intelligence System
```json
{
  "LeakBaseChance": 0.008,           // 0.8% daily base leak chance
  "ForcedRevealStrengthThreshold": 0.8,
  "ForcedRevealSecrecyThreshold": 0.2
}
```

**Tuning Notes:**
- Higher LeakBaseChance creates more intelligence generation
- Adjust ForcedReveal thresholds to control when powerful alliances are exposed
- Use `sa.listIntel` to monitor intelligence volume

### Economic System
```json
{
  "TradeFlowMultiplier": 1.5,        // Gold transfer rate multiplier
}
```

**Tuning Notes:**
- Higher TradeFlowMultiplier increases economic integration
- Monitor clan gold levels to ensure transfers don't break game economy
- Financial intelligence generation tied to transfer magnitude

### Betrayal and Defection
```json
{
  "BetrayalBaseChance": 0.02,        // 2% base betrayal chance
}
```

**Tuning Notes:**
- Lower values create more stable alliances
- Higher values increase political volatility
- Betrayal escalation system automatically increases chances over time

### Coalition Cohesion
```json
{
  "CohesionStrengthFactor": 0.6,     // Weight of strength in cohesion
  "CohesionSecrecyFactor": 0.4       // Weight of secrecy in cohesion
}
```

**Tuning Notes:**
- Adjust factor balance to emphasize strength vs secrecy importance
- Higher cohesion provides performance bonuses to coalition members

## Test Scenarios

### Basic Alliance Testing

1. **Create Test Alliance**
   ```
   sa.createAlliance empire_south khuzait_khan
   sa.dumpAlliances
   ```

2. **Monitor Natural Formation**
   ```
   sa.config FormationBaseChance 0.2
   # Wait several days in-game
   sa.dumpAlliances
   ```

3. **Test Operations**
   ```
   sa.runOperation empire_south khuzait_khan 1  # CovertAid
   sa.runOperation empire_south khuzait_khan 2  # SpyProbe
   sa.listIntel
   ```

### Intelligence System Testing

1. **Generate Leaks**
   ```
   sa.forceLeak empire_south khuzait_khan
   sa.listIntel empire_south
   ```

2. **Test Aging**
   ```
   sa.listIntel
   # Wait 30+ days in-game
   sa.listIntel  # Should show aged intelligence
   ```

3. **Counter-Intelligence**
   ```
   sa.runOperation empire_south khuzait_khan 5  # CounterIntel
   sa.listIntel  # Should show reduced reliability
   ```

### Coalition Formation Testing

1. **Multi-Alliance Creation**
   ```
   sa.createAlliance empire_south khuzait_khan
   sa.createAlliance empire_south battania_clan_1
   sa.dumpAlliances  # Should show same GroupId if coalition formed
   ```

2. **Recruitment Operations**
   ```
   sa.runOperation empire_south khuzait_khan 3  # Recruitment
   sa.dumpAlliances  # May show new alliance in same group
   ```

### Economic Integration Testing

1. **Enable Trade Pacts** (via normal dialog system)
   ```
   # Create trade pact through in-game dialog
   sa.dumpAlliances  # Should show [T] in pacts
   # Monitor clan gold levels over time
   ```

2. **Monitor Financial Intelligence**
   ```
   sa.listIntel
   # Look for Financial category (Cat=7) entries
   ```

### Betrayal Testing

1. **Force Betrayal Conditions**
   ```
   sa.addTrust empire_south khuzait_khan -0.4  # Reduce trust
   sa.config BetrayalBaseChance 0.1           # Increase betrayal rate
   # Wait for betrayal evaluation (monitor logs)
   ```

2. **Forced Reveal Testing**
   ```
   sa.addTrust empire_south khuzait_khan 0.5   # High trust/strength
   # Wait for forced reveal (monitor logs)
   sa.forceReveal empire_south khuzait_khan    # Manual trigger
   sa.listIntel  # Should show high-severity General intel
   ```

## Debugging Rumor System

### Enable Verbose Logging
```
sa.config DebugVerbose true
```

### Test Rumor Access
The rumor system requires specific alliance connections:

1. **Player Must Have Alliance** with one party
2. **Target Must Have Alliance** with same party OR be in same coalition group
3. **Use Dialog System** to test rumor options

### Rumor Gating Diagnosis

Check alliance connections:
```
sa.dumpAlliances
# Verify player clan appears in alliance list
# Verify target clan shares Group ID or direct alliance with player
```

Rumor content requires:
- Reliability ≥ 0.25
- Severity > 0  
- Player clan involvement (ClanAId or ClanBId)
- Matching coalition groups OR direct alliance connection

## Performance Monitoring

### Daily Processing Load
Monitor log output for performance warnings:
- Large numbers of alliance evaluations
- Excessive intelligence generation
- Coalition cohesion calculation times

### Memory Usage
The system uses ephemeral storage for:
- Operation cooldowns (auto-cleanup)
- Trade transfer history (capped at 20 entries per alliance)

### Save File Impact
Persistent data includes:
- Alliance records (minimal size increase)
- Intelligence records (auto-cleanup after 180 days)
- New SaveableFields 32-35 (minimal impact)

## Common Issues and Solutions

### Alliance Formation Not Working
1. Check `sa.config FormationBaseChance` - may be too low
2. Verify `MaxDailyFormations` not exceeded
3. Check clan elimination status
4. Monitor with `DebugVerbose=true`

### No Intelligence Generated
1. Verify alliances exist with `sa.dumpAlliances`
2. Check `LeakBaseChance` configuration
3. Force test with `sa.forceLeak`
4. Monitor intelligence aging with `sa.listIntel`

### Operations Not Executing
1. Check operation cooldowns (ephemeral, reset on reload)
2. Verify `OperationIntervalDays` and `NextEligibleOperationDay`
3. Test manually with `sa.runOperation`

### Rumors Not Appearing
1. Verify alliance connections between player and target clans
2. Check coalition group membership
3. Ensure intelligence exists with sufficient reliability
4. Test access control with `ShouldShowRumorOption` logic

### Performance Issues
1. Reduce `FormationBaseChance` and `MaxDailyFormations`
2. Increase operation intervals
3. Monitor alliance count with regular `sa.dumpAlliances`
4. Enable `DebugVerbose` temporarily to identify bottlenecks

This comprehensive debug system allows for thorough testing and tuning of all alliance mechanics, ensuring robust and balanced gameplay integration.