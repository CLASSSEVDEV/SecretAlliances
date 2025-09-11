# SecretAlliances Mod Implementation Notes

## Overview
This implementation addresses all requirements from the problem statement while maintaining strict save compatibility and API compliance with Bannerlord 1.2.9.

## Key Bug Fixes Implemented
1. **UniqueId Initialization**: Fixed uninitialized UniqueId in CreateAlliance method
2. **Rumor System**: Fixed incorrect filtering logic using clan pair fields instead of AllianceId
3. **Intelligence Linkage**: Added proper ClanAId/ClanBId tracking for intelligence records
4. **Duplicate Effects**: Removed duplicate MilitaryPact effects in strength/secrecy calculations

## Data Model Extensions
### SecretAllianceRecord (SaveableField 28-31)
- `LastOperationDay`: Tracks when operations were initiated
- `PendingOperationType`: Type of operation currently in progress
- `GroupSecrecyCache`: Cached group secrecy for coalition effects
- `GroupStrengthCache`: Cached group strength for coalition effects

### AllianceIntelligence (SaveableField 7-9)
- `ClanAId`: First clan in alliance pair for proper filtering
- `ClanBId`: Second clan in alliance pair for proper filtering
- `IntelCategory`: Intelligence type mapped from AllianceIntelType enum

## Coalition Mechanics
- **Cohesion Calculation**: Uses variance-based metrics across group alliances
- **Group Effects**: Cohesion influences effective secrecy and strength via caches
- **Daily Processing**: Integrated into OnDailyTickClan for automatic recalculation

## Enhanced Systems

### Trade Pact Logic
- Adaptive wealth balancing based on clan wealth disparity
- Incremental trust building based on transfer amounts
- Secrecy erosion proportional to trade activity
- Bounded transfers (max 5% of richer clan's wealth)

### Intelligence System
- Category-based intelligence generation (5 types)
- Reliability scoring with bounds
- Proper clan pair filtering for visibility
- Intelligence generation from pact activities

### Defection System
- Comprehensive factor breakdown logging
- All factors properly bounded to prevent runaway effects
- Coalition strength bonuses from group cache
- Maximum 40% defection probability cap

## Helper Predicates for UI Integration
- `ShouldShowRumorOption(Hero)`: Checks for relevant intelligence or alliance membership
- `CanOfferTradePact(Clan, Clan)`: Validates strength >= 0.3f, secrecy >= 0.2f
- `CanOfferMilitaryPact(Clan, Clan)`: Validates strength >= 0.5f, trust >= 0.6f

## Operations Scaffolding
- 7-day operation cycle with placeholder processing
- Three operation types: Military (1), Economic (2), Intelligence (3)
- Automatic operation consideration for strong alliances
- Ready for future expansion without breaking save compatibility

## Numeric Bounds Applied
- Defection probability: [0.0, 0.4]
- Daily strength gain: [0.0, 0.01]
- Daily secrecy loss: [0.0, 0.01]
- All core alliance values: [0.0, 1.0]
- Economic multipliers bounded to prevent runaway growth

## Save Compatibility
- Only appended new SaveableField indices (no renumbering)
- All new fields initialized in constructors
- Enum values mapped to int for save safety
- Backward compatible with existing save games

## API Compliance
- Uses only documented Bannerlord 1.2.9 public API
- No guessed or undocumented members
- Proper null checking and error handling
- Efficient LINQ usage patterns

## Testing & Validation
All critical systems have been validated for:
- Data structure integrity
- Save field index collision prevention
- Numeric bounds enforcement
- Helper predicate logic
- Coalition mechanics functionality
- Intelligence system operation