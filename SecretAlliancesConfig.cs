using System;
using System.IO;
using System.Text.Json;
using TaleWorlds.Library;

namespace SecretAlliances
{
    /// <summary>
    /// Configuration system with JSON auto-generation and safe fallbacks (Additional Enhancement requirement)
    /// </summary>
    [Serializable]
    public class SecretAlliancesConfig
    {
        // Economic Intelligence Constants (Former PR 2)
        public float WealthDisparityThreshold { get; set; } = 0.3f;
        public float ReserveFloor { get; set; } = 0.05f;
        public float VolatilityBand { get; set; } = 0.2f;
        public int AntiExploitTransferLimit { get; set; } = 3;
        public int AntiExploitDayWindow { get; set; } = 10;

        // Coalition Cohesion Constants (Former PR 3)
        public float CohesionStrengthWeight { get; set; } = 0.6f;
        public float CohesionSecrecyWeight { get; set; } = 0.4f;
        public float LowCohesionThreshold { get; set; } = 0.3f;
        public float CohesionStrengthBuff { get; set; } = 0.05f;
        public float CohesionSecrecyDecay { get; set; } = 0.001f;

        // Defection & Betrayal Constants (Former PR 4)
        public int DefectionCooldownDays { get; set; } = 30;
        public float BetrayalEscalatorIncrement { get; set; } = 0.1f;
        public float BetrayalEscalatorCap { get; set; } = 0.5f;
        public float BetrayalNotificationThreshold { get; set; } = 0.4f;

        // Operations Framework Constants (Former PR 5)
        public int OperationBaseDuration { get; set; } = 7;
        public float OperationSchedulePoliticalThreshold { get; set; } = 0.6f;
        public float OperationScheduleTrustThreshold { get; set; } = 0.75f;
        public int SpyProbeCooldown { get; set; } = 5;
        public int SabotageCooldown { get; set; } = 10;
        public int CovertAidCooldown { get; set; } = 7;
        public int RecruitmentCooldown { get; set; } = 14;
        public int CounterIntelCooldown { get; set; } = 12;

        // Rumor & UI Constants (Former PR 6)
        public float RumorReliabilityThreshold { get; set; } = 0.3f;
        public float RumorAgingFactor { get; set; } = 0.01f;
        public int MaxRumorsReturned { get; set; } = 3;

        // Balancing & Polishing Constants (Former PR 7)
        public float StrengthCap { get; set; } = 0.9f;
        public float SecrecyForceRevealThreshold { get; set; } = 0.25f;
        public float LeakSmoothingMidpoint { get; set; } = 0.5f;
        public float LeakSmoothingSlope { get; set; } = 6.0f;
        public float DailyTrustClamp { get; set; } = 0.05f;
        public float DailyStrengthClamp { get; set; } = 0.05f;
        public float DailySecrecyClamp { get; set; } = 0.05f;

        // Additional Enhancement Constants
        public float SuspicionThreshold { get; set; } = 3.0f;
        public int SuspicionDecayDays { get; set; } = 7;
        public float InfluenceConversionThreshold { get; set; } = 0.8f;
        public int InfluenceConversionCooldown { get; set; } = 30;

        private static SecretAlliancesConfig _instance;
        private static readonly object _lock = new object();

        public static SecretAlliancesConfig Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = LoadOrCreateDefault();
                    }
                    return _instance;
                }
            }
        }

        private static SecretAlliancesConfig LoadOrCreateDefault()
        {
            try
            {
                var configPath = Path.Combine(GetConfigDirectory(), "SecretAlliancesConfig.json");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<SecretAlliancesConfig>(json);
                    Debug.Print("[Secret Alliances] Config loaded from " + configPath);
                    return config ?? new SecretAlliancesConfig();
                }
                else
                {
                    // Auto-generate config file with defaults
                    var config = new SecretAlliancesConfig();
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(config, options);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                    File.WriteAllText(configPath, json);
                    
                    Debug.Print("[Secret Alliances] Config auto-generated at " + configPath);
                    return config;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[Secret Alliances] Config error: {ex.Message}, using defaults");
                return new SecretAlliancesConfig();
            }
        }

        private static string GetConfigDirectory()
        {
            try
            {
                // Try to get mod directory
                var asmLocation = Path.GetDirectoryName(typeof(SecretAlliancesConfig).Assembly.Location);
                if (!string.IsNullOrEmpty(asmLocation))
                    return asmLocation;
            }
            catch { }

            // Fallback to temp directory
            return Path.GetTempPath();
        }

        public void Save()
        {
            try
            {
                var configPath = Path.Combine(GetConfigDirectory(), "SecretAlliancesConfig.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json);
                Debug.Print("[Secret Alliances] Config saved to " + configPath);
            }
            catch (Exception ex)
            {
                Debug.Print($"[Secret Alliances] Config save error: {ex.Message}");
            }
        }
    }
}