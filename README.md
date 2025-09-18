# SecretAlliances Mod for Mount & Blade II: Bannerlord v1.2.9

A comprehensive mod that adds secret alliances, military pacts, trade agreements, and complex diplomacy to Mount & Blade II: Bannerlord.

## Features

- **Secret Alliances**: Create hidden diplomatic relationships with other clans
- **Military Pacts**: Coordinate military actions with allied clans
- **Trade Agreements**: Establish beneficial economic relationships
- **AI Behavior**: Smart AI that can create, maintain, and betray alliances
- **Battle Assistance**: Help allied clans in battle without changing your kingdom
- **Intelligence System**: Gather and share information about other factions

## Project Structure

```
SecretAlliances/
├── Core/                           # Domain models, services, persistence
│   ├── SecretAllianceRecord.cs     # Alliance data model with save compatibility
│   ├── AlliancesConfig.cs          # Configuration system with JSON persistence
│   ├── SecretAlliancesSaveDefiner.cs # Save system integration
│   └── DictionaryExtensions.cs     # Utility extensions
├── Campaign/                       # Campaign behavior implementations
│   ├── SecretAllianceBehavior.cs   # Main campaign behavior with game logic
│   ├── AdvancedAllianceManager.cs  # AI alliance management
│   └── ConsoleCommands.cs          # Debug and testing commands
├── UI/                            # UI helpers and components
│   └── AllianceUIHelper.cs        # Debug and information display utilities
└── SubModule.cs                   # Main module entry point
```

## Building

### Prerequisites

- .NET Framework 4.7.2
- Mount & Blade II: Bannerlord v1.2.9

### Setup

1. Clone this repository to your Bannerlord Modules folder:
   ```
   ...\Mount & Blade II Bannerlord\Modules\SecretAlliances\
   ```

2. Set the `BannerlordPath` environment variable (optional):
   ```
   set BannerlordPath=C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord
   ```

3. Build the project using Visual Studio or MSBuild:
   ```
   msbuild SecretAlliances.csproj /p:Configuration=Release
   ```

The build system will automatically find your Bannerlord installation in common Steam locations.

## Installation

1. Build the project or download the compiled mod
2. Copy the built files to your Bannerlord Modules folder
3. Enable the mod in the Bannerlord launcher

## Configuration

The mod creates a `SecretAlliancesConfig.json` file that allows you to customize:
- Alliance formation rates
- AI behavior parameters  
- Economic and military effects
- Debug options

## Key Fixes

- **Battle Assistance**: Fixed critical bug where helping allies would force kingdom changes
- **Configuration System**: Robust config loading with fallback defaults
- **Project Structure**: Organized code into logical namespaces for maintainability

## Compatibility

- Mount & Blade II: Bannerlord v1.2.9
- .NET Framework 4.7.2
- C# 7.3

## Contributing

This mod follows the design principles of minimal, surgical changes to maintain stability and save compatibility.