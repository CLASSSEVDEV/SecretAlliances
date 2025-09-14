using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace SecretAlliances
{
    public class AllianceConfig
    {
        // Configuration properties with default values
        public float FormationBaseChance { get; set; } = 0.15f; // Increased from 0.05f to make AI more active
        public int MaxDailyFormations { get; set; } = 5; // Increased from 2 to allow more formations
        public int OperationIntervalDays { get; set; } = 15;
        public float LeakBaseChance { get; set; } = 0.008f;
        public float TradeFlowMultiplier { get; set; } = 1.5f;
        public float BetrayalBaseChance { get; set; } = 0.02f;
        public float CohesionStrengthFactor { get; set; } = 0.6f;
        public float CohesionSecrecyFactor { get; set; } = 0.4f;
        public int OperationAdaptiveMinDays { get; set; } = 7;
        public int SpyProbeCooldownDays { get; set; } = 20;
        public int SabotageCooldownDays { get; set; } = 30;
        public int CounterIntelCooldownDays { get; set; } = 25;
        public int RecruitmentCooldownDays { get; set; } = 45;
        public float ForcedRevealStrengthThreshold { get; set; } = 0.8f;
        public float ForcedRevealSecrecyThreshold { get; set; } = 0.2f;
        public bool DebugVerbose { get; set; } = false;

        public float DailySecrecyDecay { get; set; } = 0.005f;
        public float DailyStrengthGrowth { get; set; } = 0.002f;
        public float CoupSecrecyThreshold { get; set; } = 0.25f;
        public float CoupStrengthThreshold { get; set; } = 0.65f;

        // Advanced feature configurations
        public int MaxAllianceRank { get; set; } = 2;
        public int ContractMinDuration { get; set; } = 30;
        public int ContractMaxDuration { get; set; } = 365;
        public float ReputationDecayRate { get; set; } = 0.001f;
        public float ReputationGainMultiplier { get; set; } = 1.0f;

        // Military coordination settings
        public float MilitaryCoordinationMaxBonus { get; set; } = 0.25f;
        public int EliteUnitExchangeMinRank { get; set; } = 1;
        public int FortressNetworkMinRank { get; set; } = 2;
        public float JointCampaignEfficiencyBonus { get; set; } = 0.15f;

        // Economic network settings
        public float TradeNetworkMaxMultiplier { get; set; } = 2.0f;
        public float ResourceSharingMaxRatio { get; set; } = 0.3f;
        public int CaravanProtectionMaxLevel { get; set; } = 5;
        public float EconomicWarfareEffectiveness { get; set; } = 0.2f;

        // Espionage system settings
        public int MaxSpyNetworkTier { get; set; } = 5;
        public float SpyNetworkDetectionChance { get; set; } = 0.1f;
        public float CounterIntelSuccessRate { get; set; } = 0.6f;
        public int DoubleAgentMinTier { get; set; } = 3;

        // Diplomatic features
        public float DiplomaticMissionSuccessRate { get; set; } = 0.7f;
        public int MarriageAllianceInfluenceBonus { get; set; } = 20;
        public float TerritoryAgreementStabilityBonus { get; set; } = 0.1f;
        public int RoyalInfluenceMaxGain { get; set; } = 50;

        // Performance and balance
        public int MaxActiveContracts { get; set; } = 100;
        public bool EnableAdvancedFeatures { get; set; } = true;
        public float AdvancedFeatureUnlockThreshold { get; set; } = 0.5f;
        public bool EnableEconomicWarfare { get; set; } = true;
        public bool EnableSpyNetworks { get; set; } = true;

        private static AllianceConfig _instance;
        private static readonly object _lock = new object();

        public static AllianceConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        private static AllianceConfig LoadConfig()
        {
            try
            {
                // Try module root first, then bin folder
                string configPath = GetConfigPath();

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<AllianceConfig>(json);
                    Debug.Print($"[SecretAlliances] Configuration loaded from {configPath}");
                    return config ?? CreateDefaultConfig(configPath);
                }
                else
                {
                    Debug.Print($"[SecretAlliances] Config file not found, creating default at {configPath}");
                    return CreateDefaultConfig(configPath);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error loading config: {ex.Message}. Using defaults.");
                return new AllianceConfig();
            }
        }

        private static string GetConfigPath()
        {
            // Try to find the module directory
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Check if we're in a Modules/SecretAlliances structure
            string moduleConfigPath = Path.Combine(baseDirectory, "SecretAlliancesConfig.json");
            if (Directory.Exists(baseDirectory) && baseDirectory.Contains("SecretAlliances"))
            {
                return moduleConfigPath;
            }

            // Fallback to bin folder
            string binConfigPath = Path.Combine(baseDirectory, "bin", "SecretAlliancesConfig.json");
            return binConfigPath;
        }

        private static AllianceConfig CreateDefaultConfig(string configPath)
        {
            try
            {
                var defaultConfig = new AllianceConfig();
                string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                File.WriteAllText(configPath, json);

                Debug.Print($"[SecretAlliances] Default configuration created at {configPath}");
                return defaultConfig;
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Could not create config file: {ex.Message}. Using in-memory defaults.");
                return new AllianceConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                string configPath = GetConfigPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, json);
                Debug.Print($"[SecretAlliances] Configuration saved to {configPath}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error saving config: {ex.Message}");
            }
        }

        // Validation methods
        public void ValidateAndClamp()
        {
            FormationBaseChance = MathF.Max(0f, MathF.Min(1f, FormationBaseChance));
            MaxDailyFormations = Math.Max(0, MaxDailyFormations);
            OperationIntervalDays = Math.Max(1, OperationIntervalDays);
            LeakBaseChance = MathF.Max(0f, MathF.Min(1f, LeakBaseChance));
            TradeFlowMultiplier = MathF.Max(0.1f, TradeFlowMultiplier);
            BetrayalBaseChance = MathF.Max(0f, MathF.Min(1f, BetrayalBaseChance));
            CohesionStrengthFactor = MathF.Max(0f, MathF.Min(1f, CohesionStrengthFactor));
            CohesionSecrecyFactor = MathF.Max(0f, MathF.Min(1f, CohesionSecrecyFactor));
            OperationAdaptiveMinDays = Math.Max(1, OperationAdaptiveMinDays);
            SpyProbeCooldownDays = Math.Max(1, SpyProbeCooldownDays);
            SabotageCooldownDays = Math.Max(1, SabotageCooldownDays);
            CounterIntelCooldownDays = Math.Max(1, CounterIntelCooldownDays);
            RecruitmentCooldownDays = Math.Max(1, RecruitmentCooldownDays);
            ForcedRevealStrengthThreshold = MathF.Max(0f, MathF.Min(1f, ForcedRevealStrengthThreshold));
            ForcedRevealSecrecyThreshold = MathF.Max(0f, MathF.Min(1f, ForcedRevealSecrecyThreshold));

            // Validate advanced feature settings
            MaxAllianceRank = Math.Max(0, Math.Min(5, MaxAllianceRank));
            ContractMinDuration = Math.Max(1, ContractMinDuration);
            ContractMaxDuration = Math.Max(ContractMinDuration, ContractMaxDuration);
            ReputationDecayRate = MathF.Max(0f, MathF.Min(0.1f, ReputationDecayRate));
            ReputationGainMultiplier = MathF.Max(0.1f, MathF.Min(5f, ReputationGainMultiplier));

            // Military coordination validation
            MilitaryCoordinationMaxBonus = MathF.Max(0f, MathF.Min(1f, MilitaryCoordinationMaxBonus));
            EliteUnitExchangeMinRank = Math.Max(0, Math.Min(MaxAllianceRank, EliteUnitExchangeMinRank));
            FortressNetworkMinRank = Math.Max(0, Math.Min(MaxAllianceRank, FortressNetworkMinRank));
            JointCampaignEfficiencyBonus = MathF.Max(0f, MathF.Min(1f, JointCampaignEfficiencyBonus));

            // Economic network validation
            TradeNetworkMaxMultiplier = MathF.Max(1f, MathF.Min(5f, TradeNetworkMaxMultiplier));
            ResourceSharingMaxRatio = MathF.Max(0f, MathF.Min(1f, ResourceSharingMaxRatio));
            CaravanProtectionMaxLevel = Math.Max(1, Math.Min(10, CaravanProtectionMaxLevel));
            EconomicWarfareEffectiveness = MathF.Max(0f, MathF.Min(1f, EconomicWarfareEffectiveness));

            // Espionage system validation
            MaxSpyNetworkTier = Math.Max(1, Math.Min(10, MaxSpyNetworkTier));
            SpyNetworkDetectionChance = MathF.Max(0f, MathF.Min(1f, SpyNetworkDetectionChance));
            CounterIntelSuccessRate = MathF.Max(0f, MathF.Min(1f, CounterIntelSuccessRate));
            DoubleAgentMinTier = Math.Max(1, Math.Min(MaxSpyNetworkTier, DoubleAgentMinTier));

            // Diplomatic validation
            DiplomaticMissionSuccessRate = MathF.Max(0f, MathF.Min(1f, DiplomaticMissionSuccessRate));
            MarriageAllianceInfluenceBonus = Math.Max(0, MarriageAllianceInfluenceBonus);
            TerritoryAgreementStabilityBonus = MathF.Max(0f, MathF.Min(1f, TerritoryAgreementStabilityBonus));
            RoyalInfluenceMaxGain = Math.Max(0, RoyalInfluenceMaxGain);

            // Performance validation
            MaxActiveContracts = Math.Max(10, MaxActiveContracts);
            AdvancedFeatureUnlockThreshold = MathF.Max(0f, MathF.Min(1f, AdvancedFeatureUnlockThreshold));
        }
    }
}