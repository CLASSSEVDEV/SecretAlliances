# Secret Alliances - Advanced Edition

A comprehensive Mount & Blade II: Bannerlord mod that transforms diplomacy through sophisticated secret alliance mechanics, economic warfare, espionage networks, and strategic military coordination.

## 🌟 Features Overview

### Core Alliance System
- **Multi-tier Alliance Ranks**: Basic (0) → Advanced (1) → Strategic (2)
- **Dynamic Reputation System**: Alliance reliability tracking with natural decay
- **Time-limited Contracts**: Formal agreements with auto-renewal and violation tracking
- **Marriage Alliance Integration**: Royal marriages strengthen diplomatic ties
- **Diplomatic Immunity**: Protection from hostile actions for high-rank alliances

### Military & Combat Systems
- **Joint Military Campaigns**: Coordinated siege and raid operations
- **Battle Coordination Levels**: 5-tier system with combat efficiency bonuses up to 25%
- **Elite Unit Exchange**: Share specialized troops between allied clans
- **Fortress Network Access**: Mutual castle and town defense agreements
- **Formation Synchronization**: Allied forces use complementary battle tactics

### Economic & Trade Networks
- **Advanced Trade Networks**: Volume multipliers up to 200% for allied trade
- **Resource Sharing Agreements**: Configurable sharing ratios up to 30%
- **Joint Caravan Protection**: Multi-level escort systems (10 protection tiers)
- **Economic Warfare**: Coordinated market disruption and trade embargos
- **Price Manipulation**: Collective market control capabilities

### Espionage & Intelligence
- **5-Tier Spy Networks**: Sophisticated intelligence operations
- **Embedded Agent System**: Long-term assets in enemy courts
- **Counter-Intelligence Operations**: Defensive systems with maintenance requirements
- **Double Agent Recruitment**: Turn enemy spies into assets
- **Information Brokerage**: Enhanced intelligence categorization and trading

### Diplomatic & Political Features
- **Council of Allies**: Multi-alliance coordination mechanisms
- **Succession Planning**: Influence heir selection in allied clans
- **Territory Agreements**: Border demarcation and expansion rights
- **Royal Influence Operations**: Direct impact on kingdom politics
- **Cultural Exchange Programs**: Long-term relationship building

## 🎮 Gameplay Integration

### Dialog System
- **Enhanced Conversation Options**: Context-aware alliance proposals
- **Advanced Operation Planning**: In-game strategic coordination
- **Intelligence Gathering**: Rumor networks and information trading
- **Contract Negotiation**: Formal agreement discussions
- **Alliance Progression**: Rank upgrade conversations

### Event Integration
- **Marriage Events**: Automatic alliance creation/strengthening
- **Settlement Changes**: Territory agreement benefits
- **Trade Monitoring**: Economic network growth tracking
- **Battle Outcomes**: Military coordination improvements
- **Political Events**: Dynamic alliance relationship effects

## ⚙️ Configuration System

### Comprehensive Settings
- **40+ Configuration Parameters**: Fine-tune every aspect of alliance mechanics
- **Performance Optimization**: Efficient processing for 100+ active alliances
- **Feature Toggle System**: Enable/disable advanced features as needed
- **Balance Validation**: Automatic parameter validation and clamping
- **Debug & Analytics**: Comprehensive logging and statistics tracking

### Key Configuration Categories
- **Formation & Decay Rates**: Control alliance natural progression
- **Operation Cooldowns**: Balance covert action frequency
- **Economic Parameters**: Trade network effectiveness settings
- **Military Coordination**: Combat bonus and cooperation levels
- **Espionage Systems**: Spy network detection and success rates

## 🛠️ Technical Implementation

### Bannerlord v1.2.9 API Compatibility
- **Full API Compliance**: All features use supported game systems
- **Event System Integration**: Comprehensive event handler coverage
- **Save System Compatibility**: Backward-compatible save file format
- **Performance Optimized**: Daily cache updates for large campaigns
- **Error Handling**: Robust null checks and validation throughout

### Data Structures
- **Advanced Alliance Records**: 45+ tracked properties per alliance
- **Contract Management**: Time-limited agreements with violation tracking
- **Military Coordination Data**: Battle tactics and unit exchange systems
- **Economic Network Data**: Trade optimization and warfare capabilities
- **Spy Network Data**: Intelligence operations and counter-measures

### .NET Framework 4.7.2 & C# 7.3 Compliance
- **Language Standard Adherence**: All code follows C# 7.3 specifications
- **Framework Compatibility**: Designed for .NET Framework 4.7.2
- **Memory Efficient**: Optimized data structures and caching systems
- **Thread Safe**: Proper synchronization for campaign behavior systems

## 🎯 Console Commands

### Basic Commands
- `sa.dumpAlliances` - List all active alliances with detailed information
- `sa.createAlliance clanA clanB` - Create test alliance between specified clans
- `sa.forceLeak clanA clanB` - Force intelligence leak for testing
- `sa.addTrust clanA clanB amount` - Modify alliance trust levels
- `sa.config [property] [value]` - View/modify configuration settings

### Advanced Commands
- `sa.upgradeAlliance clanA clanB` - Manually upgrade alliance rank
- `sa.createContract clanA clanB type duration value` - Create formal contracts
- `sa.spyOp allianceClanA allianceClanB targetClan opType` - Execute spy operations
- `sa.economicWar allianceClanA allianceClanB targetClan` - Launch economic warfare
- `sa.jointCampaign allianceClanA allianceClanB settlement` - Coordinate military campaigns
- `sa.advancedStats [detailed]` - View comprehensive alliance statistics

## 📊 Statistics & Analytics

### Performance Metrics
- **Alliance Influence Tracking**: Clan power dynamics analysis
- **Economic Impact Measurement**: Trade network effectiveness
- **Military Coordination Success**: Battle cooperation statistics
- **Intelligence Operation Results**: Spy network performance data
- **Contract Compliance Rates**: Agreement fulfillment tracking

### Debug Information
- **Comprehensive Logging**: JSON-formatted debug output
- **Real-time Statistics**: Live alliance health monitoring
- **Performance Profiling**: Cache efficiency and processing times
- **Error Reporting**: Detailed exception handling and recovery

## 🔧 Installation & Setup

### Requirements
- Mount & Blade II: Bannerlord v1.2.9 or compatible
- .NET Framework 4.7.2
- Newtonsoft.Json library (included with Bannerlord)

### Installation Steps
1. Extract mod files to `Modules/SecretAlliances/`
2. Enable mod in Bannerlord launcher
3. Start new campaign or load existing save
4. Configuration file will be auto-generated on first run

### Configuration Customization
- Edit `SecretAlliancesConfig.json` in mod directory
- Modify parameters to suit your gameplay preferences
- Use console commands for real-time testing
- Restart game to apply major configuration changes

## 🎪 Advanced Strategies

### Early Game
- Focus on relationship building and basic alliance formation
- Prioritize trade pacts for economic development
- Use intelligence gathering to identify opportunities
- Build reputation through successful minor operations

### Mid Game
- Upgrade alliances to unlock advanced features
- Establish economic networks for resource advantages
- Begin military coordination for battle benefits
- Expand spy networks for strategic intelligence

### Late Game
- Orchestrate complex multi-alliance operations
- Dominate trade networks through economic warfare
- Coordinate massive joint military campaigns
- Influence succession and territorial agreements

## 🏆 Strategic Depth

### Alliance Progression Paths
1. **Economic Focus**: Trade networks → Market control → Economic warfare
2. **Military Focus**: Battle coordination → Elite exchanges → Joint campaigns
3. **Intelligence Focus**: Spy networks → Double agents → Information dominance
4. **Diplomatic Focus**: Marriage alliances → Royal influence → Succession control

### Risk vs. Reward Balance
- Higher alliance ranks offer greater benefits but increase exposure risk
- Economic warfare provides immediate advantages but may trigger retaliation
- Spy operations yield valuable intelligence but risk detection and diplomatic damage
- Military coordination enhances battle effectiveness but requires significant investment

## 📝 Version Compatibility

### Save Game Compatibility
- **Full Backward Compatibility**: New fields use append-only patterns
- **Graceful Degradation**: Missing data handled with sensible defaults
- **Migration Support**: Automatic upgrade of older alliance formats
- **Safe Uninstall**: Core game save integrity maintained

### Mod Compatibility
- **Event System Respect**: Uses non-serialized listeners only
- **API Standard Compliance**: No private reflection or unsafe operations
- **Resource Efficiency**: Minimal performance impact on base game
- **Clean Integration**: No conflicts with standard game mechanics

---

*Transform your Bannerlord experience with the most comprehensive alliance system ever created. From simple trade agreements to complex multi-kingdom conspiracies, Secret Alliances delivers unprecedented strategic depth and diplomatic intrigue.*

**Compatible with Bannerlord v1.2.9 | .NET Framework 4.7.2 | C# 7.3**