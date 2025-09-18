using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.Actions;

namespace SecretAlliances
{
    /// <summary>
    /// UI Integration class for Secret Alliances mod
    /// Provides interface integration with clan and kingdom screens
    /// Compatible with Bannerlord v1.2.9 API
    /// </summary>
    public class AllianceUIIntegration
    {
        private readonly SecretAllianceBehavior _behavior;
        private readonly AdvancedAllianceManager _advancedManager;

        public AllianceUIIntegration(SecretAllianceBehavior behavior)
        {
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _advancedManager = new AdvancedAllianceManager(behavior);
        }

        /// <summary>
        /// Gets alliance status information for display in clan screen
        /// </summary>
        public string GetClanAllianceStatusText(Clan clan)
        {
            if (clan == null) return "No alliance information available.";

            var alliances = _behavior.GetAlliances()
                .Where(a => a.IsActive && (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id))
                .ToList();

            if (!alliances.Any())
                return "This clan has no active secret alliances.";

            var statusText = $"Active Secret Alliances: {alliances.Count}\n\n";

            foreach (var alliance in alliances.Take(5)) // Limit display to prevent UI overflow
            {
                var partnerClan = alliance.InitiatorClanId == clan.Id ? 
                    alliance.GetTargetClan() : alliance.GetInitiatorClan();

                if (partnerClan != null)
                {
                    statusText += $"• {partnerClan.Name}\n";
                    statusText += $"  Strength: {alliance.Strength:P0} | Trust: {alliance.TrustLevel:P0}\n";
                    
                    if (alliance.TradePact) statusText += "  [Trade Pact Active]\n";
                    if (alliance.MilitaryPact) statusText += "  [Military Pact Active]\n";
                    if (alliance.MarriageAlliance) statusText += "  [Marriage Alliance]\n";
                    if (alliance.DiplomaticImmunity) statusText += "  [Diplomatic Immunity]\n";
                    
                    statusText += $"  Operations: {alliance.SuccessfulOperations}\n\n";
                }
            }

            if (alliances.Count > 5)
            {
                statusText += $"... and {alliances.Count - 5} more alliances.";
            }

            return statusText;
        }

        /// <summary>
        /// Gets recommended alliance opportunities for the player clan
        /// </summary>
        public List<string> GetAllianceRecommendations()
        {
            var recommendations = new List<string>();

            try
            {
                var opportunities = _advancedManager.AnalyzeAllianceOpportunities();
                
                foreach (var opportunity in opportunities.Take(3))
                {
                    var recommendation = $"Consider alliance with {opportunity.TargetClan.Name}:\n";
                    recommendation += $"  Score: {opportunity.Score:P0}\n";
                    recommendation += $"  Military Value: {opportunity.MilitaryValue:P0}\n";
                    recommendation += $"  Economic Value: {opportunity.EconomicValue:P0}\n";
                    recommendation += $"  Risk Level: {opportunity.RiskLevel:P0}\n";
                    
                    if (opportunity.Recommendations.Any())
                    {
                        recommendation += "  Recommendations:\n";
                        foreach (var rec in opportunity.Recommendations.Take(2))
                        {
                            recommendation += $"    • {rec}\n";
                        }
                    }
                    
                    recommendations.Add(recommendation);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error generating recommendations: {ex.Message}");
                recommendations.Add("Unable to analyze alliance opportunities at this time.");
            }

            return recommendations;
        }

        /// <summary>
        /// Gets intelligence reports for display
        /// </summary>
        public List<string> GetIntelligenceReports(int maxReports = 5)
        {
            var reports = new List<string>();

            try
            {
                var alliances = _behavior.GetAlliances();
                foreach (var alliance in alliances.Where(a => a.IsActive))
                {
                    // Simulate intelligence reports based on alliance data
                    if (alliance.LastLeakSeverity > 0.3f && MBRandom.RandomFloat < 0.3f)
                    {
                        var report = $"Intelligence Report: {alliance.GetInitiatorClan()?.Name} <-> {alliance.GetTargetClan()?.Name}\n";
                        report += $"Reliability: {(alliance.Secrecy > 0.5f ? "High" : "Moderate")}\n";
                        report += $"Activity Level: {(alliance.SuccessfulOperations > 3 ? "High" : "Low")}\n";
                        reports.Add(report);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error generating intelligence reports: {ex.Message}");
                reports.Add("Intelligence gathering systems are currently unavailable.");
            }

            return reports.Take(maxReports).ToList();
        }

        /// <summary>
        /// Gets the current political influence from alliances
        /// </summary>
        public string GetAllianceInfluenceStatus(Clan clan)
        {
            if (clan == null) return "No influence data available.";

            try
            {
                float influence = _behavior.GetAllianceInfluence(clan);
                bool hasImmunity = _behavior.HasDiplomaticImmunity(clan);
                
                var status = $"Alliance Political Influence: {influence:F2}\n";
                
                if (hasImmunity)
                {
                    status += "Diplomatic Immunity: Active\n";
                    status += "Protection from hostile diplomatic actions.\n";
                }
                else
                {
                    status += "Diplomatic Immunity: None\n";
                }

                var contracts = _behavior.GetActiveContracts()
                    .Where(c => _behavior.GetAllianceById(c.AllianceId)?.InitiatorClanId == clan.Id ||
                               _behavior.GetAllianceById(c.AllianceId)?.TargetClanId == clan.Id)
                    .ToList();

                if (contracts.Any())
                {
                    status += $"\nActive Contracts: {contracts.Count}\n";
                    status += $"Total Contract Value: {contracts.Sum(c => c.ContractValue):F0} denars\n";
                }

                return status;
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error getting influence status: {ex.Message}");
                return "Unable to retrieve alliance influence data.";
            }
        }

        /// <summary>
        /// Provides actionable alliance management options
        /// </summary>
        public List<AllianceManagementOption> GetManagementOptions(Clan playerClan)
        {
            var options = new List<AllianceManagementOption>();

            if (playerClan == null) return options;

            try
            {
                var activeAlliances = _behavior.GetAlliances()
                    .Where(a => a.IsActive && (a.InitiatorClanId == playerClan.Id || a.TargetClanId == playerClan.Id))
                    .ToList();

                foreach (var alliance in activeAlliances)
                {
                    var partnerClan = alliance.InitiatorClanId == playerClan.Id ? 
                        alliance.GetTargetClan() : alliance.GetInitiatorClan();

                    if (partnerClan != null)
                    {
                        // Upgrade alliance option
                        if (_behavior.TryUpgradeAlliance(playerClan, partnerClan))
                        {
                            options.Add(new AllianceManagementOption
                            {
                                Title = $"Upgrade Alliance with {partnerClan.Name}",
                                Description = "Strengthen your alliance to unlock advanced features",
                                ActionType = AllianceActionType.Upgrade,
                                TargetClan = partnerClan,
                                Alliance = alliance
                            });
                        }

                        // Trade pact option
                        if (_behavior.CanOfferTradePact(playerClan, partnerClan))
                        {
                            options.Add(new AllianceManagementOption
                            {
                                Title = $"Establish Trade Pact with {partnerClan.Name}",
                                Description = "Coordinate trade efforts for mutual economic benefit",
                                ActionType = AllianceActionType.TradePact,
                                TargetClan = partnerClan,
                                Alliance = alliance
                            });
                        }

                        // Military pact option
                        if (_behavior.CanOfferMilitaryPact(playerClan, partnerClan))
                        {
                            options.Add(new AllianceManagementOption
                            {
                                Title = $"Form Military Pact with {partnerClan.Name}",
                                Description = "Coordinate military operations and provide mutual support",
                                ActionType = AllianceActionType.MilitaryPact,
                                TargetClan = partnerClan,
                                Alliance = alliance
                            });
                        }

                        // Dissolution option
                        options.Add(new AllianceManagementOption
                        {
                            Title = $"Dissolve Alliance with {partnerClan.Name}",
                            Description = "End the secret alliance (this may have consequences)",
                            ActionType = AllianceActionType.Dissolve,
                            TargetClan = partnerClan,
                            Alliance = alliance
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error generating management options: {ex.Message}");
            }

            return options;
        }

        /// <summary>
        /// Executes a management action
        /// </summary>
        public bool ExecuteManagementAction(AllianceManagementOption option)
        {
            if (option?.TargetClan == null || option.Alliance == null) return false;

            try
            {
                switch (option.ActionType)
                {
                    case AllianceActionType.Upgrade:
                        return _behavior.TryUpgradeAlliance(Clan.PlayerClan, option.TargetClan);

                    case AllianceActionType.TradePact:
                        return _behavior.TrySetTradePact(Clan.PlayerClan, option.TargetClan);

                    case AllianceActionType.MilitaryPact:
                        return _behavior.TrySetMilitaryPact(Clan.PlayerClan, option.TargetClan);

                    case AllianceActionType.Dissolve:
                        return _behavior.TryDissolveAlliance(Clan.PlayerClan, option.TargetClan, true);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error executing management action: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Represents a management action that can be taken on an alliance
    /// </summary>
    public class AllianceManagementOption
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public AllianceActionType ActionType { get; set; }
        public Clan TargetClan { get; set; }
        public SecretAllianceRecord Alliance { get; set; }
    }

    /// <summary>
    /// Types of actions that can be performed on alliances
    /// </summary>
    public enum AllianceActionType
    {
        Upgrade,
        TradePact,
        MilitaryPact,
        Dissolve,
        Intelligence,
        Contract
    }
}