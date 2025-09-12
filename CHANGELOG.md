# Secret Alliances - Changelog

## Mega Feature Expansion (v2.0.0)

This major update implements a comprehensive expansion of the Secret Alliances system with 16 major feature areas:

### 1. Alliance Behavior Core Enhancements
- ✅ **Initialization Self-Diagnostics**: First-day diagnostics report active alliance count, average strength/secrecy, coalition groups
- ✅ **Enhanced Event Registration**: Comprehensive event listener system for all game events affecting alliances
- ✅ **UniqueId Repair System**: Automatic repair of invalid UniqueId entries on load using InitiatorClanId fallback

### 2. Configuration System
- ✅ **JSON Configuration**: Dynamic AllianceConfig.cs reading SecretAlliancesConfig.json
- ✅ **Auto-Generation**: Creates default config file if missing with comprehensive settings
- ✅ **Safe Fallbacks**: Robust error handling with in-memory defaults if file operations fail
- ✅ **Runtime Validation**: Value clamping and validation with save capability

### 3. Alliance Formation AI
- ✅ **Daily Formation Evaluation**: Configurable base chance with daily formation limits
- ✅ **Weighting Factors**: Mutual enemies, economic disparity, political pressure, military power ratios
- ✅ **Desperation Mechanics**: Weaker clans more likely to form alliances
- ✅ **Smart Targeting**: Avoids excessive evaluations while maintaining realistic formation patterns

### 4. Rumor & Intelligence System
- ✅ **Dialog Integration**: ShouldShowRumorOption and TryGetRumorsForHero methods
- ✅ **Ranking System**: Intelligence scored by reliability × recency × category weight
- ✅ **Category Weighting**: Coup (1.25x), Military (1.15x), Financial (1.1x), Recruitment (1.0x), Trade (0.95x), General (0.9x)
- ✅ **Access Control**: Rumors only available through shared alliance connections or coalition groups
- ✅ **Aging System**: Daily reliability decay (0.005/day), automatic cleanup after 180 days

### 5. Leak & Intel Generation
- ✅ **Logistic Probability**: Enhanced leak chance using logistic function based on secrecy levels
- ✅ **Pact-Specific Bonuses**: TradePact (+10%), MilitaryPact (+15%) leak chance modifiers
- ✅ **Severity Modeling**: Dynamic severity calculation based on strength, secrecy, betrayal flags
- ✅ **Informer Relations**: Reliability based on informer hero traits and relationships

### 6. Trade & Financial Dynamics
- ✅ **Adaptive Transfers**: Daily gold transfers based on disparity ratios and trust levels
- ✅ **Conservative Limits**: Givers retain 75% of gold above 5k buffer
- ✅ **Magnitude Tracking**: Percentile calculation among last 20 transfers per alliance
- ✅ **Intelligence Generation**: Financial intel created for significant transfers (>60th percentile)
- ✅ **Trust Dynamics**: Trust gains with diminishing returns, secrecy penalties for large transfers

### 7. Coalition Cohesion
- ✅ **Group Metrics**: Cohesion = CohesionStrengthFactor × avgStrength + CohesionSecrecyFactor × avgSecrecy
- ✅ **Performance Bonuses**: High cohesion (>0.6) grants +Strength bonus and slower secrecy decay
- ✅ **Penalty System**: Low cohesion (<0.35) increases secrecy decay rate
- ✅ **Cached Values**: GroupStrengthCache and GroupSecrecyCache for performance

### 8. Operations Framework
- ✅ **Five Operation Types**: CovertAid, SpyProbe, RecruitmentFeelers, SabotageRaid, CounterIntelligence
- ✅ **Cooldown Management**: Per-alliance, per-operation cooldown tracking (ephemeral)
- ✅ **Adaptive Scheduling**: Interval adjustment based on PoliticalPressure and Trust levels
- ✅ **Success Mechanics**: Multi-factor success calculation including leader cunning
- ✅ **Risk/Reward Balance**: Operation-specific difficulties and failure penalties

#### Operation Details:
- **CovertAid**: Low risk, +Strength/Trust, -Secrecy
- **SpyProbe**: Medium risk, generates Military intel about third parties
- **RecruitmentFeelers**: Medium risk, attempts to recruit new clans to coalition
- **SabotageRaid**: High risk, creates high-severity Military intel about enemies
- **CounterIntelligence**: Low-medium risk, reduces existing intel reliability, provides leak protection buff

### 9. Defection & Betrayal System
- ✅ **Strategic Evaluation**: Pre-battle and periodic betrayal assessment
- ✅ **Multi-Factor Analysis**: Base chance modified by strength, trust, political pressure, desperation
- ✅ **Escalation System**: Near-miss attempts (+0.05 base chance, capped at +0.15)
- ✅ **Debug Logging**: JSON factor output for probability >0.05 or verbose mode
- ✅ **Betrayal Cooldowns**: Prevents spam re-evaluation with configurable cooldown periods

### 10. Forced Reveal Mechanics
- ✅ **Threshold System**: Strength > ForcedRevealStrengthThreshold AND Secrecy < ForcedRevealSecrecyThreshold
- ✅ **Probability Calculation**: (Strength - threshold) × (thresholdSecrecy - Secrecy + 0.05)
- ✅ **Kingdom-Wide Impact**: High-severity General intel generated for all clans in related kingdoms
- ✅ **Automatic Flagging**: Sets BetrayalRevealed flag on forced reveal

### 11. Aging & Decay System
- ✅ **Alliance Dissolution**: Alliances >540 days old with Strength <0.3 and Trust <0.25 may dissolve (2% daily)
- ✅ **Intelligence Aging**: Daily DaysOld increment, reliability decay (0.005/day)
- ✅ **Automatic Cleanup**: Removal when DaysOld >180 or reliability <0.05

### 12. Console Commands
- ✅ **sa.dumpAlliances**: List all active alliances with core statistics
- ✅ **sa.forceLeak**: Force leak generation for testing (usage: sa.forceLeak clanA clanB)
- ✅ **sa.addTrust**: Modify trust levels (usage: sa.addTrust clanA clanB amount)
- ✅ **sa.runOperation**: Execute specific operations (usage: sa.runOperation clanA clanB opType)
- ✅ **sa.listIntel**: Display intelligence records (usage: sa.listIntel [clanStringId])
- ✅ **sa.config**: View/modify configuration (usage: sa.config [property] [value])
- ✅ **sa.forceReveal**: Force alliance revelation (usage: sa.forceReveal clanA clanB)
- ✅ **sa.createAlliance**: Create test alliance (usage: sa.createAlliance clanA clanB)

### 13. Performance & Safety
- ✅ **Optimized Loops**: Minimized heavy LINQ operations in daily processing
- ✅ **Value Clamping**: All float fields properly constrained (0-1 where appropriate)
- ✅ **Null Safety**: Comprehensive null guards around hero/clan lookups
- ✅ **Memory Management**: Automatic cleanup of old data structures

### 14. Enhanced Logging
- ✅ **Consistent Prefix**: All logs use [SecretAlliances] prefix
- ✅ **Verbosity Control**: DebugVerbose configuration flag for detailed logging
- ✅ **Factor Logging**: JSON format for complex calculations (betrayal evaluation)
- ✅ **Event Logging**: Major events logged with relevant context

### 15. Save Compatibility
- ✅ **Field Preservation**: No modification of existing SaveableField indices 1-31
- ✅ **New Fields**: Added indices 32-35 for additional functionality:
  - [32] NextEligibleOperationDay
  - [33] BetrayalCooldownDays  
  - [34] CounterIntelBuffExpiryDay
  - [35] BetrayalEscalationCounter
- ✅ **Backward Compatibility**: All new fields optional with safe defaults
- ✅ **Ephemeral Data**: Operation cooldowns and trade transfers use in-memory storage

### 16. Dialog Integration
- ✅ **Rumor Access Control**: ShouldShowRumorOption checks alliance connections and coalition groups
- ✅ **Ranked Results**: GetTopRumorString and TryGetRumorsForHero with intelligent filtering
- ✅ **Formatted Output**: Context-aware rumor string generation with informer attribution

## Technical Details

### New Classes
- **AllianceConfig**: JSON-based configuration management
- **ConsoleCommands**: Comprehensive debugging interface
- **TradeTransferRecord**: Trade transaction tracking (ephemeral)
- **OperationType**: Enumeration for operation types

### Enhanced Enumerations
- **AllianceIntelType**: Expanded from 5 to 10 categories for better intelligence classification

### Configuration Parameters
All values configurable via SecretAlliancesConfig.json:
- Formation rates and limits
- Operation intervals and cooldowns  
- Leak and betrayal base chances
- Trade flow multipliers
- Cohesion calculation factors
- Forced reveal thresholds
- Debug verbosity control

### Performance Considerations
- Ephemeral operation cooldown tracking (not persisted)
- Capped trade transfer history (20 entries max)
- Limited daily evaluations to prevent performance issues
- Intelligent cleanup of old data structures

This mega expansion transforms Secret Alliances from a basic alliance system into a comprehensive political simulation with deep strategic mechanics, realistic information warfare, and emergent coalition dynamics.