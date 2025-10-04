# Secret Alliances - Complete Political Simulation

A comprehensive Mount & Blade II: Bannerlord mod that adds deep diplomatic, economic, and espionage mechanics inspired by real-world politics.

## 🌟 Features

### 🤝 Diplomacy System
- **Influence & Reputation**: Build political power (0-1000 influence) and maintain your diplomatic standing
- **7 Agreement Types**: Non-aggression pacts, trade deals, military access, mutual defense, tributes, and more
- **Political Favors**: Request support from allies - from diplomatic introductions to military backing
- **Political Intrigue**: Engage in or defend against influence campaigns, backroom deals, and betrayals

### 💰 Economic Warfare
- **Trade Embargoes**: Impose partial or full embargoes, financial sanctions, or military trade bans
- **Trade Monopolies**: Control specific goods for profit (requires 50 influence + 50k gold)
- **Market Manipulation**: Control prices in your settlements
- **Coordinated Attacks**: Launch alliance-wide economic assaults on rivals

### 🕵️ Espionage & Intelligence
- **Spy Networks**: Establish 5-tier spy networks in enemy territory
- **6 Operation Types**:
  - Gather Intelligence (Tier 1+)
  - Sabotage (Tier 2+)
  - Assassination (Tier 4+)
  - Steal Technology (Tier 3+)
  - Incite Rebellion (Tier 5+)
  - Plant Double Agent (Tier 3+)
- **Counter-Intelligence**: Invest in detection capabilities (5 levels)
- **Intelligence Reports**: Receive detailed reports on enemies (quality based on network tier)

## 🎮 How to Use

### Opening the Alliance Manager
1. Open your **Clan** menu (hotkey: L)
2. Click the **"Secret Alliances"** button
3. The Alliance Manager UI will open showing:
   - Your active alliances
   - Available alliances to join
   - Pending requests
   - Sent requests

### Forming Alliances
1. Talk to a lord in person
2. Choose diplomatic dialog options
3. Propose a secret alliance
4. They will accept based on:
   - Your relationship
   - Relative power
   - Traits (Honor, Calculating, etc.)

### Using Diplomacy
**Create Agreements:**
- Use influence to establish formal agreements
- Choose type and duration
- Both parties benefit from the agreement

**Request Political Favors:**
- Costs 10-35 influence depending on favor type
- Success based on alliance trust and relationship
- Grants various benefits (gold, influence, intelligence)

**Warning**: Breaking agreements damages reputation (-0.15) and relations (-20)!

### Economic Warfare
**Impose Embargoes:**
- Requires influence (20-40 depending on type)
- Reduces target's trade and prosperity
- Duration is customizable

**Establish Monopolies:**
- Costs 50 influence + 50,000 gold
- Generates daily profit (1,000 gold × 1.5 multiplier)
- Can be broken by rivals (costs them 30 influence)

**Market Manipulation:**
- Control prices in your settlements
- Costs 15 influence
- Effects decay 5% per day

### Espionage Operations
**Step 1: Establish Spy Network**
- Choose target clan
- Select tier (1-5): Higher tier = better effectiveness but higher cost
- Pay influence (15 × tier) and gold (5,000 × tier)
- Network generates intel automatically

**Step 2: Launch Operations**
- Select operation type based on network tier
- Pay influence cost (10-50 depending on operation)
- Wait for completion (3-15 days)
- Success chance shown based on network effectiveness

**Step 3: Counter-Intelligence**
- Invest in your own counter-intel (20 influence + 3k gold per level)
- Each level increases detection chance by 2% per hour
- Reduces enemy operation success by 10% per level

## 💡 Strategy Tips

### Early Game (Clan Tier 1-2)
- Focus on forming basic alliances
- Build relationships with potential partners
- Start accumulating influence
- Keep reputation neutral/positive

### Mid Game (Clan Tier 3-4)
- Create trade agreements for economic boost
- Establish Tier 1-2 spy networks
- Use political favors strategically
- Consider partial embargoes on enemies

### Late Game (Clan Tier 5-6)
- Launch coordinated alliance attacks
- Establish trade monopolies
- Run Tier 4-5 espionage operations
- Engage in high-stakes political intrigue

## ⚠️ Consequences

### Exposure & Betrayal
- **Exposed Spy Network**: -30 relation, -0.15 reputation, network destroyed
- **Broken Agreement**: -20 relation, -0.15 reputation, -20 influence
- **Failed Operation**: 50% chance of exposure with severe penalties

### Resource Management
- Spy networks cost 100 gold/tier/day to maintain
- Influence regenerates naturally based on power
- Reputation slowly returns to neutral (0.5)
- Alliance trust must be maintained through cooperation

## 🤖 AI Behavior

All systems include AI participation:
- NPCs form their own diplomatic agreements
- AI clans request political favors
- Rivals impose embargoes on each other
- AI establishes spy networks and launches operations
- Political intrigue happens independently

This creates a **dynamic political landscape** where you're not the only player!

## 🔧 Console Commands

Access via console (Ctrl + ~):
- `alliance.list` - Show all active alliances
- `alliance.create [clan1] [clan2]` - Force alliance creation
- `alliance.info [clan]` - Show clan's alliance status
- (See ConsoleCommands.cs for full list)

## 📊 Resource Guide

### Influence
- **Sources**: Settlements, fiefs, military strength, wealth, kingdom position
- **Uses**: All diplomatic/espionage actions
- **Range**: 0-1000 points
- **Regeneration**: Slowly shifts towards base influence daily

### Reputation
- **Range**: 0.0 (terrible) to 1.0 (excellent)
- **Affects**: Success rates of all diplomatic actions
- **Damaged By**: Betrayals, broken agreements, exposed espionage
- **Recovery**: Slowly drifts toward 0.5 (neutral) at 1% per day

### Alliance Trust
- **Range**: 0.0 to 1.0
- **Affects**: Political favor acceptance, alliance stability
- **Improved By**: Fulfilling requests, political favors, cooperation
- **Reduced By**: Declining requests, inaction, kingdom changes

## 🏗️ Technical Details

**Compatible With**: Mount & Blade II: Bannerlord v1.2.9
**Framework**: .NET Framework 4.7.2
**Dependencies**:
- Harmony 2.3.6
- UIExtenderEx 2.12.0
- BUTR.MessageBoxPInvoke 1.0.0.1

**Save Compatibility**: All systems fully persist in save files

## 🐛 Troubleshooting

### UI Button Not Appearing
- Make sure UIExtenderEx 2.12.0 is installed
- Try the overlay button (top-right of clan screen)
- Check mod load order (SecretAlliances should load after UIExtenderEx)

### Behaviors Not Working
- Ensure you're in a campaign (not sandbox setup)
- Check game version matches v1.2.9
- Look for error messages in console log

### Save Issues
- All systems use proper SaveSystem implementation
- If issues persist, check for mod conflicts
- Backup saves before major updates

## 📝 Credits

**Original Concept**: CLASSSEVDEV
**Version 2.0 Overhaul**: Complete rewrite with 3,000+ lines of new code
**Design Philosophy**: Real-world political simulation

## 🎯 Vision

This mod transforms Bannerlord's campaign layer into a **comprehensive political simulation** where:
- Military might is just one path to power
- Strategic planning spans multiple fronts
- Every action has consequences
- AI creates unpredictable challenges
- Victory requires diplomatic finesse, not just battlefield prowess

Experience Mount & Blade II like never before - where **politics is as important as warfare**!

---

For detailed feature documentation, see [CHANGELOG.md](CHANGELOG.md)
