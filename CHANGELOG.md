# Secret Alliances - Core Stabilization Changelog

## Version 1.0 - Core Stabilization (PR 1)

### Data Model Extensions (Save Compatible)
- **SecretAllianceRecord**: Added new SaveableFields 28-31
  - `LastOperationDay` (Field 28): Tracks when the last operation was performed
  - `PendingOperationType` (Field 29): Stores the type of operation pending execution
  - `GroupSecrecyCache` (Field 30): Cached secrecy value for group operations
  - `GroupStrengthCache` (Field 31): Cached strength value for group operations

- **AllianceIntelligence**: Added new SaveableFields 7-9
  - `ClanAId` (Field 7): MBGUID of the first clan involved in the alliance
  - `ClanBId` (Field 8): MBGUID of the second clan involved in the alliance  
  - `IntelCategory` (Field 9): Integer mapping to AllianceIntelType enum

### UniqueId Stabilization
- **Fixed alliance creation**: Now assigns proper UniqueId using InitiatorClanId as surrogate
- **Intelligence generation**: Ensures never uses default MBGUID when creating intelligence records

### Intelligence & Rumor System Fixes
- **TryGetRumorsForHero**: Fixed logic that incorrectly compared AllianceId with hero.Clan.Id
- **Relevance filtering**: Now uses ClanAId/ClanBId for proper clan-based intelligence filtering
- **Ranking system**: Implemented `reliability * recencyWeight` formula where `recencyWeight = 1 - DaysOld * 0.01` (floor at 0.5)
- **ShouldShowRumorOption**: Added helper method returning bool to gate dialogue options

### New Features
- **AllianceIntelType enum**: Added internal enum with categories (General/Trade/Military/Coup/Financial/Recruitment)
- **CanOfferTradePact helper**: Centralized logic checking alliance active, no existing trade pact, not on cooldown, trust >= 0.35, secrecy >= 0.15
- **ProcessLeak improvements**: Now populates ClanAId, ClanBId, and IntelCategory fields with proper intelligence categorization
- **Alliance creation**: Changed initial CooldownDays from 5 to 0 to allow immediate pact offers

### Technical Notes
- All new SaveableField indices are appended only - no existing indices modified
- Maintains backward compatibility with existing save files
- New fields initialize with sensible defaults for existing alliances
- Proper null guards added when accessing new intelligence fields
- Enhanced logging includes clan names and intelligence categories

### Debugging Support
- Enhanced debug output in ProcessLeak includes clan names and intelligence categories
- Rumor retrieval logging shows count and top-ranked entry details
- Intelligence leak logging includes reliability, severity, and category information

---

**Next PRs**: Adaptive trade pact economics, coalition cohesion recalculation, defection probability instrumentation, operations scheduling scaffold, and expanded economic balancing.