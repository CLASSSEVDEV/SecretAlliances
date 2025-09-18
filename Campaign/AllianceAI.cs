using System;
using System.Collections.Generic;
using System.Linq;
using SecretAlliances.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances.Campaign
{
    /// <summary>
    /// Enhanced AI system for rational alliance decision-making
    /// Implements utility-based AI for creating, maintaining, and betraying alliances
    /// </summary>
    public class AllianceAI
    {
        private readonly SecretAllianceBehavior _behavior;
        private readonly AllianceConfig _config;

        public AllianceAI(SecretAllianceBehavior behavior)
        {
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
            _config = AllianceConfig.Instance;
        }

        #region Alliance Formation AI

        /// <summary>
        /// Evaluates whether a clan should form a new alliance
        /// Uses utility-based decision making with rational factors
        /// </summary>
        public bool ShouldCreateAlliance(Clan proposerClan, Clan targetClan)
        {
            if (proposerClan == null || targetClan == null) return false;
            if (proposerClan == targetClan) return false;
            if (proposerClan == Clan.PlayerClan) return false; // Player controls their own decisions

            // Calculate utility score for alliance formation
            float utilityScore = CalculateAllianceFormationUtility(proposerClan, targetClan);
            
            // Add randomness for believable AI behavior
            float randomFactor = MBRandom.RandomFloat * 0.2f - 0.1f; // ±10%
            utilityScore += randomFactor;

            // Decision threshold based on clan personality and circumstances
            float threshold = GetDecisionThreshold(proposerClan, "alliance_formation");

            bool shouldCreate = utilityScore > threshold;

            if (_config.DebugVerbose)
            {
                Debug.Print($"[SecretAlliances AI] {proposerClan.Name} evaluating alliance with {targetClan.Name}: " +
                          $"Utility={utilityScore:F3}, Threshold={threshold:F3}, Decision={shouldCreate}");
            }

            return shouldCreate;
        }

        /// <summary>
        /// Calculates utility score for alliance formation
        /// </summary>
        private float CalculateAllianceFormationUtility(Clan proposerClan, Clan targetClan)
        {
            float utility = 0f;
            var factors = new List<string>();

            // Military utility - mutual protection and strength
            float militaryUtility = CalculateMilitaryUtility(proposerClan, targetClan);
            utility += militaryUtility * 0.3f;
            factors.Add($"Military: {militaryUtility:F2}");

            // Economic utility - trade benefits and resource sharing
            float economicUtility = CalculateEconomicUtility(proposerClan, targetClan);
            utility += economicUtility * 0.25f;
            factors.Add($"Economic: {economicUtility:F2}");

            // Political utility - diplomatic advantages
            float politicalUtility = CalculatePoliticalUtility(proposerClan, targetClan);
            utility += politicalUtility * 0.25f;
            factors.Add($"Political: {politicalUtility:F2}");

            // Security utility - protection from threats
            float securityUtility = CalculateSecurityUtility(proposerClan, targetClan);
            utility += securityUtility * 0.2f;
            factors.Add($"Security: {securityUtility:F2}");

            if (_config.DebugVerbose)
            {
                Debug.Print($"[SecretAlliances AI] Alliance utility breakdown for {proposerClan.Name} -> {targetClan.Name}: " +
                          string.Join(", ", factors) + $" = {utility:F3}");
            }

            return utility;
        }

        #endregion

        #region Betrayal AI

        /// <summary>
        /// Evaluates whether a clan should betray an existing alliance
        /// Based on rational factors like changing circumstances and opportunities
        /// </summary>
        public bool ShouldBetrayAlliance(Clan clan, SecretAllianceRecord alliance)
        {
            if (clan == null || alliance == null) return false;
            if (clan == Clan.PlayerClan) return false; // Player controls their own decisions
            if (!alliance.IsActive) return false;

            // Calculate betrayal utility
            float betrayalUtility = CalculateBetrayalUtility(clan, alliance);
            
            // Add personality-based modifiers
            float personalityModifier = GetBetrayalPersonalityModifier(clan);
            betrayalUtility *= personalityModifier;

            // Decision threshold - betrayal should be harder than formation
            float threshold = GetDecisionThreshold(clan, "betrayal");

            bool shouldBetray = betrayalUtility > threshold;

            if (_config.DebugVerbose && betrayalUtility > 0.3f) // Only log significant betrayal considerations
            {
                Debug.Print($"[SecretAlliances AI] {clan.Name} considering betrayal: " +
                          $"Utility={betrayalUtility:F3}, Threshold={threshold:F3}, Decision={shouldBetray}");
            }

            return shouldBetray;
        }

        /// <summary>
        /// Calculates utility score for betraying an alliance
        /// </summary>
        private float CalculateBetrayalUtility(Clan clan, SecretAllianceRecord alliance)
        {
            float utility = 0f;
            var otherClan = alliance.GetInitiatorClan() == clan ? alliance.GetTargetClan() : alliance.GetInitiatorClan();
            
            if (otherClan == null) return 0f;

            // Opportunity cost - better alliances available
            float opportunityCost = CalculateOpportunityCost(clan, otherClan);
            utility += opportunityCost * 0.3f;

            // Power imbalance - other clan became too powerful or too weak
            float powerImbalance = CalculatePowerImbalance(clan, otherClan);
            utility += powerImbalance * 0.25f;

            // Changing political situation
            float politicalChange = CalculatePoliticalChanges(clan, otherClan);
            utility += politicalChange * 0.2f;

            // Alliance burden - costs outweigh benefits
            float allianceBurden = CalculateAllianceBurden(clan, alliance);
            utility += allianceBurden * 0.15f;

            // External pressure - threats or incentives to betray
            float externalPressure = CalculateExternalPressure(clan, otherClan);
            utility += externalPressure * 0.1f;

            return utility;
        }

        #endregion

        #region Aid Request AI

        /// <summary>
        /// Evaluates whether a clan should request aid from allies
        /// </summary>
        public bool ShouldRequestAid(Clan clan, SecretAllianceRecord alliance, string aidType)
        {
            if (clan == null || alliance == null) return false;
            if (clan == Clan.PlayerClan) return false; // Player makes their own requests

            float desperation = CalculateDesperationLevel(clan);
            float allianceStrength = alliance.Strength;
            float trustLevel = alliance.TrustLevel;

            // Clans are more likely to request aid when desperate and trust is high
            float requestProbability = (desperation * 0.5f) + (trustLevel * 0.3f) + (allianceStrength * 0.2f);

            // Adjust based on aid type
            switch (aidType.ToLower())
            {
                case "military":
                    requestProbability *= clan.Kingdom?.IsAtWarWith(Clan.PlayerClan?.Kingdom) == true ? 1.5f : 1.0f;
                    break;
                case "economic":
                    requestProbability *= clan.Gold < 5000 ? 1.3f : 0.8f;
                    break;
                case "intelligence":
                    requestProbability *= 1.1f; // Always somewhat valuable
                    break;
            }

            return MBRandom.RandomFloat < Math.Min(0.8f, requestProbability);
        }

        /// <summary>
        /// Evaluates whether a clan should provide aid when requested
        /// </summary>
        public bool ShouldProvideAid(Clan clan, SecretAllianceRecord alliance, string aidType, float aidAmount)
        {
            if (clan == null || alliance == null) return false;
            if (clan == Clan.PlayerClan) return false; // Player makes their own decisions

            float trustLevel = alliance.TrustLevel;
            float allianceStrength = alliance.Strength;
            float clanCapacity = CalculateClanCapacity(clan, aidType);

            // Willingness based on trust, alliance strength, and capacity
            float willingness = (trustLevel * 0.4f) + (allianceStrength * 0.3f) + (clanCapacity * 0.3f);

            // Adjust based on aid type and amount
            switch (aidType.ToLower())
            {
                case "military":
                    willingness *= clan.Kingdom?.IsAtWarWith(Clan.PlayerClan?.Kingdom) == true ? 1.2f : 1.0f;
                    break;
                case "economic":
                    float affordabilityRatio = aidAmount / Math.Max(1f, clan.Gold);
                    willingness *= Math.Max(0.1f, 1f - affordabilityRatio); // Less willing if aid is too expensive
                    break;
                case "intelligence":
                    willingness *= 1.1f; // Intelligence is relatively cheap to provide
                    break;
            }

            return MBRandom.RandomFloat < Math.Min(0.9f, willingness);
        }

        #endregion

        #region Utility Calculation Helpers

        private float CalculateMilitaryUtility(Clan clan1, Clan clan2)
        {
            float utility = 0f;

            // Strength complementarity
            float strengthRatio = Math.Min(clan1.TotalStrength, clan2.TotalStrength) / 
                                 Math.Max(clan1.TotalStrength, clan2.TotalStrength);
            utility += strengthRatio * 0.4f; // Balanced alliances are better

            // Geographic military coverage
            if (clan1.FactionMidSettlement != null && clan2.FactionMidSettlement != null)
            {
                float distance = clan1.FactionMidSettlement.Position2D.Distance(clan2.FactionMidSettlement.Position2D);
                float coverageBonus = Math.Max(0f, (200f - distance) / 200f); // Better if within 200 units
                utility += coverageBonus * 0.3f;
            }

            // Common enemies provide military utility
            int commonEnemies = CountCommonEnemies(clan1, clan2);
            utility += Math.Min(0.3f, commonEnemies * 0.1f);

            return Math.Min(1f, utility);
        }

        private float CalculateEconomicUtility(Clan clan1, Clan clan2)
        {
            float utility = 0f;

            // Wealth complementarity - wealthy clans can support poorer ones
            float wealthDifference = Math.Abs(clan1.Gold - clan2.Gold) / Math.Max(clan1.Gold + clan2.Gold, 1f);
            utility += (1f - wealthDifference) * 0.4f; // Similar wealth levels are better

            // Trade route benefits (simplified)
            if (clan1.FactionMidSettlement != null && clan2.FactionMidSettlement != null)
            {
                float distance = clan1.FactionMidSettlement.Position2D.Distance(clan2.FactionMidSettlement.Position2D);
                float tradeBonus = Math.Max(0f, (150f - distance) / 150f) * 0.3f; // Closer is better for trade
                utility += tradeBonus;
            }

            // Settlement synergy - different settlement types complement each other
            utility += CalculateSettlementSynergy(clan1, clan2) * 0.3f;

            return Math.Min(1f, utility);
        }

        private float CalculatePoliticalUtility(Clan clan1, Clan clan2)
        {
            float utility = 0f;

            // Relationship bonus
            if (clan1.Leader != null && clan2.Leader != null)
            {
                float relation = clan1.Leader.GetRelation(clan2.Leader);
                utility += Math.Max(0f, relation / 100f) * 0.4f;
            }

            // Kingdom situation
            if (clan1.Kingdom == clan2.Kingdom)
            {
                utility += 0.3f; // Same kingdom bonus
            }
            else if (clan1.Kingdom?.IsAtWarWith(clan2.Kingdom) == true)
            {
                utility -= 0.5f; // War penalty
            }

            // Cultural compatibility
            if (clan1.Culture == clan2.Culture)
            {
                utility += 0.2f;
            }

            // Trait compatibility
            utility += CalculateTraitCompatibility(clan1, clan2) * 0.1f;

            return Math.Max(-0.5f, Math.Min(1f, utility));
        }

        private float CalculateSecurityUtility(Clan clan1, Clan clan2)
        {
            float utility = 0f;

            // Mutual protection value
            float threatLevel1 = CalculateThreatLevel(clan1);
            float threatLevel2 = CalculateThreatLevel(clan2);
            utility += (threatLevel1 + threatLevel2) * 0.3f; // Higher threat = more utility from alliance

            // Alliance network effects
            var clan1Alliances = _behavior.GetAlliancesForClan(clan1);
            var clan2Alliances = _behavior.GetAlliancesForClan(clan2);
            
            float networkBonus = Math.Min(0.2f, (clan1Alliances.Count + clan2Alliances.Count) * 0.05f);
            utility += networkBonus;

            return Math.Min(1f, utility);
        }

        private float GetDecisionThreshold(Clan clan, string decisionType)
        {
            float baseThreshold = decisionType switch
            {
                "alliance_formation" => 0.6f,
                "betrayal" => 0.8f, // Betrayal should be harder
                "aid_request" => 0.5f,
                "aid_provision" => 0.4f,
                _ => 0.6f
            };

            // Adjust based on clan traits and circumstances
            if (clan.Leader != null)
            {
                int honor = clan.Leader.GetTraitLevel(DefaultTraits.Honor);
                int calculating = clan.Leader.GetTraitLevel(DefaultTraits.Calculating);

                baseThreshold += honor * 0.1f; // Honorable clans are more loyal
                baseThreshold -= calculating * 0.05f; // Calculating clans are more opportunistic
            }

            // Desperate clans have lower thresholds
            float desperation = CalculateDesperationLevel(clan);
            baseThreshold -= desperation * 0.2f;

            return Math.Max(0.1f, Math.Min(0.9f, baseThreshold));
        }

        private float CalculateDesperationLevel(Clan clan)
        {
            float desperation = 0f;

            // Financial desperation
            if (clan.Gold < 1000) desperation += 0.3f;
            else if (clan.Gold < 5000) desperation += 0.1f;

            // Military desperation
            if (clan.TotalStrength < 50) desperation += 0.3f;
            else if (clan.TotalStrength < 100) desperation += 0.1f;

            // Political desperation (at war)
            if (clan.Kingdom?.IsAtWarWith(Clan.PlayerClan?.Kingdom) == true)
                desperation += 0.2f;

            // Settlement loss desperation
            if (clan.Settlements.Count == 0) desperation += 0.2f;

            return Math.Min(1f, desperation);
        }

        private float GetBetrayalPersonalityModifier(Clan clan)
        {
            float modifier = 1f;

            if (clan.Leader != null)
            {
                int honor = clan.Leader.GetTraitLevel(DefaultTraits.Honor);
                int calculating = clan.Leader.GetTraitLevel(DefaultTraits.Calculating);

                modifier -= honor * 0.3f; // Honorable clans less likely to betray
                modifier += calculating * 0.2f; // Calculating clans more opportunistic
            }

            return Math.Max(0.1f, Math.Min(2f, modifier));
        }

        // Additional helper methods for specific calculations
        private int CountCommonEnemies(Clan clan1, Clan clan2)
        {
            int count = 0;
            foreach (var kingdom in Kingdom.All)
            {
                if (clan1.Kingdom?.IsAtWarWith(kingdom) == true && 
                    clan2.Kingdom?.IsAtWarWith(kingdom) == true)
                {
                    count++;
                }
            }
            return count;
        }

        private float CalculateSettlementSynergy(Clan clan1, Clan clan2)
        {
            // Simplified settlement synergy calculation
            float synergy = 0f;
            
            bool clan1HasTown = clan1.Settlements.Any(s => s.IsTown);
            bool clan1HasCastle = clan1.Settlements.Any(s => s.IsCastle);
            bool clan2HasTown = clan2.Settlements.Any(s => s.IsTown);
            bool clan2HasCastle = clan2.Settlements.Any(s => s.IsCastle);

            if (clan1HasTown && clan2HasCastle) synergy += 0.2f;
            if (clan1HasCastle && clan2HasTown) synergy += 0.2f;

            return synergy;
        }

        private float CalculateTraitCompatibility(Clan clan1, Clan clan2)
        {
            if (clan1.Leader == null || clan2.Leader == null) return 0f;

            float compatibility = 0f;
            
            // Honor compatibility
            int honor1 = clan1.Leader.GetTraitLevel(DefaultTraits.Honor);
            int honor2 = clan2.Leader.GetTraitLevel(DefaultTraits.Honor);
            compatibility += (1f - Math.Abs(honor1 - honor2) / 5f) * 0.3f;

            // Calculating compatibility  
            int calc1 = clan1.Leader.GetTraitLevel(DefaultTraits.Calculating);
            int calc2 = clan2.Leader.GetTraitLevel(DefaultTraits.Calculating);
            compatibility += (1f - Math.Abs(calc1 - calc2) / 5f) * 0.2f;

            return compatibility;
        }

        private float CalculateThreatLevel(Clan clan)
        {
            float threat = 0f;

            // Military threat
            if (clan.TotalStrength < 100) threat += 0.3f;
            
            // Political threat (wars)
            if (clan.Kingdom != null)
            {
                int enemyKingdoms = Kingdom.All.Count(k => clan.Kingdom.IsAtWarWith(k));
                threat += enemyKingdoms * 0.2f;
            }

            // Economic threat
            if (clan.Gold < 5000) threat += 0.2f;

            return Math.Min(1f, threat);
        }

        private float CalculateClanCapacity(Clan clan, string aidType)
        {
            return aidType.ToLower() switch
            {
                "military" => Math.Min(1f, clan.TotalStrength / 200f),
                "economic" => Math.Min(1f, clan.Gold / 20000f),
                "intelligence" => 0.7f, // Most clans can provide some intelligence
                _ => 0.5f
            };
        }

        // Placeholder implementations for betrayal utility calculations
        private float CalculateOpportunityCost(Clan clan, Clan currentAlly) => 0.2f; // Simplified
        private float CalculatePowerImbalance(Clan clan, Clan ally) => 0.1f; // Simplified  
        private float CalculatePoliticalChanges(Clan clan, Clan ally) => 0.1f; // Simplified
        private float CalculateAllianceBurden(Clan clan, SecretAllianceRecord alliance) => 0.1f; // Simplified
        private float CalculateExternalPressure(Clan clan, Clan ally) => 0.05f; // Simplified

        #endregion
    }
}