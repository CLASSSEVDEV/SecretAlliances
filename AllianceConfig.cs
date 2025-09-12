using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace SecretAlliances
{
    [Serializable]
    public class AllianceConfig
    {
        // Formation settings
        public float FormationBaseChance { get; set; } = 0.07f;
        public int MaxDailyFormations { get; set; } = 3;
        
        // Operation settings
        public int OperationIntervalDays { get; set; } = 7;
        public int OperationAdaptiveMinDays { get; set; } = 3;
        
        // Risk and probability settings
        public float LeakBaseChance { get; set; } = 0.01f;
        public float TradeFlowMultiplier { get; set; } = 1.0f;
        public float BetrayalBaseChance { get; set; } = 0.005f;
        
        // Cohesion factors
        public float CohesionStrengthFactor { get; set; } = 0.6f;
        public float CohesionSecrecyFactor { get; set; } = 0.4f;
        
        // Operation cooldowns (days)
        public int SpyProbeCooldownDays { get; set; } = 5;
        public int SabotageCooldownDays { get; set; } = 10;
        public int CounterIntelCooldownDays { get; set; } = 8;
        public int RecruitmentCooldownDays { get; set; } = 14;
        public int CovertAidCooldownDays { get; set; } = 4;
        
        // Forced reveal thresholds
        public float ForcedRevealStrengthThreshold { get; set; } = 0.9f;
        public float ForcedRevealSecrecyThreshold { get; set; } = 0.25f;
        
        // Debug and console settings
        public bool DebugVerbose { get; set; } = false;
        public bool DebugEnableConsoleCommands { get; set; } = false;
        
        // Player notifications
        public bool NotificationsEnabled { get; set; } = true;
        
        // Rumor system
        public int MaxRumorsReturned { get; set; } = 3;

        /// <summary>
        /// Loads configuration from JSON file in module root path, or creates default if missing.
        /// </summary>
        /// <param name="moduleRootPath">Path to the module root directory</param>
        /// <returns>AllianceConfig instance with loaded or default values</returns>
        public static AllianceConfig LoadOrCreate(string moduleRootPath)
        {
            string configPath = Path.Combine(moduleRootPath, "SecretAlliancesConfig.json");
            AllianceConfig config = null;

            try
            {
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    config = JsonConvert.DeserializeObject<AllianceConfig>(jsonContent);
                    
                    if (config != null)
                    {
                        Debug.Print($"[SecretAlliances] Config loaded from {configPath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Failed to load config from {configPath}: {ex.Message}");
            }

            // Create default config if loading failed or file doesn't exist
            config = new AllianceConfig();
            
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save default config with pretty formatting
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, jsonContent);
                
                Debug.Print($"[SecretAlliances] Default config created at {configPath}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Failed to save default config to {configPath}: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// Saves the current configuration to JSON file.
        /// </summary>
        /// <param name="moduleRootPath">Path to the module root directory</param>
        public void Save(string moduleRootPath)
        {
            string configPath = Path.Combine(moduleRootPath, "SecretAlliancesConfig.json");
            
            try
            {
                string jsonContent = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(configPath, jsonContent);
                Debug.Print($"[SecretAlliances] Config saved to {configPath}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Failed to save config to {configPath}: {ex.Message}");
            }
        }
    }
}