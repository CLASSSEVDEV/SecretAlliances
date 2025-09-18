using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

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
                    spyData.EmbeddedAgents.Add(GetRandomHeroFromClan(targetClan)?.Id ?? default(MBGUID));
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

        // Implementation of complex calculations
        private Dictionary<string, float> GetClanTroopComposition(Clan clan)
        {
            var composition = new Dictionary<string, float>
            {
                {"infantry", 0.4f},
                {"archer", 0.3f},
                {"cavalry", 0.3f}
            };

            if (clan?.WarPartyComponents != null)
            {
                float totalTroops = 0f;
                float infantryCount = 0f;
                float archerCount = 0f;
                float cavalryCount = 0f;

                foreach (var warParty in clan.WarPartyComponents)
                {
                    var party = warParty.MobileParty;
                    if (party?.MemberRoster != null)
                    {
                        for (int i = 0; i < party.MemberRoster.Count; i++)
                        {
                            var element = party.MemberRoster.GetElementCopyAtIndex(i);
                            if (element.Character?.IsInfantry == true)
                                infantryCount += element.Number;
                            else if (element.Character?.IsArcher == true)
                                archerCount += element.Number;
                            else if (element.Character?.IsMounted == true)
                                cavalryCount += element.Number;
                            totalTroops += element.Number;
                        }
                    }
                }

                if (totalTroops > 0)
                {
                    composition["infantry"] = infantryCount / totalTroops;
                    composition["archer"] = archerCount / totalTroops;
                    composition["cavalry"] = cavalryCount / totalTroops;
                }
            }

            return composition;
        }

        private float CalculateTroopComplementarity(Dictionary<string, float> comp1, Dictionary<string, float> comp2)
        {
            float complementarity = 0f;
            
            // High complementarity when one clan is strong where the other is weak
            complementarity += Math.Abs(comp1["infantry"] - comp2["infantry"]) * 0.3f;
            complementarity += Math.Abs(comp1["archer"] - comp2["archer"]) * 0.3f;
            complementarity += Math.Abs(comp1["cavalry"] - comp2["cavalry"]) * 0.4f;
            
            return Math.Min(1f, complementarity);
        }

        private float CalculateMilitaryCoverage(Clan clan1, Clan clan2)
        {
            float coverage = 0.5f; // Base coverage
            
            if (clan1?.Settlements != null && clan2?.Settlements != null)
            {
                var allSettlements = clan1.Settlements.Concat(clan2.Settlements).ToList();
                if (allSettlements.Count > 3)
                {
                    coverage += 0.2f; // Multiple settlements provide better coverage
                }
                
                // Check if settlements are spread across different regions
                var cultures = allSettlements.Select(s => s.Culture).Distinct().Count();
                if (cultures > 1)
                {
                    coverage += 0.3f; // Cross-cultural presence
                }
            }
            
            return Math.Min(1f, coverage);
        }

        private float CalculateTradeRouteSynergy(Clan clan1, Clan clan2)
        {
            float synergy = 0.3f; // Base synergy
            
            if (clan1?.Settlements != null && clan2?.Settlements != null)
            {
                // Check for complementary settlement types
                var clan1Towns = clan1.Settlements.Count(s => s.IsTown);
                var clan1Villages = clan1.Settlements.Count(s => s.IsVillage);
                var clan2Towns = clan2.Settlements.Count(s => s.IsTown);
                var clan2Villages = clan2.Settlements.Count(s => s.IsVillage);
                
                // Towns and villages complement each other well
                if (clan1Towns > 0 && clan2Villages > 0 || clan2Towns > 0 && clan1Villages > 0)
                {
                    synergy += 0.4f;
                }
                
                // Same culture reduces friction
                if (clan1.Culture == clan2.Culture)
                {
                    synergy += 0.3f;
                }
            }
            
            return Math.Min(1f, synergy);
        }

        private float CalculateResourceComplementarity(Clan clan1, Clan clan2)
        {
            float complementarity = 0.4f; // Base complementarity
            
            // Economic status differences can be complementary
            if (clan1?.Gold > 0 && clan2?.Gold > 0)
            {
                float wealthRatio = (float)Math.Min(clan1.Gold, clan2.Gold) / Math.Max(clan1.Gold, clan2.Gold);
                if (wealthRatio < 0.5f)
                {
                    complementarity += 0.3f; // Wealthy and poor clans can complement each other
                }
            }
            
            // Different settlement types provide resource diversity
            var clan1HasTowns = clan1?.Settlements?.Any(s => s.IsTown) == true;
            var clan1HasVillages = clan1?.Settlements?.Any(s => s.IsVillage) == true;
            var clan2HasTowns = clan2?.Settlements?.Any(s => s.IsTown) == true;
            var clan2HasVillages = clan2?.Settlements?.Any(s => s.IsVillage) == true;
            
            if ((clan1HasTowns && clan2HasVillages) || (clan2HasTowns && clan1HasVillages))
            {
                complementarity += 0.3f;
            }
            
            return Math.Min(1f, complementarity);
        }

        private float CalculateMarketAccess(Clan clan1, Clan clan2)
        {
            float marketAccess = 0.3f; // Base access
            
            if (clan1?.Settlements != null && clan2?.Settlements != null)
            {
                int totalTowns = clan1.Settlements.Count(s => s.IsTown) + clan2.Settlements.Count(s => s.IsTown);
                marketAccess += totalTowns * 0.15f; // Each town improves market access
                
                // Cross-kingdom alliances provide broader market access
                if (clan1.Kingdom != clan2.Kingdom)
                {
                    marketAccess += 0.4f;
                }
            }
            
            return Math.Min(1f, marketAccess);
        }

        private float CalculateGeographicProximity(Clan clan1, Clan clan2)
        {
            float proximity = 0.5f; // Default moderate proximity
            
            if (clan1?.Settlements?.Any() == true && clan2?.Settlements?.Any() == true)
            {
                var settlement1 = clan1.Settlements.FirstOrDefault();
                var settlement2 = clan2.Settlements.FirstOrDefault();
                
                if (settlement1 != null && settlement2 != null)
                {
                    float distance = settlement1.Position2D.Distance(settlement2.Position2D);
                    
                    // Closer settlements have higher proximity scores
                    if (distance < 50f) proximity = 0.9f;
                    else if (distance < 100f) proximity = 0.7f;
                    else if (distance < 200f) proximity = 0.5f;
                    else proximity = 0.3f;
                }
            }
            
            return proximity;
        }

        private float CalculateThreatResponse(Clan clan1, Clan clan2)
        {
            float threatResponse = 0.2f; // Base threat response
            
            // Check for common enemies
            if (clan1?.Kingdom != null && clan2?.Kingdom != null)
            {
                var clan1Enemies = clan1.Kingdom.IsAtWarWith.ToList();
                var clan2Enemies = clan2.Kingdom.IsAtWarWith.ToList();
                
                int commonEnemies = clan1Enemies.Intersect(clan2Enemies).Count();
                threatResponse += commonEnemies * 0.3f; // Each common enemy increases threat response
                
                // Being at war with each other reduces threat response
                if (clan1.Kingdom.IsAtWarWith.Contains(clan2.Kingdom))
                {
                    threatResponse -= 0.6f;
                }
            }
            
            return Math.Max(0f, Math.Min(1f, threatResponse));
        }

        private float CalculateTraitCompatibility(Hero hero1, Hero hero2)
        {
            if (hero1 == null || hero2 == null) return 0.5f;
            
            float compatibility = 0.5f; // Base compatibility
            
            var traits1 = hero1.GetHeroTraits();
            var traits2 = hero2.GetHeroTraits();
            
            // Similar honor levels work well together
            int honorDiff = Math.Abs(traits1.Honor - traits2.Honor);
            compatibility += (3 - honorDiff) * 0.1f;
            
            // Calculating heroes work well with anyone
            if (traits1.Calculating > 0 || traits2.Calculating > 0)
            {
                compatibility += 0.2f;
            }
            
            // Generous heroes are easier to work with
            compatibility += (traits1.Generosity + traits2.Generosity) * 0.05f;
            
            return Math.Max(0f, Math.Min(1f, compatibility));
        }

        private float CalculateMarketImpact(SecretAllianceRecord alliance, Clan target)
        {
            float impact = 0.3f; // Base impact
            
            if (alliance.EconomicIntegration > 0.5f)
            {
                impact += alliance.EconomicIntegration * 0.4f;
            }
            
            // Stronger alliances have more market influence
            impact += alliance.Strength * 0.3f;
            
            return Math.Min(1f, impact);
        }

        private void EstablishAlternativeTradeRoutes(SecretAllianceRecord alliance)
        {
            var economicData = _behavior.GetEconomicNetworkData().FirstOrDefault(e => e.AllianceId == alliance.UniqueId);
            if (economicData != null)
            {
                economicData.TradeVolumeMultiplier += 0.1f;
                Debug.Print($"[SecretAlliances] Alternative trade routes established for alliance {alliance.UniqueId}");
            }
        }

        private OperationResult ExecuteDiplomaticInfluenceOperation(SecretAllianceRecord alliance, Clan target)
        {
            var result = new OperationResult { OperationType = AdvancedOperationType.DiplomaticInfluenceOperation };
            
            if (alliance.ReputationScore > 0.6f && alliance.AllianceRank >= 1)
            {
                // Successful diplomatic influence
                var initiator = alliance.GetInitiatorClan();
                var partner = alliance.GetTargetClan();
                
                if (initiator?.Leader != null && partner?.Leader != null && target?.Leader != null)
                {
                    // Improve relations between alliance members and target
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(initiator.Leader, target.Leader, 5);
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(partner.Leader, target.Leader, 5);
                    
                    result.Success = true;
                    result.Message = "Diplomatic influence successfully established";
                    result.ImpactScore = 0.7f;
                }
            }
            else
            {
                result.Success = false;
                result.Message = "Insufficient diplomatic standing for influence operation";
                result.ImpactScore = 0.1f;
            }
            
            return result;
        }

        private OperationResult ExecuteMilitaryCoordinationDrill(SecretAllianceRecord alliance)
        {
            var result = new OperationResult { OperationType = AdvancedOperationType.MilitaryCoordinationDrill };
            
            if (alliance.MilitaryPact && alliance.MilitaryCoordination > 0.3f)
            {
                // Improve military coordination
                alliance.MilitaryCoordination += 0.05f;
                alliance.MilitaryCoordination = Math.Min(1f, alliance.MilitaryCoordination);
                
                result.Success = true;
                result.Message = "Military coordination improved through joint exercises";
                result.ImpactScore = 0.6f;
            }
            else
            {
                result.Success = false;
                result.Message = "Military pact required for coordination drills";
                result.ImpactScore = 0.0f;
            }
            
            return result;
        }

        private OperationResult ExecuteCulturalExchangeProgram(SecretAllianceRecord alliance)
        {
            var result = new OperationResult { OperationType = AdvancedOperationType.CulturalExchangeProgram };
            
            var initiator = alliance.GetInitiatorClan();
            var partner = alliance.GetTargetClan();
            
            if (initiator != null && partner != null)
            {
                // Improve trust and reduce cultural barriers
                alliance.TrustLevel += 0.03f;
                alliance.TrustLevel = Math.Min(1f, alliance.TrustLevel);
                
                if (initiator.Culture != partner.Culture)
                {
                    // Cross-cultural exchange provides additional benefits
                    alliance.ReputationScore += 0.02f;
                    alliance.ReputationScore = Math.Min(1f, alliance.ReputationScore);
                }
                
                result.Success = true;
                result.Message = "Cultural exchange program successfully completed";
                result.ImpactScore = 0.5f;
            }
            else
            {
                result.Success = false;
                result.Message = "Invalid alliance for cultural exchange";
                result.ImpactScore = 0.0f;
            }
            
            return result;
        }

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