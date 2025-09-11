# Secret Alliances - Debug Instructions

## Testing Core Stabilization Features

### Forcing Intelligence Leaks

#### Method 1: Lower Alliance Secrecy in Debugger
1. Start a campaign and create a secret alliance between two clans
2. Pause the game and open your debugger (if available)
3. Find the alliance in the `SecretAllianceBehavior._alliances` list
4. Set the alliance's `Secrecy` property to a low value (e.g., 0.2 or lower)
5. Resume the game and wait a few days - leaks should occur more frequently

#### Method 2: Temporary Console Helper (Development)
```csharp
// Add this temporary method to SecretAllianceBehavior for testing
public void DEBUG_ForceLeakForAlliance(int allianceIndex)
{
    if (allianceIndex >= 0 && allianceIndex < _alliances.Count)
    {
        var alliance = _alliances[allianceIndex];
        alliance.Secrecy = 0.1f; // Very low secrecy
        var potentialInformants = GetPotentialInformants(
            alliance.GetInitiatorClan(), 
            alliance.GetTargetClan(), 
            alliance);
        ProcessLeak(alliance, potentialInformants);
        Debug.Print($"DEBUG: Forced leak for alliance {allianceIndex}");
    }
}
```

#### Method 3: Alliance Property Manipulation
```csharp
// In console or mod code:
var behavior = Campaign.Current.GetCampaignBehavior<SecretAllianceBehavior>();
var alliances = behavior.GetActiveAlliances();
if (alliances.Any())
{
    var alliance = alliances.First();
    alliance.Secrecy = 0.15f;
    alliance.LeakAttempts = 3; // Increases leak probability
}
```

### Testing Rumor Retrieval

#### Verify Rumor Logic
1. **Create Alliance**: Use console or dialogue to create secret alliance between clans
2. **Force Leak**: Use methods above to create intelligence entries
3. **Test Hero Access**: Try `TryGetRumorsForHero()` with different heroes:
   - Heroes from involved clans (should have high chance)
   - Heroes from same kingdom (should have some chance)
   - Heroes from different kingdoms (should have low/no chance)

#### Console Test Commands
```csharp
// Test rumor availability
var hero = Hero.MainHero; // or any hero
var behavior = Campaign.Current.GetCampaignBehavior<SecretAllianceBehavior>();
if (behavior.TryGetRumorsForHero(hero, out string rumors))
{
    Debug.Print($"Rumors for {hero.Name}: {rumors}");
}
else
{
    Debug.Print($"No rumors available for {hero.Name}");
}

// Test rumor option gate
bool shouldShow = behavior.ShouldShowRumorOption(hero);
Debug.Print($"Should show rumor option for {hero.Name}: {shouldShow}");
```

### Testing Trade Pact Logic

#### Verify CanOfferTradePact
```csharp
var behavior = Campaign.Current.GetCampaignBehavior<SecretAllianceBehavior>();
var clan1 = Clan.PlayerClan;
var clan2 = Clan.All.First(c => c != clan1 && !c.IsEliminated);

// Create alliance first
behavior.CreateAlliance(clan1, clan2, 0.8f, 0.5f);

// Test trade pact eligibility
bool canOffer = behavior.CanOfferTradePact(clan1, clan2);
Debug.Print($"Can offer trade pact: {canOffer}");

// Adjust alliance properties and retest
var alliance = behavior.FindAlliance(clan1, clan2);
alliance.TrustLevel = 0.4f;  // Above threshold
alliance.Secrecy = 0.2f;     // Above threshold
alliance.CooldownDays = 0;   // Not on cooldown

canOffer = behavior.CanOfferTradePact(clan1, clan2);
Debug.Print($"Can offer after adjustments: {canOffer}");
```

### Verifying Save Compatibility

#### Testing New Fields
1. **Create alliance** with old save (before stabilization)
2. **Load game** - should not crash, new fields should have defaults
3. **Create new alliance** - should populate all fields properly
4. **Save and reload** - verify all data persists correctly

#### Field Value Verification
```csharp
var alliances = Campaign.Current.GetCampaignBehavior<SecretAllianceBehavior>().GetActiveAlliances();
foreach (var alliance in alliances)
{
    Debug.Print($"Alliance {alliance.InitiatorClanId} -> {alliance.TargetClanId}:");
    Debug.Print($"  UniqueId: {alliance.UniqueId}");
    Debug.Print($"  LastOperationDay: {alliance.LastOperationDay}");
    Debug.Print($"  PendingOperationType: {alliance.PendingOperationType}");
    Debug.Print($"  GroupSecrecyCache: {alliance.GroupSecrecyCache}");
    Debug.Print($"  GroupStrengthCache: {alliance.GroupStrengthCache}");
    Debug.Print($"  CooldownDays: {alliance.CooldownDays}");
}

var intel = Campaign.Current.GetCampaignBehavior<SecretAllianceBehavior>().GetIntelligence();
foreach (var i in intel.Take(5)) // First 5 entries
{
    Debug.Print($"Intelligence: {i.AllianceId}");
    Debug.Print($"  ClanAId: {i.ClanAId}");
    Debug.Print($"  ClanBId: {i.ClanBId}");
    Debug.Print($"  IntelCategory: {i.IntelCategory} ({(AllianceIntelType)i.IntelCategory})");
}
```

### Expected Test Results

#### Alliance Creation
- CooldownDays should be 0 (allowing immediate trade pact offers)
- UniqueId should be set to InitiatorClanId (not default MBGUID)
- New fields should initialize to appropriate defaults

#### Intelligence Leaks
- Should populate ClanAId and ClanBId with alliance clan IDs
- IntelCategory should reflect alliance type (0=General, 1=Trade, 2=Military, etc.)
- Debug output should include clan names and category

#### Rumor Retrieval
- Should use ClanAId/ClanBId for relevance filtering (not AllianceId comparison)
- Should rank by reliability * recencyWeight formula
- ShouldShowRumorOption should gate availability correctly

### Common Issues to Check

1. **Null Reference Exceptions**: Ensure null guards for new fields in old saves
2. **Default MBGUID**: Verify UniqueId is never default value
3. **Incorrect Filtering**: Confirm rumor logic uses clan IDs, not alliance ID
4. **Save Corruption**: Test save/load cycles with mixed old/new alliances
5. **Performance**: Ensure intelligence ranking doesn't cause frame drops with large datasets

### Performance Notes

- Intelligence collection filtering runs on each rumor check - monitor for performance issues
- Ranking calculation scales with intelligence entries - consider limiting active intelligence count
- Debug output should be minimal in production builds