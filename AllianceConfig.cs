using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace SecretAlliances
{
    public class AllianceConfig
    {
        // Configuration properties with default values
        public float FormationBaseChance { get; set; } = 0.05f;
        public int MaxDailyFormations { get; set; } = 2;
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
        }
    }
}