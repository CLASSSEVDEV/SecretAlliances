using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances
{
    /// <summary>
    /// Advanced alliance management system providing high-level strategic operations
    /// Compatible with Bannerlord v1.2.9 API and .NET Framework 4.7.2
    /// </summary>
    public class AdvancedAllianceManager
    {
        private readonly SecretAllianceBehavior _behavior;
        private readonly AllianceConfig _config;

        public AdvancedAllianceManager(SecretAllianceBehavior behavior)
        {
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _config = AllianceConfig.Instance;
        }

        #region Strategic Alliance Management

        /// <summary>
        /// Analyzes and suggests optimal alliance formation opportunities for the player
        /// </summary>
        public List<AllianceOpportunity> AnalyzeAllianceOpportunities()
        {
            var opportunities = new List<AllianceOpportunity>();
            var playerClan = Clan.PlayerClan;

            if (playerClan?.Kingdom == null) return opportunities;

            foreach (var targetClan in Clan.All.Where(c => IsValidAllianceTarget(c, playerClan)))
            {
                var opportunity = EvaluateAllianceOpportunity(playerClan, targetClan);
                if (opportunity.Score > 0.4f) // Only suggest viable opportunities
                {
                    opportunities.Add(opportunity);
                }
            }

            return opportunities.OrderByDescending(o => o.Score).Take(10).ToList();
        }

        /// <summary>
        /// Evaluates the strategic value of forming an alliance between two clans
        /// </summary>
        private AllianceOpportunity EvaluateAllianceOpportunity(Clan playerClan, Clan targetClan)
        {
            var opportunity = new AllianceOpportunity
            {
                TargetClan = targetClan,
                PlayerClan = playerClan
            };

            // Military synergy analysis
            float militaryScore = CalculateMilitarySynergy(playerClan, targetClan);
            
            // Economic compatibility analysis
            float economicScore = CalculateEconomicCompatibility(playerClan, targetClan);
            
            // Political alignment analysis
            float politicalScore = CalculatePoliticalAlignment(playerClan, targetClan);
            
            // Geographic proximity analysis
            float proximityScore = CalculateGeographicProximity(playerClan, targetClan);
            
            // Threat response analysis
            float threatScore = CalculateThreatResponse(playerClan, targetClan);

            // Weighted composite score
            opportunity.Score = (militaryScore * 0.25f) + (economicScore * 0.2f) + 
                               (politicalScore * 0.25f) + (proximityScore * 0.15f) + 
                               (threatScore * 0.15f);

            opportunity.MilitaryValue = militaryScore;
            opportunity.EconomicValue = economicScore;
            opportunity.PoliticalValue = politicalScore;
            opportunity.RiskLevel = CalculateRiskLevel(playerClan, targetClan);
            
            // Generate strategic recommendations
            opportunity.Recommendations = GenerateStrategicRecommendations(opportunity);

            return opportunity;
        }

        /// <summary>
        /// Calculates military synergy between two clans
        /// </summary>
        private float CalculateMilitarySynergy(Clan playerClan, Clan targetClan)
        {
            float synergy = 0f;

            // Complementary military strengths
            float playerStrength = playerClan.TotalStrength;
            float targetStrength = targetClan.TotalStrength;
            float combinedStrength = playerStrength + targetStrength;

            // Bonus for balanced alliance (neither clan dominates completely)
            float balanceRatio = Math.Min(playerStrength, targetStrength) / Math.Max(playerStrength, targetStrength);
            synergy += balanceRatio * 0.3f;

            // Analyze troop composition complementarity
            var playerTroops = GetClanTroopComposition(playerClan);
            var targetTroops = GetClanTroopComposition(targetClan);
            synergy += CalculateTroopComplementarity(playerTroops, targetTroops) * 0.4f;

            // Geographic military coverage
            synergy += CalculateMilitaryCoverage(playerClan, targetClan) * 0.3f;

            return Math.Min(1f, synergy);
        }

        /// <summary>
        /// Calculates economic compatibility between clans
        /// </summary>
        private float CalculateEconomicCompatibility(Clan playerClan, Clan targetClan)
        {
            float compatibility = 0f;

            // Trade route synergy
            compatibility += CalculateTradeRouteSynergy(playerClan, targetClan) * 0.4f;

            // Resource complementarity
            compatibility += CalculateResourceComplementarity(playerClan, targetClan) * 0.3f;

            // Market access benefits
            compatibility += CalculateMarketAccess(playerClan, targetClan) * 0.3f;

            return Math.Min(1f, compatibility);
        }

        /// <summary>
        /// Calculates political alignment between clans
        /// </summary>
        private float CalculatePoliticalAlignment(Clan playerClan, Clan targetClan)
        {
            float alignment = 0f;

            // Relationship analysis
            var relation = targetClan.Leader?.GetRelation(Hero.MainHero) ?? 0;
            alignment += Math.Max(0f, relation / 100f) * 0.4f;

            // Kingdom alignment
            if (playerClan.Kingdom == targetClan.Kingdom)
            {
                alignment += 0.3f; // Same kingdom bonus
            }
            else if (playerClan.Kingdom?.IsAtWarWith(targetClan.Kingdom) == true)
            {
                alignment -= 0.5f; // War penalty
            }

            // Cultural compatibility
            if (playerClan.Culture == targetClan.Culture)
            {
                alignment += 0.2f;
            }

            // Honor and trait compatibility
            alignment += CalculateTraitCompatibility(Hero.MainHero, targetClan.Leader) * 0.1f;

            return Math.Max(0f, Math.Min(1f, alignment));
        }

        #endregion

        #region Advanced Operations Management

        /// <summary>
        /// Orchestrates a complex multi-phase alliance operation
        /// </summary>
        public OperationResult ExecuteAdvancedOperation(SecretAllianceRecord alliance, AdvancedOperationType operationType, Clan targetClan)
        {
            var result = new OperationResult
            {
                OperationType = operationType,
                TargetClan = targetClan,
                StartDay = CampaignTime.Now.GetDayOfYear
            };

            switch (operationType)
            {
                case AdvancedOperationType.CoordinatedEconomicCampaign:
                    result = ExecuteCoordinatedEconomicCampaign(alliance, targetClan);
                    break;

                case AdvancedOperationType.IntelligenceNetworkExpansion:
                    result = ExecuteIntelligenceNetworkExpansion(alliance, targetClan);
                    break;

                case AdvancedOperationType.DiplomaticInfluenceOperation:
                    result = ExecuteDiplomaticInfluenceOperation(alliance, targetClan);
                    break;

                case AdvancedOperationType.MilitaryCoordinationDrill:
                    result = ExecuteMilitaryCoordinationDrill(alliance);
                    break;

                case AdvancedOperationType.CulturalExchangeProgram:
                    result = ExecuteCulturalExchangeProgram(alliance);
                    break;

                default:
                    result.Success = false;
                    result.Message = "Unknown operation type";
                    break;
            }

            // Update alliance statistics
            if (result.Success)
            {
                alliance.SuccessfulOperations++;
                _behavior.UpdateReputationScore(alliance, 0.05f);
            }
            else
            {
                alliance.Secrecy -= 0.02f; // Failed operations may compromise secrecy
            }

            return result;
        }

        /// <summary>
        /// Executes a coordinated economic campaign against a target
        /// </summary>
        private OperationResult ExecuteCoordinatedEconomicCampaign(SecretAllianceRecord alliance, Clan targetClan)
        {
            var result = new OperationResult { OperationType = AdvancedOperationType.CoordinatedEconomicCampaign };

            if (!_config.EnableEconomicWarfare || alliance.EconomicIntegration < 0.4f)
            {
                result.Success = false;
                result.Message = "Insufficient economic integration for coordinated campaign";
                return result;
            }

            // Phase 1: Market analysis and preparation
            float marketImpact = CalculateMarketImpact(alliance, targetClan);
            
            // Phase 2: Coordinated trade disruption
            _behavior.ExecuteEconomicWarfare(alliance, targetClan);
            
            // Phase 3: Alternative route establishment
            EstablishAlternativeTradeRoutes(alliance);

            result.Success = marketImpact > 0.3f;
            result.Message = result.Success ? 
                $"Economic campaign successful - {marketImpact:P0} market disruption achieved" :
                "Economic campaign had limited impact";
            result.ImpactScore = marketImpact;

            return result;
        }

        /// <summary>
        /// Executes intelligence network expansion operation
        /// </summary>
        private OperationResult ExecuteIntelligenceNetworkExpansion(SecretAllianceRecord alliance, Clan targetClan)
        {
            var result = new OperationResult { OperationType = AdvancedOperationType.IntelligenceNetworkExpansion };

            if (!_config.EnableSpyNetworks || alliance.SpyNetworkLevel < 2)
            {
                result.Success = false;
                result.Message = "Insufficient spy network capabilities";
                return result;
            }

            // Multi-phase intelligence operation
            int successfulPhases = 0;
            
            // Phase 1: Agent placement
            if (_behavior.ExecuteSpyOperation(alliance, targetClan, 1))
                successfulPhases++;

            // Phase 2: Network establishment
            if (MBRandom.RandomFloat < 0.7f)
            {
                var spyData = _behavior.GetSpyNetworkData().FirstOrDefault(s => s.AllianceId == alliance.UniqueId);
                if (spyData != null)
                {
                    spyData.EmbeddedAgents.Add(GetRandomHeroFromClan(targetClan)?.Id ?? MBGUID.Empty);
                    successfulPhases++;
                }
            }

            // Phase 3: Information gathering
            if (_behavior.ExecuteSpyOperation(alliance, targetClan, 2))
                successfulPhases++;

            result.Success = successfulPhases >= 2;
            result.Message = $"Intelligence expansion: {successfulPhases}/3 phases successful";
            result.ImpactScore = successfulPhases / 3f;

            return result;
        }

        #endregion

        #region Utility Methods

        private bool IsValidAllianceTarget(Clan targetClan, Clan playerClan)
        {
            return targetClan != null && !targetClan.IsEliminated && 
                   targetClan != playerClan && targetClan.Leader != null &&
                   _behavior.FindAlliance(playerClan, targetClan) == null;
        }

        private float CalculateRiskLevel(Clan playerClan, Clan targetClan)
        {
            float risk = 0f;

            // Political risk
            if (playerClan.Kingdom?.IsAtWarWith(targetClan.Kingdom) == true)
                risk += 0.4f;

            // Reputation risk
            if (targetClan.Leader?.GetTraitLevel(DefaultTraits.Honor) < 0)
                risk += 0.2f;

            // Military risk (if target is much stronger)
            if (targetClan.TotalStrength > playerClan.TotalStrength * 2f)
                risk += 0.3f;

            // Geographic risk (distance)
            var distance = CalculateGeographicProximity(playerClan, targetClan);
            risk += (1f - distance) * 0.1f;

            return Math.Min(1f, risk);
        }

        private List<string> GenerateStrategicRecommendations(AllianceOpportunity opportunity)
        {
            var recommendations = new List<string>();

            if (opportunity.MilitaryValue > 0.7f)
                recommendations.Add("Strong military synergy - consider military pact");

            if (opportunity.EconomicValue > 0.7f)
                recommendations.Add("Excellent trade potential - prioritize trade pact");

            if (opportunity.RiskLevel > 0.6f)
                recommendations.Add("High risk - proceed with caution and strong initial secrecy");

            if (opportunity.PoliticalValue < 0.3f)
                recommendations.Add("Poor political alignment - invest in diplomacy first");

            return recommendations;
        }

        private Hero GetRandomHeroFromClan(Clan clan)
        {
            return clan?.Heroes?.Where(h => h.IsAlive && !h.IsChild)?.GetRandomElementInefficiently();
        }

        // Placeholder implementations for complex calculations
        private Dictionary<string, float> GetClanTroopComposition(Clan clan) => new Dictionary<string, float>();
        private float CalculateTroopComplementarity(Dictionary<string, float> comp1, Dictionary<string, float> comp2) => 0.5f;
        private float CalculateMilitaryCoverage(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateTradeRouteSynergy(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateResourceComplementarity(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateMarketAccess(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateGeographicProximity(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateThreatResponse(Clan clan1, Clan clan2) => 0.5f;
        private float CalculateTraitCompatibility(Hero hero1, Hero hero2) => 0.5f;
        private float CalculateMarketImpact(SecretAllianceRecord alliance, Clan target) => 0.6f;
        private void EstablishAlternativeTradeRoutes(SecretAllianceRecord alliance) { }
        private OperationResult ExecuteDiplomaticInfluenceOperation(SecretAllianceRecord alliance, Clan target) => new OperationResult { Success = true };
        private OperationResult ExecuteMilitaryCoordinationDrill(SecretAllianceRecord alliance) => new OperationResult { Success = true };
        private OperationResult ExecuteCulturalExchangeProgram(SecretAllianceRecord alliance) => new OperationResult { Success = true };

        #endregion
    }

    #region Supporting Data Structures

    /// <summary>
    /// Represents an alliance formation opportunity with strategic analysis
    /// </summary>
    public class AllianceOpportunity
    {
        public Clan PlayerClan { get; set; }
        public Clan TargetClan { get; set; }
        public float Score { get; set; }
        public float MilitaryValue { get; set; }
        public float EconomicValue { get; set; }
        public float PoliticalValue { get; set; }
        public float RiskLevel { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// Advanced operation types for complex alliance activities
    /// </summary>
    public enum AdvancedOperationType
    {
        CoordinatedEconomicCampaign = 1,
        IntelligenceNetworkExpansion = 2,
        DiplomaticInfluenceOperation = 3,
        MilitaryCoordinationDrill = 4,
        CulturalExchangeProgram = 5
    }

    /// <summary>
    /// Result of an advanced alliance operation
    /// </summary>
    public class OperationResult
    {
        public AdvancedOperationType OperationType { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public float ImpactScore { get; set; }
        public Clan TargetClan { get; set; }
        public int StartDay { get; set; }
        public int CompletionDay { get; set; }
    }

    #endregion
}