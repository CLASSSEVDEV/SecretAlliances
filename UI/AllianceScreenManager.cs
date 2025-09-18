using System;
using System.Collections.Generic;
using System.Linq;
using SecretAlliances.Campaign;
using SecretAlliances.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;

namespace SecretAlliances.UI
{
    /// <summary>
    /// Manages UI integration for alliance screens and displays
    /// Provides integration points for Clan and Kingdom screens
    /// </summary>
    public class AllianceScreenManager
    {
        private SecretAllianceBehavior _allianceBehavior;

        public AllianceScreenManager(SecretAllianceBehavior allianceBehavior)
        {
            _allianceBehavior = allianceBehavior;
        }

        /// <summary>
        /// Initialize UI integration hooks for existing screens
        /// </summary>
        public void InitializeScreenIntegration()
        {
            // TODO: When Gauntlet UI support is added, initialize screen extensions here
            // For now, we provide console-based information display
            Debug.Print("[SecretAlliances] UI integration initialized - console mode");
        }

        /// <summary>
        /// Get alliance information for the clan screen
        /// </summary>
        public AllianceDisplayInfo GetClanAllianceInfo(Clan clan)
        {
            if (clan == null || _allianceBehavior == null)
                return new AllianceDisplayInfo();

            var alliances = _allianceBehavior.GetAlliancesForClan(clan);
            var activeAlliances = alliances.Where(a => a.IsActive).ToList();

            return new AllianceDisplayInfo
            {
                TotalAlliances = activeAlliances.Count,
                ActiveAlliances = activeAlliances,
                HasMilitaryPacts = activeAlliances.Any(a => a.MilitaryPact),
                HasTradePacts = activeAlliances.Any(a => a.TradePact),
                AverageSecrecy = activeAlliances.Any() ? activeAlliances.Average(a => a.Secrecy) : 0f,
                AverageStrength = activeAlliances.Any() ? activeAlliances.Average(a => a.Strength) : 0f,
                IsPlayerClan = clan == Clan.PlayerClan
            };
        }

        /// <summary>
        /// Get alliance information for the kingdom screen
        /// </summary>
        public KingdomAllianceDisplayInfo GetKingdomAllianceInfo(Kingdom kingdom)
        {
            if (kingdom == null || _allianceBehavior == null)
                return new KingdomAllianceDisplayInfo();

            var allAlliances = _allianceBehavior.GetActiveAlliances();
            var kingdomAlliances = allAlliances.Where(a =>
                (a.GetInitiatorClan()?.Kingdom == kingdom) ||
                (a.GetTargetClan()?.Kingdom == kingdom)).ToList();

            var internalAlliances = kingdomAlliances.Where(a =>
                a.GetInitiatorClan()?.Kingdom == kingdom &&
                a.GetTargetClan()?.Kingdom == kingdom).ToList();

            var externalAlliances = kingdomAlliances.Where(a =>
                (a.GetInitiatorClan()?.Kingdom == kingdom && a.GetTargetClan()?.Kingdom != kingdom) ||
                (a.GetTargetClan()?.Kingdom == kingdom && a.GetInitiatorClan()?.Kingdom != kingdom)).ToList();

            return new KingdomAllianceDisplayInfo
            {
                Kingdom = kingdom,
                TotalAlliances = kingdomAlliances.Count,
                InternalAlliances = internalAlliances.Count,
                ExternalAlliances = externalAlliances.Count,
                ActiveAlliances = kingdomAlliances,
                StabilityThreat = CalculateKingdomStabilityThreat(kingdom, kingdomAlliances),
                HasSecretAlliances = kingdomAlliances.Any(a => a.Secrecy > 0.5f)
            };
        }

        /// <summary>
        /// Show alliance details in an information message
        /// Temporary solution until Gauntlet UI is implemented
        /// </summary>
        public void ShowAllianceDetails(Clan clan)
        {
            var info = GetClanAllianceInfo(clan);
            if (info.TotalAlliances == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{clan.Name} has no active secret alliances.", Colors.Gray));
                return;
            }

            var details = new List<string>();
            details.Add($"{clan.Name} Alliance Summary:");
            details.Add($"Active Alliances: {info.TotalAlliances}");
            
            if (info.HasMilitaryPacts)
                details.Add("Has Military Pacts");
            if (info.HasTradePacts)
                details.Add("Has Trade Pacts");

            details.Add($"Average Secrecy: {info.AverageSecrecy:F2}");
            details.Add($"Average Strength: {info.AverageStrength:F2}");

            InformationManager.DisplayMessage(new InformationMessage(
                string.Join(" | ", details), Colors.Cyan));
        }

        /// <summary>
        /// Show kingdom-wide alliance overview
        /// </summary>
        public void ShowKingdomAllianceOverview(Kingdom kingdom)
        {
            var info = GetKingdomAllianceInfo(kingdom);
            if (info.TotalAlliances == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{kingdom.Name} has no known secret alliances.", Colors.Gray));
                return;
            }

            var details = new List<string>();
            details.Add($"{kingdom.Name} Alliance Overview:");
            details.Add($"Total: {info.TotalAlliances}");
            details.Add($"Internal: {info.InternalAlliances}");
            details.Add($"External: {info.ExternalAlliances}");
            
            string threatLevel = info.StabilityThreat > 0.7f ? "HIGH" : 
                               info.StabilityThreat > 0.4f ? "MEDIUM" : "LOW";
            details.Add($"Threat: {threatLevel}");

            InformationManager.DisplayMessage(new InformationMessage(
                string.Join(" | ", details), Colors.Yellow));
        }

        private float CalculateKingdomStabilityThreat(Kingdom kingdom, List<SecretAllianceRecord> alliances)
        {
            if (!alliances.Any()) return 0f;

            float threat = 0f;
            int strongAlliances = alliances.Count(a => a.Strength > 0.6f);
            int secretAlliances = alliances.Count(a => a.Secrecy > 0.5f);
            int militaryPacts = alliances.Count(a => a.MilitaryPact);

            threat += strongAlliances * 0.2f;
            threat += secretAlliances * 0.15f;
            threat += militaryPacts * 0.25f;

            // External alliances are more threatening
            int externalAlliances = alliances.Count(a =>
                (a.GetInitiatorClan()?.Kingdom == kingdom && a.GetTargetClan()?.Kingdom != kingdom) ||
                (a.GetTargetClan()?.Kingdom == kingdom && a.GetInitiatorClan()?.Kingdom != kingdom));
            
            threat += externalAlliances * 0.3f;

            return Math.Min(1f, threat / alliances.Count);
        }
    }

    /// <summary>
    /// Display information for clan alliance screen
    /// </summary>
    public class AllianceDisplayInfo
    {
        public int TotalAlliances { get; set; }
        public List<SecretAllianceRecord> ActiveAlliances { get; set; } = new List<SecretAllianceRecord>();
        public bool HasMilitaryPacts { get; set; }
        public bool HasTradePacts { get; set; }
        public float AverageSecrecy { get; set; }
        public float AverageStrength { get; set; }
        public bool IsPlayerClan { get; set; }
    }

    /// <summary>
    /// Display information for kingdom alliance screen
    /// </summary>
    public class KingdomAllianceDisplayInfo
    {
        public Kingdom Kingdom { get; set; }
        public int TotalAlliances { get; set; }
        public int InternalAlliances { get; set; }
        public int ExternalAlliances { get; set; }
        public List<SecretAllianceRecord> ActiveAlliances { get; set; } = new List<SecretAllianceRecord>();
        public float StabilityThreat { get; set; }
        public bool HasSecretAlliances { get; set; }
    }
}