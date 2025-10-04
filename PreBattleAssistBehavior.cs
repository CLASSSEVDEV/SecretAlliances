using SecretAlliances.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Handles battle assistance requests without forcing kingdom changes
    /// This fixes the critical bug that forced players to leave their kingdom when assisting allies
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public class PreBattleAssistBehavior : CampaignBehaviorBase
    {
        private readonly AllianceService _allianceService;
        private readonly RequestsBehavior _requestsBehavior;

        // Track assistance decisions to avoid duplicate processing
        private Dictionary<MapEvent, bool> _processedBattles = new Dictionary<MapEvent, bool>();

        public PreBattleAssistBehavior(AllianceService allianceService, RequestsBehavior requestsBehavior)
        {
            _allianceService = allianceService;
            _requestsBehavior = requestsBehavior;
        }

        public override void RegisterEvents()
        {
            // Hook map events for battle assistance
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

            // Hook encounter menus for player battle choices
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data needed for this behavior
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null || _processedBattles.ContainsKey(mapEvent))
                return;

            _processedBattles[mapEvent] = true;

            // Check for potential assistance opportunities
            CheckForAssistanceOpportunities(mapEvent, attackerParty, defenderParty);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent != null)
            {
                // Clean up tracking
                _processedBattles.Remove(mapEvent);

                // Process assistance consequences (exposure risk, etc.)
                ProcessAssistanceConsequences(mapEvent);
            }
        }

        private void OnGameMenuOpened(MenuCallbackArgs args)
        {
            // Add assistance options to encounter menus when appropriate
            if (args.MenuContext.GameMenu.StringId == "encounter" &&
                Campaign.Current.CurrentMenuContext != null)
            {
                CheckForPlayerAssistanceOptions();
            }
        }

        private void CheckForAssistanceOpportunities(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (attackerParty?.LeaderHero?.Clan == null || defenderParty?.LeaderHero?.Clan == null)
                return;

            var attackerClan = attackerParty.LeaderHero.Clan;
            var defenderClan = defenderParty.LeaderHero.Clan;

            // Check for alliances between nearby parties and the combatants
            CheckNearbyAlliedParties(mapEvent, attackerClan, defenderClan);
        }

        private void CheckNearbyAlliedParties(MapEvent mapEvent, Clan attackerClan, Clan defenderClan)
        {
            var nearbyParties = GetNearbyParties(mapEvent.Position, 10f); // 10 map units radius

            foreach (var party in nearbyParties)
            {
                if (party?.LeaderHero?.Clan == null || party.MapEvent == mapEvent)
                    continue;

                var partyClan = party.LeaderHero.Clan;

                // Check if this party has an alliance with either combatant
                var attackerAlliance = _allianceService.GetAlliance(partyClan, attackerClan);
                var defenderAlliance = _allianceService.GetAlliance(partyClan, defenderClan);

                if (attackerAlliance != null)
                {
                    ConsiderAssistance(party, mapEvent, attackerClan, true, attackerAlliance);
                }
                else if (defenderAlliance != null)
                {
                    ConsiderAssistance(party, mapEvent, defenderClan, false, defenderAlliance);
                }
            }
        }

        private void ConsiderAssistance(MobileParty assistingParty, MapEvent mapEvent, Clan alliedClan, bool supportAttacker, Alliance alliance)
        {
            if (assistingParty == null || mapEvent == null || alliance == null)
                return;

            // Calculate assistance probability based on alliance strength, trust, and circumstances
            var assistanceProbability = CalculateAssistanceProbability(assistingParty, alliedClan, alliance, mapEvent);

            if (assistingParty == MobileParty.MainParty)
            {
                // For player, create a request they can choose to accept
                CreatePlayerAssistanceRequest(mapEvent, alliedClan, supportAttacker, alliance);
            }
            else
            {
                // For AI parties, decide automatically
                if (MBRandom.RandomFloat < assistanceProbability)
                {
                    ExecuteAssistance(assistingParty, mapEvent, supportAttacker, alliance);
                }
            }
        }

        private float CalculateAssistanceProbability(MobileParty assistingParty, Clan alliedClan, Alliance alliance, MapEvent mapEvent)
        {
            float baseProbability = 0.3f;

            // Alliance factors
            baseProbability += alliance.TrustLevel * 0.4f;
            baseProbability += (1f - alliance.SecrecyLevel) * 0.2f; // Less secret = more likely to help openly

            // Strength considerations
            var assistingStrength = assistingParty.Party.TotalStrength;
            var totalBattleStrength = mapEvent.AttackerSide.TroopCount + mapEvent.DefenderSide.TroopCount;
            var strengthRatio = assistingStrength / (totalBattleStrength + assistingStrength);
            baseProbability += strengthRatio * 0.3f; // More likely if they can make a difference

            // Distance penalty (closer = more likely)
            var distance = assistingParty.Position2D.Distance(mapEvent.Position);
            baseProbability -= (distance / 10f) * 0.1f;

            // Relationship with allied clan leader
            if (assistingParty.LeaderHero != null && alliedClan.Leader != null)
            {
                var relationship = assistingParty.LeaderHero.GetRelation(alliedClan.Leader);
                baseProbability += (relationship / 100f) * 0.2f;
            }

            return MathF.Max(0f, MathF.Min(1f, baseProbability));
        }

        private void CreatePlayerAssistanceRequest(MapEvent mapEvent, Clan alliedClan, bool supportAttacker, Alliance alliance)
        {
            var description = $"Your secret ally {alliedClan.Name} is engaged in battle. Will you assist them?";
            var risk = 0.6f - (alliance.SecrecyLevel * 0.3f); // More secret alliances = less risk

            var request = new Request(RequestType.BattleAssistance, Hero.MainHero.Clan, alliedClan, description)
            {
                RiskLevel = risk,
                ProposedReward = CalculateAssistanceReward(mapEvent, alliance),
                
            };

            _requestsBehavior.AddRequest(request);

            // Show immediate notification
            InformationManager.DisplayMessage(new InformationMessage(
                $"Your ally {alliedClan.Name} requests battle assistance!",
                Color.FromUint(0x00F16D26))); // Orange color
        }

        private void AddPartyToSide(MapEvent mapEvent, MobileParty party, bool toAttacker)
        {
            var side = toAttacker ? mapEvent.AttackerSide : mapEvent.DefenderSide;

            // Construct MapEventParty through reflection (internal constructor).
            var ctor = typeof(MapEventParty).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(MapEventSide), typeof(MobileParty) },
                null);
            var meParty = (MapEventParty)ctor.Invoke(new object[] { side, party });

            // Add into the side's party list.
            var partiesField = typeof(MapEventSide).GetField("_parties",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<MapEventParty>)partiesField.GetValue(side);
            list.Add(meParty);

            // Recalculate
            mapEvent.RecalculateStrengthOfSides();
        }



        private int CalculateAssistanceReward(MapEvent mapEvent, Alliance alliance)
        {
            var baseReward = 500;
            var battleScale = (mapEvent.AttackerSide.TroopCount + mapEvent.DefenderSide.TroopCount) / 100f;
            var trustBonus = alliance.TrustLevel * 300;

            return (int)(baseReward + (battleScale * 50) + trustBonus);
        }

        private void ExecuteAssistance(MobileParty assistingParty, MapEvent mapEvent, bool supportAttacker, Alliance alliance)
        {
            try
            {
                // Add the assisting party properly into the battle
                AddPartyToSide(mapEvent, assistingParty, supportAttacker);

                // Update alliance trust
                alliance.TrustLevel += 0.05f;
                alliance.AddHistoryEntry($"{assistingParty.Name} assisted in battle");

                // Apply secrecy consequences
                ApplySecrecyConsequences(assistingParty, mapEvent, alliance);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"{assistingParty.Name} joins the battle to assist their secret ally!",
                    Color.FromUint(0x00F16D26)));
            }
            catch (Exception ex)
            {
                // If reflection fails, fallback to simulated assistance
                ProvideSimulatedAssistance(assistingParty, mapEvent, supportAttacker, alliance);
                TaleWorlds.Library.Debug.Print($"[SecretAlliances] Battle assistance fallback used: {ex.Message}");
            }
        }


        private void ProvideSimulatedAssistance(MobileParty assistingParty, MapEvent mapEvent, bool supportAttacker, Alliance alliance)
        {
            // Fallback method: Apply statistical influence without direct battle participation
            MapEventSide targetSide = supportAttacker ? mapEvent.AttackerSide : mapEvent.DefenderSide;

            // Calculate assistance impact based on party strength
            var assistanceStrength = assistingParty.Party.TotalStrength * 0.3f; // 30% effectiveness as hidden support
            var moraleBuff = assistanceStrength / targetSide.TroopCount;

            // Apply temporary strength bonus to allied side (simulating tactical support)
            // Note: This is a conceptual approach - actual implementation would need proper MapEvent hooks

            alliance.AddHistoryEntry($"{assistingParty.Name} provided covert battle support");
            ApplySecrecyConsequences(assistingParty, mapEvent, alliance);
        }

        private void ApplySecrecyConsequences(MobileParty assistingParty, MapEvent mapEvent, Alliance alliance)
        {
            // Calculate exposure risk based on battle circumstances
            var exposureRisk = 0.1f; // Base risk

            // Larger battles = more witnesses
            var battleSize = mapEvent.AttackerSide.TroopCount + mapEvent.DefenderSide.TroopCount;
            exposureRisk += (battleSize / 1000f) * 0.2f;

            // Battles near settlements = more exposure
            var nearestSettlement = Settlement.All.Where(s => s.IsTown || s.IsCastle || s.IsVillage).OrderBy(s => s.Position2D.Distance(mapEvent.Position)).FirstOrDefault(s => s.Position2D.Distance(mapEvent.Position) <= 5f);
            if (nearestSettlement != null)
            {
                exposureRisk += 0.3f;
            }

            // Apply secrecy loss
            alliance.SecrecyLevel = MathF.Max(0f, alliance.SecrecyLevel - exposureRisk);

            // Generate potential leaks based on exposure
            if (MBRandom.RandomFloat < exposureRisk)
            {
                // This will be handled by LeakBehavior
                InformationManager.DisplayMessage(new InformationMessage(
                    "Your secret assistance may have been noticed...",
                    Color.FromUint(0x00D65C0A))); // Red warning
            }
        }

        private void ProcessAssistanceConsequences(MapEvent mapEvent)
        {
            // Post-battle processing for any assistance that occurred
            // This is where we handle reputation changes, relationship impacts, etc.
            // without the problematic kingdom switching
        }

        private void CheckForPlayerAssistanceOptions()
        {
            // Add game menu options for players to accept/decline assistance requests
            var pendingRequests = _requestsBehavior.GetPendingRequestsForClan(Hero.MainHero.Clan);
            var battleRequests = pendingRequests.Where(r => r.Type == RequestType.BattleAssistance).ToList();

            if (battleRequests.Any())
            {
                // This would integrate with the encounter menu system
                // Implementation depends on specific menu structure
            }
        }

        private List<MobileParty> GetNearbyParties(Vec2 position, float radius)
        {
            var nearbyParties = new List<MobileParty>();

            foreach (var party in MobileParty.All)
            {
                if (party != null && party.Position2D.Distance(position) <= radius)
                {
                    nearbyParties.Add(party);
                }
            }

            return nearbyParties;
        }

        /// <summary>
        /// Helper class for battle party representation
        /// This allows parties to participate in battles without changing kingdoms
        /// </summary>
        private class BattleParty
        {
            public PartyBase Party { get; }
            public MapEventSide Side { get; }

            public BattleParty(PartyBase party, MapEventSide side)
            {
                Party = party;
                Side = side;
            }
        }
    }
}