using SecretAlliances.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Core service for managing secret alliances
    /// Provides clean API for alliance creation, management, and validation
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public class AllianceService : CampaignBehaviorBase
    {
        private List<Alliance> _alliances = new List<Alliance>();
        private Dictionary<string, CampaignTime> _proposalCooldowns = new Dictionary<string, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(this, OnClanDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_Alliances_v2", ref _alliances);
            dataStore.SyncData("SecretAlliances_ProposalCooldowns", ref _proposalCooldowns);
        }

        private void OnDailyTick()
        {
            // Clean up expired cooldowns
            var expiredCooldowns = _proposalCooldowns.Where(kvp => CampaignTime.Now > kvp.Value).ToList();
            foreach (var expired in expiredCooldowns)
            {
                _proposalCooldowns.Remove(expired.Key);
            }

            // Process alliance decay and maintenance
            ProcessDailyAllianceUpdates();
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            // Kingdom changes affect alliance dynamics but don't break them
            var clanAlliances = GetAlliancesForClan(clan);
            foreach (var alliance in clanAlliances)
            {
                alliance.AddHistoryEntry($"{clan.Name} changed from {oldKingdom?.Name.ToString() ?? "no kingdom"} to {newKingdom?.Name.ToString() ?? "no kingdom"}");

                // Adjust trust based on circumstances
                if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom)
                {
                    alliance.TrustLevel += 0.05f; // Joining a kingdom can help alliance goals
                }
                else if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.LeaveWithRebellion)
                {
                    alliance.TrustLevel -= 0.1f; // Rebellion creates instability
                }
            }
        }

        private void OnClanDestroyed(Clan clan)
        {
            // Remove destroyed clans from all alliances
            var affectedAlliances = GetAlliancesForClan(clan);
            foreach (var alliance in affectedAlliances)
            {
                alliance.RemoveMember(clan);
            }
        }

        #region Public API Methods

        /// <summary>
        /// Create a new alliance between multiple clans
        /// </summary>
        public Alliance CreateAlliance(string name, List<Clan> members, Clan leader, string purpose = "Mutual cooperation")
        {
            if (members == null || members.Count < 2)
                return null;

            if (leader == null || !members.Contains(leader))
                leader = members[0];

            var alliance = new Alliance(name, members, leader, purpose);
            _alliances.Add(alliance);

            // Apply initial relationship bonuses
            foreach (var member in members)
            {
                foreach (var otherMember in members)
                {
                    if (member != otherMember && member.Leader != null && otherMember.Leader != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(member.Leader, otherMember.Leader, 5);
                    }
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"Secret alliance '{name}' has been formed!",
                Color.FromUint(0x00F16D26)));

            return alliance;
        }

        /// <summary>
        /// Propose an alliance to another clan (includes cooldown and reputation checks)
        /// </summary>
        public bool ProposeAlliance(Clan proposerClan, Clan targetClan, string allianceName = null)
        {
            if (!CanProposeAlliance(proposerClan, targetClan))
                return false;

            // Set cooldown to prevent spam
            var cooldownKey = $"{proposerClan.Id}_{targetClan.Id}";
            _proposalCooldowns[cooldownKey] = CampaignTime.DaysFromNow(7); // 7 day cooldown

            // Calculate acceptance chance
            var acceptanceChance = CalculateAllianceAcceptanceChance(proposerClan, targetClan);

            if (MBRandom.RandomFloat < acceptanceChance)
            {
                // Auto-accept for high reputation/relationship
                var name = allianceName ?? $"Alliance of {proposerClan.Name} and {targetClan.Name}";
                CreateAlliance(name, new List<Clan> { proposerClan, targetClan }, proposerClan);
                return true;
            }
            else
            {
                // Store as pending proposal (would be handled by dialog system)
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{targetClan.Name} is considering your alliance proposal...",
                    Colors.Yellow));
                return false;
            }
        }

        /// <summary>
        /// Check if two clans can form an alliance
        /// </summary>
        public bool CanProposeAlliance(Clan proposerClan, Clan targetClan)
        {
            if (proposerClan == null || targetClan == null || proposerClan == targetClan)
                return false;

            if (proposerClan.IsEliminated || targetClan.IsEliminated)
                return false;

            // Check if alliance already exists
            if (GetAlliance(proposerClan, targetClan) != null)
                return false;

            // Check cooldown
            var cooldownKey = $"{proposerClan.Id}_{targetClan.Id}";
            if (_proposalCooldowns.ContainsKey(cooldownKey) && CampaignTime.Now < _proposalCooldowns[cooldownKey])
                return false;

            // Check if clans are at war
            if (proposerClan.Kingdom != null && targetClan.Kingdom != null &&
                proposerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
            {
                // War doesn't prevent secret alliances, but makes them riskier
                return true;
            }

            return true;
        }

        /// <summary>
        /// Get alliance between two specific clans
        /// </summary>
        public Alliance GetAlliance(Clan clan1, Clan clan2)
        {
            if (clan1 == null || clan2 == null)
                return null;

            return _alliances.FirstOrDefault(a => a.IsActive &&
                a.HasMember(clan1) && a.HasMember(clan2));
        }

        /// <summary>
        /// Get all alliances involving a specific clan
        /// </summary>
        public List<Alliance> GetAlliancesForClan(Clan clan)
        {
            if (clan == null)
                return new List<Alliance>();

            return _alliances.Where(a => a.IsActive && a.HasMember(clan)).ToList();
        }

        /// <summary>
        /// Get all active alliances
        /// </summary>
        public List<Alliance> GetAllActiveAlliances()
        {
            return _alliances.Where(a => a.IsActive).ToList();
        }

        /// <summary>
        /// Attempt to join an existing alliance
        /// </summary>
        public bool JoinAlliance(Alliance alliance, Clan newMember)
        {
            if (alliance == null || newMember == null || !alliance.IsActive)
                return false;

            if (alliance.HasMember(newMember))
                return false;

            // Check if other members approve (simplified - in full implementation would involve negotiations)
            var leaderClan = alliance.GetLeaderClan();
            if (leaderClan != null && leaderClan.Leader != null && newMember.Leader != null)
            {
                var relationship = leaderClan.Leader.GetRelation(newMember.Leader);
                var approvalChance = 0.3f + (relationship / 100f);

                if (MBRandom.RandomFloat < approvalChance)
                {
                    alliance.AddMember(newMember);

                    // Apply relationship bonuses with existing members
                    foreach (var existingMember in alliance.GetMemberClans())
                    {
                        if (existingMember != newMember && existingMember.Leader != null && newMember.Leader != null)
                        {
                            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(existingMember.Leader, newMember.Leader, 3);
                        }
                    }

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{newMember.Name} has joined the {alliance.Name}!",
                        Color.FromUint(0x00F16D26)));

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Leave an alliance
        /// </summary>
        public bool LeaveAlliance(Alliance alliance, Clan leavingClan)
        {
            if (alliance == null || leavingClan == null || !alliance.HasMember(leavingClan))
                return false;

            alliance.RemoveMember(leavingClan);

            // Apply relationship penalties with remaining members
            foreach (var remainingMember in alliance.GetMemberClans())
            {
                if (remainingMember.Leader != null && leavingClan.Leader != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(remainingMember.Leader, leavingClan.Leader, -10);
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"{leavingClan.Name} has left the {alliance.Name}",
                Colors.Red));

            return true;
        }

        /// <summary>
        /// Dissolve an alliance completely
        /// </summary>
        public bool DissolveAlliance(Alliance alliance, Clan requestingClan)
        {
            if (alliance == null || !alliance.IsActive)
                return false;

            // Only leader or majority vote can dissolve
            var leaderClan = alliance.GetLeaderClan();
            if (requestingClan != leaderClan)
            {
                // Would need majority vote logic here
                return false;
            }

            alliance.IsActive = false;
            alliance.AddHistoryEntry($"Alliance dissolved by {requestingClan.Name}");

            // Apply relationship penalties
            var members = alliance.GetMemberClans();
            foreach (var member1 in members)
            {
                foreach (var member2 in members)
                {
                    if (member1 != member2 && member1.Leader != null && member2.Leader != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(member1.Leader, member2.Leader, -5);
                    }
                }
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"The {alliance.Name} has been dissolved",
                Colors.Red));

            return true;
        }

        #endregion

        #region Private Helper Methods

        private void ProcessDailyAllianceUpdates()
        {
            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                // Natural trust decay over time (alliances require maintenance)
                alliance.TrustLevel = MathF.Max(0f, alliance.TrustLevel - 0.001f);

                // Secrecy can slowly improve if no major incidents
                if (MBRandom.RandomFloat < 0.1f) // 10% chance daily
                {
                    alliance.SecrecyLevel = MathF.Min(1f, alliance.SecrecyLevel + 0.01f);
                }

                // Check for alliance breakdown due to low trust
                if (alliance.TrustLevel < 0.1f && MBRandom.RandomFloat < 0.05f) // 5% chance if trust is very low
                {
                    alliance.IsActive = false;
                    alliance.AddHistoryEntry("Alliance dissolved due to lack of trust");

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"The {alliance.Name} has collapsed due to mistrust",
                        Colors.Red));
                }
            }
        }

        private float CalculateAllianceAcceptanceChance(Clan proposerClan, Clan targetClan)
        {
            float baseChance = 0.2f;

            // Relationship bonus
            if (proposerClan.Leader != null && targetClan.Leader != null)
            {
                var relationship = proposerClan.Leader.GetRelation(targetClan.Leader);
                baseChance += (relationship / 100f) * 0.4f;
            }

            // Power balance considerations
            var powerRatio = (float)proposerClan.TotalStrength / (proposerClan.TotalStrength + targetClan.TotalStrength);
            if (powerRatio > 0.3f && powerRatio < 0.7f) // Similar strength = more likely
            {
                baseChance += 0.2f;
            }

            // Kingdom status
            if (proposerClan.Kingdom == targetClan.Kingdom)
            {
                baseChance += 0.1f; // Same kingdom = easier
            }
            else if (proposerClan.Kingdom != null && targetClan.Kingdom != null &&
                     proposerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
            {
                baseChance -= 0.3f; // At war = much harder but not impossible
            }

            // Trait considerations
            if (targetClan.Leader != null)
            {
                var leader = targetClan.Leader;
                if (leader.GetTraitLevel(DefaultTraits.Honor) > 0)
                    baseChance += 0.1f;
                if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                    baseChance += 0.15f;
                if (leader.GetTraitLevel(DefaultTraits.Mercy) < 0)
                    baseChance -= 0.1f;
            }

            return MathF.Max(0f, MathF.Min(1f, baseChance));
        }

        #endregion
    }
}