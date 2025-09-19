using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances.Models
{
    /// <summary>
    /// Utility-based decision making system for AI clans
    /// Provides rational scoring for alliance, betrayal, and assistance decisions
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public static class UtilityModel
    {
        #region Alliance Decision Utilities

        /// <summary>
        /// Calculate utility score for joining an alliance
        /// </summary>
        public static float CalculateAllianceJoinUtility(Clan evaluatingClan, Alliance alliance, List<Clan> potentialMembers = null)
        {
            if (evaluatingClan == null || alliance == null)
                return 0f;

            float utility = 0f;
            var members = potentialMembers ?? alliance.GetMemberClans();

            // Strategic strength gain
            utility += CalculateStrengthUtility(evaluatingClan, members);

            // Relationship benefits
            utility += CalculateRelationshipUtility(evaluatingClan, members);

            // Economic benefits
            utility += CalculateEconomicUtility(evaluatingClan, members);

            // Risk assessment
            utility -= CalculateRiskPenalty(evaluatingClan, alliance, members);

            // Trait-based personality adjustments
            utility = ApplyPersonalityModifiers(evaluatingClan, utility, "alliance_join");

            return MathF.Max(0f, MathF.Min(100f, utility));
        }

        /// <summary>
        /// Calculate utility score for creating a new alliance
        /// </summary>
        public static float CalculateAllianceCreationUtility(Clan evaluatingClan, Clan targetClan)
        {
            if (evaluatingClan == null || targetClan == null || evaluatingClan == targetClan)
                return 0f;

            float utility = 0f;

            // Base utility from potential cooperation
            utility += CalculateCooperationPotential(evaluatingClan, targetClan);

            // Strategic positioning
            utility += CalculateStrategicPositioning(evaluatingClan, targetClan);

            // Threat mitigation
            utility += CalculateThreatMitigation(evaluatingClan, targetClan);

            // Opportunity cost
            utility -= CalculateOpportunityCost(evaluatingClan, targetClan);

            // Apply personality modifiers
            utility = ApplyPersonalityModifiers(evaluatingClan, utility, "alliance_create");

            return MathF.Max(0f, MathF.Min(100f, utility));
        }

        #endregion

        #region Assistance Decision Utilities

        /// <summary>
        /// Calculate utility for accepting an assistance request
        /// </summary>
        public static float CalculateAssistanceUtility(Clan evaluatingClan, Request request, Alliance alliance = null)
        {
            if (evaluatingClan == null || request == null)
                return 0f;

            float utility = 0f;
            var requesterClan = request.GetRequesterClan();

            // Relationship benefit
            if (requesterClan != null)
            {
                utility += GetRelationshipScore(evaluatingClan, requesterClan) * 0.3f;
            }

            // Alliance loyalty bonus
            if (alliance != null)
            {
                utility += alliance.TrustLevel * 20f;
                utility += (1f - alliance.SecrecyLevel) * 10f; // Less secret = more obligation
            }

            // Request-specific utility
            utility += CalculateRequestSpecificUtility(evaluatingClan, request);

            // Risk vs reward assessment
            utility += (request.GetEstimatedReward() / 100f) - (request.RiskLevel * 15f);

            // Capability assessment
            utility += CalculateCapabilityScore(evaluatingClan, request) * 0.2f;

            // Apply personality modifiers
            utility = ApplyPersonalityModifiers(evaluatingClan, utility, "assistance");

            return MathF.Max(0f, MathF.Min(100f, utility));
        }

        /// <summary>
        /// Calculate utility for betraying an ally in battle
        /// </summary>
        public static float CalculateBetrayalUtility(Clan evaluatingClan, Clan allyTarget, Clan potentialBeneficiary, Alliance alliance = null)
        {
            if (evaluatingClan == null || allyTarget == null)
                return 0f;

            float utility = 0f;

            // Immediate strategic gain
            if (potentialBeneficiary != null)
            {
                utility += CalculateStrategicGain(evaluatingClan, potentialBeneficiary, allyTarget);
            }

            // Current alliance dissatisfaction
            if (alliance != null)
            {
                utility += (1f - alliance.TrustLevel) * 30f; // Low trust = higher betrayal utility
                utility += CalculateAllianceDisatisfaction(evaluatingClan, alliance) * 20f;
            }

            // Power dynamics
            utility += CalculatePowerDynamicsUtility(evaluatingClan, allyTarget, potentialBeneficiary);

            // Severe personality penalties for honorable characters
            utility = ApplyBetrayalPersonalityModifiers(evaluatingClan, utility);

            // Long-term reputation cost
            utility -= CalculateReputationCost(evaluatingClan) * 0.5f;

            return MathF.Max(0f, MathF.Min(100f, utility));
        }

        #endregion

        #region Investment Decision Utilities

        /// <summary>
        /// Calculate utility for investing in secrecy/counter-intelligence
        /// </summary>
        public static float CalculateSecrecyInvestmentUtility(Clan evaluatingClan, Alliance alliance, int cost)
        {
            if (evaluatingClan == null || alliance == null)
                return 0f;

            float utility = 0f;

            // Current exposure risk
            var exposureRisk = 1f - alliance.SecrecyLevel;
            utility += exposureRisk * 40f; // Higher risk = higher investment utility

            // Economic capability
            var affordability = MathF.Min(1f, evaluatingClan.Gold / (float)cost);
            utility += affordability * 20f;

            // Alliance value protection
            utility += alliance.TrustLevel * 15f; // More valuable alliances worth protecting

            // Recent leak history
            if (alliance.HistoryLog.Any(entry => entry.Contains("leaked")))
            {
                utility += 25f; // Recent leaks increase investment urgency
            }

            // Personality factors
            utility = ApplyPersonalityModifiers(evaluatingClan, utility, "secrecy_investment");

            return MathF.Max(0f, MathF.Min(100f, utility));
        }

        #endregion

        #region Private Utility Calculation Methods

        private static float CalculateStrengthUtility(Clan evaluatingClan, List<Clan> potentialAllies)
        {
            if (evaluatingClan == null) return 0f;

            var ownStrength = evaluatingClan.TotalStrength;
            var alliedStrength = potentialAllies?.Sum(c => c?.TotalStrength ?? 0) ?? 0;
            var combinedStrength = ownStrength + alliedStrength;

            // Utility based on strength multiplier
            var strengthMultiplier = combinedStrength / (float)ownStrength;
            return MathF.Min(30f, (strengthMultiplier - 1f) * 15f); // Cap at 30 points
        }

        private static float CalculateRelationshipUtility(Clan evaluatingClan, List<Clan> potentialAllies)
        {
            if (evaluatingClan?.Leader == null || potentialAllies == null) return 0f;

            float totalRelationValue = 0f;
            int validRelations = 0;

            foreach (var ally in potentialAllies)
            {
                if (ally?.Leader != null && ally != evaluatingClan)
                {
                    var relation = evaluatingClan.Leader.GetRelation(ally.Leader);
                    totalRelationValue += relation / 5f; // Convert -100/+100 to -20/+20 range
                    validRelations++;
                }
            }

            return validRelations > 0 ? totalRelationValue / validRelations : 0f;
        }

        private static float CalculateEconomicUtility(Clan evaluatingClan, List<Clan> potentialAllies)
        {
            if (evaluatingClan == null || potentialAllies == null) return 0f;

            float utility = 0f;
            
            // Trade potential
            var wealthyAllies = potentialAllies.Count(c => c != null && c.Gold > 20000);
            utility += wealthyAllies * 5f;

            // Settlement synergies
            var evaluatingSettlements = evaluatingClan.Settlements?.Count ?? 0;
            var alliedSettlements = potentialAllies.Sum(c => c?.Settlements?.Count ?? 0);
            if (evaluatingSettlements > 0 && alliedSettlements > 0)
            {
                utility += MathF.Min(10f, alliedSettlements * 2f);
            }

            return utility;
        }

        private static float CalculateRiskPenalty(Clan evaluatingClan, Alliance alliance, List<Clan> members)
        {
            float risk = 0f;

            // Multi-kingdom risk
            var kingdoms = members.Where(c => c?.Kingdom != null).Select(c => c.Kingdom).Distinct().ToList();
            if (kingdoms.Count > 1)
            {
                risk += 15f; // Cross-kingdom alliances are risky

                // At-war kingdoms are extremely risky
                for (int i = 0; i < kingdoms.Count - 1; i++)
                {
                    for (int j = i + 1; j < kingdoms.Count; j++)
                    {
                        if (kingdoms[i].IsAtWarWith(kingdoms[j]))
                        {
                            risk += 25f;
                            break;
                        }
                    }
                }
            }

            // Secrecy risk
            if (alliance != null)
            {
                risk += (1f - alliance.SecrecyLevel) * 20f;
            }

            // Size risk (more members = harder to keep secret)
            risk += (members.Count - 2) * 5f;

            return risk;
        }

        private static float CalculateCooperationPotential(Clan evaluatingClan, Clan targetClan)
        {
            if (evaluatingClan?.Leader == null || targetClan?.Leader == null) return 0f;

            float potential = 0f;

            // Relationship foundation
            var relation = evaluatingClan.Leader.GetRelation(targetClan.Leader);
            potential += relation * 0.2f;

            // Complementary strengths
            var strengthRatio = (float)targetClan.TotalStrength / evaluatingClan.TotalStrength;
            if (strengthRatio >= 0.5f && strengthRatio <= 2f) // Similar strength = good cooperation
            {
                potential += 15f;
            }

            // Geographic proximity (simplified)
            if (evaluatingClan.Kingdom == targetClan.Kingdom)
            {
                potential += 10f;
            }

            return potential;
        }

        private static float CalculateStrategicPositioning(Clan evaluatingClan, Clan targetClan)
        {
            float positioning = 0f;

            // Kingdom considerations
            if (evaluatingClan.Kingdom != targetClan.Kingdom)
            {
                if (evaluatingClan.Kingdom != null && targetClan.Kingdom != null)
                {
                    if (evaluatingClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
                    {
                        positioning += 20f; // Enemy alliance = very strategic
                    }
                    else
                    {
                        positioning += 10f; // Cross-kingdom alliance = somewhat strategic
                    }
                }
            }

            return positioning;
        }

        private static float CalculateThreatMitigation(Clan evaluatingClan, Clan targetClan)
        {
            // Simple threat assessment - would be more complex in full implementation
            float mitigation = 0f;

            if (targetClan.TotalStrength > evaluatingClan.TotalStrength * 0.8f)
            {
                mitigation += 15f; // Allying with strong clans reduces threat
            }

            return mitigation;
        }

        private static float CalculateOpportunityCost(Clan evaluatingClan, Clan targetClan)
        {
            float cost = 0f;

            // Cost of potential better alternatives (simplified)
            if (evaluatingClan.Gold < 10000)
            {
                cost += 10f; // Poor clans have higher opportunity cost
            }

            return cost;
        }

        private static float CalculateRequestSpecificUtility(Clan evaluatingClan, Request request)
        {
            float utility = 0f;

            switch (request.Type)
            {
                case RequestType.BattleAssistance:
                    utility += CalculateBattleAssistanceUtility(evaluatingClan, request);
                    break;
                case RequestType.Tribute:
                    utility += CalculateTributeUtility(evaluatingClan, request);
                    break;
                case RequestType.TradeConvoyEscort:
                    utility += CalculateEscortUtility(evaluatingClan, request);
                    break;
                default:
                    utility += 5f; // Base utility for other requests
                    break;
            }

            return utility;
        }

        private static float CalculateBattleAssistanceUtility(Clan evaluatingClan, Request request)
        {
            float utility = 0f;

            // Military strength considerations
            if (evaluatingClan.TotalStrength > 300) // Can afford to help
            {
                utility += 10f;
            }

            // War status
            if (evaluatingClan.Kingdom?.IsAtWarWith(request.GetRequesterClan()?.Kingdom) == false)
            {
                utility += 15f; // Not at war = easier to help
            }

            return utility;
        }

        private static float CalculateTributeUtility(Clan evaluatingClan, Request request)
        {
            float utility = 0f;

            // Wealth assessment
            if (evaluatingClan.Gold > request.ProposedReward * 2)
            {
                utility += 15f; // Can afford it
            }
            else if (evaluatingClan.Gold < request.ProposedReward)
            {
                utility -= 20f; // Can't afford it
            }

            return utility;
        }

        private static float CalculateEscortUtility(Clan evaluatingClan, Request request)
        {
            float utility = 0f;

            // Mobile party availability (simplified)
            utility += 10f; // Base utility for escort missions

            return utility;
        }

        private static float CalculateCapabilityScore(Clan evaluatingClan, Request request)
        {
            if (evaluatingClan == null) return 0f;

            float score = 0f;

            // Military capability
            score += MathF.Min(50f, evaluatingClan.TotalStrength / 20f);

            // Economic capability
            score += MathF.Min(30f, evaluatingClan.Gold / 1000f);

            // Leadership capability
            if (evaluatingClan.Leader != null)
            {
                score += evaluatingClan.Leader.GetSkillValue(DefaultSkills.Leadership) / 5f;
                score += evaluatingClan.Leader.GetSkillValue(DefaultSkills.Tactics) / 10f;
            }

            return score;
        }

        private static float GetRelationshipScore(Clan clan1, Clan clan2)
        {
            if (clan1?.Leader == null || clan2?.Leader == null) return 0f;
            
            return clan1.Leader.GetRelation(clan2.Leader) / 5f; // Convert to -20/+20 range
        }

        private static float ApplyPersonalityModifiers(Clan clan, float baseUtility, string decisionType)
        {
            if (clan?.Leader == null) return baseUtility;

            float modifier = 1f;
            var leader = clan.Leader;

            switch (decisionType)
            {
                case "alliance_join":
                case "alliance_create":
                    if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                        modifier += 0.2f; // Calculating leaders more likely to form alliances
                    if (leader.GetTraitLevel(DefaultTraits.Honor) > 0)
                        modifier -= 0.1f; // Honorable leaders more cautious about secret alliances
                    if (leader.GetTraitLevel(DefaultTraits.Generosity) > 0)
                        modifier += 0.15f; // Generous leaders more cooperative
                    break;

                case "assistance":
                    if (leader.GetTraitLevel(DefaultTraits.Generosity) > 0)
                        modifier += 0.3f; // Generous leaders more helpful
                    if (leader.GetTraitLevel(DefaultTraits.Honor) > 0)
                        modifier += 0.2f; // Honorable leaders honor commitments
                    if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                        modifier -= 0.1f; // Calculating leaders more selective
                    break;

                case "secrecy_investment":
                    if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                        modifier += 0.4f; // Calculating leaders invest in protection
                    break;
            }

            return baseUtility * modifier;
        }

        private static float ApplyBetrayalPersonalityModifiers(Clan clan, float baseUtility)
        {
            if (clan?.Leader == null) return baseUtility;

            var leader = clan.Leader;
            float modifier = 1f;

            // Honor strongly opposes betrayal
            var honorLevel = leader.GetTraitLevel(DefaultTraits.Honor);
            if (honorLevel > 0)
                modifier -= honorLevel * 0.8f; // Very strong penalty
            else if (honorLevel < 0)
                modifier += Math.Abs(honorLevel) * 0.3f; // Dishonorable = more likely to betray

            // Mercy affects betrayal likelihood
            if (leader.GetTraitLevel(DefaultTraits.Mercy) < 0)
                modifier += 0.2f; // Cruel leaders more likely to betray

            // Calculating leaders weigh betrayal more carefully
            if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                modifier += 0.1f; // Slight increase as they might see strategic value

            return MathF.Max(0f, baseUtility * modifier);
        }

        // Additional helper methods would be implemented here for completeness
        private static float CalculateStrategicGain(Clan evaluating, Clan beneficiary, Clan target) => 15f; // Simplified
        private static float CalculateAllianceDisatisfaction(Clan clan, Alliance alliance) => (1f - alliance.TrustLevel); // Simplified
        private static float CalculatePowerDynamicsUtility(Clan evaluating, Clan target, Clan beneficiary) => 10f; // Simplified
        private static float CalculateReputationCost(Clan clan) => 20f; // Simplified

        #endregion
    }
}