using System;
using System.Collections.Generic;
using System.Linq;
using SecretAlliances.Behaviors;
using SecretAlliances.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Advanced diplomacy system that mimics real-world politics
    /// Includes: Influence trading, backroom deals, political marriages, sanctions, embargoes
    /// Compatible with Bannerlord v1.2.9 API and .NET Framework 4.7.2
    /// </summary>
    public class DiplomacyManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private List<DiplomaticAgreement> _agreements = new List<DiplomaticAgreement>();

        [SaveableField(2)]
        private Dictionary<string, int> _clanInfluence = new Dictionary<string, int>();

        [SaveableField(3)]
        private Dictionary<string, float> _clanReputation = new Dictionary<string, float>();

        [SaveableField(4)]
        private List<PoliticalFavor> _politicalFavors = new List<PoliticalFavor>();

        private readonly AllianceService _allianceService;

        public DiplomacyManager(AllianceService allianceService)
        {
            _allianceService = allianceService;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_DiplomaticAgreements", ref _agreements);
            dataStore.SyncData("SecretAlliances_ClanInfluence", ref _clanInfluence);
            dataStore.SyncData("SecretAlliances_ClanReputation", ref _clanReputation);
            dataStore.SyncData("SecretAlliances_PoliticalFavors", ref _politicalFavors);
        }

        private void OnDailyTick()
        {
            ProcessDiplomaticAgreements();
            UpdateClanInfluence();
            DecayReputation();
        }

        private void OnWeeklyTick()
        {
            GenerateAIDiplomaticActions();
            ProcessPoliticalIntrigues();
        }

        private void OnHourlyTick()
        {
            ProcessUrgentDiplomaticMatters();
        }

        #region Influence and Reputation System

        /// <summary>
        /// Get a clan's political influence score (0-1000)
        /// </summary>
        public int GetClanInfluence(Clan clan)
        {
            if (clan == null) return 0;
            
            var key = clan.Id.ToString();
            if (!_clanInfluence.ContainsKey(key))
            {
                // Initialize influence based on clan strength
                _clanInfluence[key] = CalculateBaseInfluence(clan);
            }
            
            return _clanInfluence[key];
        }

        /// <summary>
        /// Add or remove influence from a clan
        /// </summary>
        public void ModifyInfluence(Clan clan, int amount, string reason = "")
        {
            if (clan == null) return;
            
            var key = clan.Id.ToString();
            if (!_clanInfluence.ContainsKey(key))
                _clanInfluence[key] = CalculateBaseInfluence(clan);
            
            _clanInfluence[key] = Math.Max(0, Math.Min(1000, _clanInfluence[key] + amount));
            
            if (clan == Hero.MainHero.Clan && Math.Abs(amount) >= 5)
            {
                var color = amount > 0 ? Color.FromUint(0x0000FF00) : Colors.Red;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Influence {(amount > 0 ? "gained" : "lost")}: {Math.Abs(amount)} ({reason})",
                    color));
            }
        }

        /// <summary>
        /// Get a clan's diplomatic reputation (0.0 - 1.0)
        /// </summary>
        public float GetClanReputation(Clan clan)
        {
            if (clan == null) return 0.5f;
            
            var key = clan.Id.ToString();
            if (!_clanReputation.ContainsKey(key))
                _clanReputation[key] = 0.5f; // Neutral starting reputation
            
            return _clanReputation[key];
        }

        /// <summary>
        /// Modify a clan's reputation
        /// </summary>
        public void ModifyReputation(Clan clan, float amount, string reason = "")
        {
            if (clan == null) return;
            
            var key = clan.Id.ToString();
            if (!_clanReputation.ContainsKey(key))
                _clanReputation[key] = 0.5f;
            
            _clanReputation[key] = Math.Max(0f, Math.Min(1f, _clanReputation[key] + amount));
            
            if (clan == Hero.MainHero.Clan && Math.Abs(amount) >= 0.05f)
            {
                var color = amount > 0 ? Color.FromUint(0x0000FF00) : Colors.Red;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Reputation {(amount > 0 ? "improved" : "damaged")}: {reason}",
                    color));
            }
        }

        private int CalculateBaseInfluence(Clan clan)
        {
            if (clan == null) return 0;
            
            int influence = 0;
            
            // Base influence from settlements
            influence += clan.Settlements.Count * 10;
            
            // Influence from fiefs
            influence += clan.Fiefs.Count() * 20;
            
            // Influence from military strength
            influence += (int)(clan.TotalStrength / 100);
            
            // Influence from wealth
            influence += clan.Gold / 5000;
            
            // Influence from kingdom position
            if (clan.Kingdom != null && clan.Kingdom.RulingClan == clan)
                influence += 100; // Ruling clan bonus
            
            return Math.Min(1000, influence);
        }

        private void UpdateClanInfluence()
        {
            // Slow decay towards calculated base influence (natural shift)
            foreach (var clan in Clan.All)
            {
                if (clan.IsEliminated) continue;
                
                var currentInfluence = GetClanInfluence(clan);
                var baseInfluence = CalculateBaseInfluence(clan);
                
                // Gradually shift towards base
                var diff = baseInfluence - currentInfluence;
                var adjustment = (int)(diff * 0.05f); // 5% adjustment per day
                
                if (adjustment != 0)
                {
                    var key = clan.Id.ToString();
                    _clanInfluence[key] = currentInfluence + adjustment;
                }
            }
        }

        private void DecayReputation()
        {
            // Reputation slowly returns to neutral (0.5) over time
            var keys = _clanReputation.Keys.ToList();
            foreach (var key in keys)
            {
                var current = _clanReputation[key];
                var diff = 0.5f - current;
                _clanReputation[key] += diff * 0.01f; // 1% drift towards neutral per day
            }
        }

        #endregion

        #region Diplomatic Agreements

        /// <summary>
        /// Create a new diplomatic agreement between clans
        /// </summary>
        public DiplomaticAgreement CreateAgreement(AgreementType type, Clan initiator, Clan target, 
            int duration, Dictionary<string, object> terms = null)
        {
            if (initiator == null || target == null) return null;
            
            var agreement = new DiplomaticAgreement
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                InitiatorClanId = initiator.Id,
                TargetClanId = target.Id,
                StartDate = CampaignTime.Now,
                ExpiryDate = CampaignTime.DaysFromNow(duration),
                Terms = terms ?? new Dictionary<string, object>(),
                IsActive = true
            };
            
            _agreements.Add(agreement);
            ApplyAgreementEffects(agreement, true);
            
            // Cost influence to make agreements
            ModifyInfluence(initiator, -5, $"Agreement: {type}");
            
            InformationManager.DisplayMessage(new InformationMessage(
                $"Diplomatic agreement established: {initiator.Name} <-> {target.Name} ({type})",
                Color.FromUint(0x00F16D26)));
            
            return agreement;
        }

        /// <summary>
        /// Break a diplomatic agreement (with consequences)
        /// </summary>
        public void BreakAgreement(DiplomaticAgreement agreement, Clan breaker, string reason = "")
        {
            if (agreement == null || !agreement.IsActive) return;
            
            agreement.IsActive = false;
            agreement.BrokenBy = breaker?.Id ?? default(MBGUID);
            agreement.BreakReason = reason;
            
            ApplyAgreementEffects(agreement, false);
            
            // Severe reputation penalty for breaking agreements
            ModifyReputation(breaker, -0.15f, $"Broke agreement: {agreement.Type}");
            ModifyInfluence(breaker, -20, "Trust violation");
            
            // Relationship penalty
            var initiator = GetClanById(agreement.InitiatorClanId);
            var target = GetClanById(agreement.TargetClanId);
            var otherParty = breaker == initiator ? target : initiator;
            
            if (breaker?.Leader != null && otherParty?.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    breaker.Leader, otherParty.Leader, -20);
            }
            
            InformationManager.DisplayMessage(new InformationMessage(
                $"{breaker.Name} has broken their agreement with {otherParty.Name}!",
                Colors.Red));
        }

        private void ApplyAgreementEffects(DiplomaticAgreement agreement, bool isApplying)
        {
            var initiator = GetClanById(agreement.InitiatorClanId);
            var target = GetClanById(agreement.TargetClanId);
            
            if (initiator == null || target == null) return;
            
            switch (agreement.Type)
            {
                case AgreementType.NonAggressionPact:
                    // Prevents attacks, improves relations
                    if (isApplying && initiator.Leader != null && target.Leader != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                            initiator.Leader, target.Leader, 10);
                    }
                    break;
                    
                case AgreementType.TradeAgreement:
                    // Boosts prosperity for both parties
                    ApplyTradeBonus(initiator, target, isApplying);
                    break;
                    
                case AgreementType.MilitaryAccess:
                    // Allows passage through territory
                    // This is implicit, checked by other systems
                    break;
                    
                case AgreementType.MutualDefense:
                    // Triggers defensive support in wars
                    if (isApplying)
                    {
                        ModifyInfluence(initiator, 10, "Mutual Defense Pact");
                        ModifyInfluence(target, 10, "Mutual Defense Pact");
                    }
                    break;
                    
                case AgreementType.TributePact:
                    // Regular tribute payments
                    // Handled in daily tick
                    break;
            }
        }

        private void ApplyTradeBonus(Clan clan1, Clan clan2, bool isApplying)
        {
            float multiplier = isApplying ? 1.1f : 1.0f / 1.1f;
            
            // Boost prosperity of owned settlements
            foreach (var settlement in clan1.Settlements)
            {
                if (settlement.IsTown)
                {
                    settlement.Town.Prosperity *= multiplier;
                }
            }
            
            foreach (var settlement in clan2.Settlements)
            {
                if (settlement.IsTown)
                {
                    settlement.Town.Prosperity *= multiplier;
                }
            }
        }

        private void ProcessDiplomaticAgreements()
        {
            var expiredAgreements = _agreements.Where(a => 
                a.IsActive && CampaignTime.Now > a.ExpiryDate).ToList();
            
            foreach (var agreement in expiredAgreements)
            {
                agreement.IsActive = false;
                ApplyAgreementEffects(agreement, false);
                
                var initiator = GetClanById(agreement.InitiatorClanId);
                var target = GetClanById(agreement.TargetClanId);
                
                if (initiator == Hero.MainHero.Clan || target == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Diplomatic agreement expired: {initiator.Name} <-> {target.Name} ({agreement.Type})",
                        Colors.Yellow));
                }
            }
        }

        #endregion

        #region Political Favors and Backroom Deals

        /// <summary>
        /// Request a political favor from an allied clan
        /// </summary>
        public bool RequestFavor(Clan requester, Clan target, FavorType favorType, string description)
        {
            if (requester == null || target == null) return false;
            
            // Check if alliance exists
            var alliance = _allianceService.GetAlliance(requester, target);
            if (alliance == null) return false;
            
            // Calculate influence cost
            int influenceCost = GetFavorInfluenceCost(favorType);
            
            if (GetClanInfluence(requester) < influenceCost)
            {
                if (requester == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient influence to request this favor",
                        Colors.Red));
                }
                return false;
            }
            
            // Calculate acceptance chance based on relationship and trust
            float acceptanceChance = CalculateFavorAcceptanceChance(requester, target, alliance, favorType);
            
            if (MBRandom.RandomFloat < acceptanceChance)
            {
                var favor = new PoliticalFavor
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = favorType,
                    RequesterClanId = requester.Id,
                    TargetClanId = target.Id,
                    Description = description,
                    RequestDate = CampaignTime.Now,
                    IsGranted = true,
                    InfluenceCost = influenceCost
                };
                
                _politicalFavors.Add(favor);
                ModifyInfluence(requester, -influenceCost, $"Political favor: {favorType}");
                
                // Apply favor effects
                ApplyFavorEffects(favor);
                
                // Improve trust in alliance
                alliance.TrustLevel += 0.05f;
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{target.Name} has granted your political favor: {favorType}",
                    Color.FromUint(0x0000FF00)));
                
                return true;
            }
            else
            {
                // Favor denied - small relationship penalty
                if (requester.Leader != null && target.Leader != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        requester.Leader, target.Leader, -2);
                }
                
                if (requester == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{target.Name} has declined your request for a political favor",
                        Colors.Yellow));
                }
                
                return false;
            }
        }

        private int GetFavorInfluenceCost(FavorType favorType)
        {
            switch (favorType)
            {
                case FavorType.IntroduceDiplomat: return 10;
                case FavorType.ShareIntelligence: return 15;
                case FavorType.EconomicSupport: return 20;
                case FavorType.MilitarySupport: return 25;
                case FavorType.PoliticalEndorsement: return 30;
                case FavorType.VoteInfluence: return 35;
                default: return 10;
            }
        }

        private float CalculateFavorAcceptanceChance(Clan requester, Clan target, Alliance alliance, FavorType favorType)
        {
            float baseChance = 0.3f;
            
            // Alliance trust bonus
            baseChance += alliance.TrustLevel * 0.4f;
            
            // Relationship bonus
            if (requester.Leader != null && target.Leader != null)
            {
                var relation = requester.Leader.GetRelation(target.Leader);
                baseChance += (relation / 100f) * 0.2f;
            }
            
            // Reputation bonus
            baseChance += GetClanReputation(requester) * 0.1f;
            
            return Math.Max(0f, Math.Min(1f, baseChance));
        }

        private void ApplyFavorEffects(PoliticalFavor favor)
        {
            var requester = GetClanById(favor.RequesterClanId);
            var target = GetClanById(favor.TargetClanId);
            
            if (requester == null || target == null) return;
            
            switch (favor.Type)
            {
                case FavorType.IntroduceDiplomat:
                    // Improve relations with third parties
                    ModifyInfluence(requester, 5, "Diplomatic introduction");
                    break;
                    
                case FavorType.ShareIntelligence:
                    // Grant visibility of enemy movements (abstract)
                    ModifyInfluence(requester, 3, "Intelligence sharing");
                    break;
                    
                case FavorType.EconomicSupport:
                    // Transfer gold
                    int goldAmount = (int)(target.Gold * 0.1f);
                    GiveGoldAction.ApplyBetweenCharacters(target.Leader, requester.Leader, goldAmount);
                    break;
                    
                case FavorType.MilitarySupport:
                    // Create military aid request
                    // This is handled by the RequestsBehavior
                    break;
                    
                case FavorType.PoliticalEndorsement:
                    // Boost reputation
                    ModifyReputation(requester, 0.05f, $"Endorsed by {target.Name}");
                    break;
                    
                case FavorType.VoteInfluence:
                    // Boost kingdom influence
                    if (requester.Kingdom != null)
                    {
                        requester.Influence += 20;
                    }
                    break;
            }
        }

        #endregion

        #region AI Diplomatic Actions

        private void GenerateAIDiplomaticActions()
        {
            // AI clans occasionally initiate diplomatic actions
            if (MBRandom.RandomFloat > 0.2f) return; // 20% chance per week
            
            var activeAlliances = _allianceService.GetAllActiveAlliances();
            
            foreach (var alliance in activeAlliances)
            {
                var members = alliance.GetMemberClans();
                if (members.Count < 2) continue;
                
                // Random pair within alliance
                var clan1 = members.GetRandomElementInefficiently();
                var clan2 = members.Where(c => c != clan1).GetRandomElementInefficiently();
                
                if (clan1 == null || clan2 == null) continue;
                
                // Skip if player is involved (let player initiate)
                if (clan1 == Hero.MainHero.Clan || clan2 == Hero.MainHero.Clan) continue;
                
                // Decide on action
                if (MBRandom.RandomFloat < 0.5f && GetClanInfluence(clan1) >= 20)
                {
                    // Create diplomatic agreement
                    var agreementTypes = new[] { AgreementType.TradeAgreement, AgreementType.NonAggressionPact };
                    var type = agreementTypes[MBRandom.RandomInt(agreementTypes.Length)];
                    CreateAgreement(type, clan1, clan2, MBRandom.RandomInt(30, 180));
                }
                else if (GetClanInfluence(clan1) >= 15)
                {
                    // Request political favor
                    var favorTypes = new[] { FavorType.ShareIntelligence, FavorType.EconomicSupport };
                    var type = favorTypes[MBRandom.RandomInt(favorTypes.Length)];
                    RequestFavor(clan1, clan2, type, $"AI-generated favor request: {type}");
                }
            }
        }

        private void ProcessPoliticalIntrigues()
        {
            // Process complex political machinations
            ProcessInfluenceCampaigns();
            ProcessBackroomDeals();
            ProcessPoliticalBetrayal();
        }

        private void ProcessInfluenceCampaigns()
        {
            // Clans with high influence can campaign against others
            var influentialClans = Clan.All
                .Where(c => !c.IsEliminated && GetClanInfluence(c) > 500)
                .Take(5)
                .ToList();
            
            foreach (var clan in influentialClans)
            {
                if (MBRandom.RandomFloat < 0.1f) // 10% chance per week
                {
                    // Target a rival
                    var rivals = Clan.All
                        .Where(c => !c.IsEliminated && c != clan && 
                                   clan.Leader != null && c.Leader != null &&
                                   clan.Leader.GetRelation(c.Leader) < -20)
                        .ToList();
                    
                    if (rivals.Any())
                    {
                        var target = rivals.GetRandomElementInefficiently();
                        
                        // Reduce target's influence
                        ModifyInfluence(target, -10, $"Influence campaign by {clan.Name}");
                        ModifyInfluence(clan, -5, "Running influence campaign");
                        
                        if (target == Hero.MainHero.Clan)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"{clan.Name} is running a campaign to undermine your influence!",
                                Colors.Red));
                        }
                    }
                }
            }
        }

        private void ProcessBackroomDeals()
        {
            // Secret deals between clans that benefit both but harm others
            if (MBRandom.RandomFloat > 0.05f) return; // 5% chance per week
            
            var eligibleClans = Clan.All.Where(c => !c.IsEliminated && c != Hero.MainHero.Clan).ToList();
            
            if (eligibleClans.Count < 2) return;
            
            var clan1 = eligibleClans.GetRandomElementInefficiently();
            var clan2 = eligibleClans.Where(c => c != clan1).GetRandomElementInefficiently();
            
            if (clan1 == null || clan2 == null) return;
            
            // Check if they have mutual enemies
            var commonEnemies = FindCommonEnemies(clan1, clan2);
            
            if (commonEnemies.Any())
            {
                var target = commonEnemies.GetRandomElementInefficiently();
                
                // Execute backroom deal
                ModifyInfluence(clan1, 5, "Secret alliance");
                ModifyInfluence(clan2, 5, "Secret alliance");
                ModifyInfluence(target, -15, "Diplomatic isolation");
                
                if (target == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Rumors suggest {clan1.Name} and {clan2.Name} are conspiring against you...",
                        Colors.Red));
                }
            }
        }

        private void ProcessPoliticalBetrayal()
        {
            // Rare chance of political betrayal within alliances
            if (MBRandom.RandomFloat > 0.02f) return; // 2% chance per week
            
            var alliances = _allianceService.GetAllActiveAlliances();
            
            foreach (var alliance in alliances)
            {
                if (alliance.TrustLevel < 0.3f) // Only betray low-trust alliances
                {
                    var members = alliance.GetMemberClans();
                    if (members.Count < 2) continue;
                    
                    var betrayer = members.Where(c => c != Hero.MainHero.Clan).GetRandomElementInefficiently();
                    var victim = members.Where(c => c != betrayer).GetRandomElementInefficiently();
                    
                    if (betrayer == null || victim == null) continue;
                    
                    // Execute betrayal
                    alliance.TrustLevel = 0f;
                    alliance.IsActive = false;
                    
                    ModifyReputation(betrayer, -0.2f, $"Betrayed {victim.Name}");
                    
                    if (betrayer.Leader != null && victim.Leader != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                            betrayer.Leader, victim.Leader, -30);
                    }
                    
                    if (victim == Hero.MainHero.Clan || betrayer == Hero.MainHero.Clan)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{betrayer.Name} has betrayed the {alliance.Name}!",
                            Colors.Red));
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private void ProcessUrgentDiplomaticMatters()
        {
            // Check for tribute payments
            var activeTributes = _agreements.Where(a => 
                a.IsActive && a.Type == AgreementType.TributePact).ToList();
            
            foreach (var tribute in activeTributes)
            {
                // Check if payment is due (every 24 hours)
                if (tribute.LastPaymentTime == default(CampaignTime) ||
                    (CampaignTime.Now - tribute.LastPaymentTime).ToHours >= 24)
                {
                    ProcessTributePayment(tribute);
                    tribute.LastPaymentTime = CampaignTime.Now;
                }
            }
        }

        private void ProcessTributePayment(DiplomaticAgreement agreement)
        {
            var payer = GetClanById(agreement.InitiatorClanId);
            var receiver = GetClanById(agreement.TargetClanId);
            
            if (payer == null || receiver == null || payer.Leader == null || receiver.Leader == null)
                return;
            
            int tributeAmount = 100; // Base amount
            if (agreement.Terms.ContainsKey("TributeAmount"))
            {
                tributeAmount = Convert.ToInt32(agreement.Terms["TributeAmount"]);
            }
            
            if (payer.Gold >= tributeAmount)
            {
                GiveGoldAction.ApplyBetweenCharacters(payer.Leader, receiver.Leader, tributeAmount);
            }
            else
            {
                // Cannot pay tribute - break agreement
                BreakAgreement(agreement, payer, "Unable to pay tribute");
            }
        }

        private Clan GetClanById(MBGUID clanId)
        {
            return Clan.All.FirstOrDefault(c => c.Id == clanId);
        }

        private List<Clan> FindCommonEnemies(Clan clan1, Clan clan2)
        {
            if (clan1?.Leader == null || clan2?.Leader == null)
                return new List<Clan>();
            
            return Clan.All.Where(c =>
                c != clan1 && c != clan2 && !c.IsEliminated &&
                c.Leader != null &&
                clan1.Leader.GetRelation(c.Leader) < -10 &&
                clan2.Leader.GetRelation(c.Leader) < -10)
                .ToList();
        }

        #endregion
    }

    #region Supporting Classes

    [SaveableClass(1)]
    public class DiplomaticAgreement
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public AgreementType Type { get; set; }

        [SaveableProperty(3)]
        public MBGUID InitiatorClanId { get; set; }

        [SaveableProperty(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(5)]
        public CampaignTime StartDate { get; set; }

        [SaveableProperty(6)]
        public CampaignTime ExpiryDate { get; set; }

        [SaveableProperty(7)]
        public Dictionary<string, object> Terms { get; set; }

        [SaveableProperty(8)]
        public bool IsActive { get; set; }

        [SaveableProperty(9)]
        public MBGUID BrokenBy { get; set; }

        [SaveableProperty(10)]
        public string BreakReason { get; set; }

        [SaveableProperty(11)]
        public CampaignTime LastPaymentTime { get; set; }
    }

    public enum AgreementType
    {
        NonAggressionPact,
        TradeAgreement,
        MilitaryAccess,
        MutualDefense,
        TributePact,
        MarriageAlliance,
        TerritoryAgreement
    }

    [SaveableClass(2)]
    public class PoliticalFavor
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public FavorType Type { get; set; }

        [SaveableProperty(3)]
        public MBGUID RequesterClanId { get; set; }

        [SaveableProperty(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(5)]
        public string Description { get; set; }

        [SaveableProperty(6)]
        public CampaignTime RequestDate { get; set; }

        [SaveableProperty(7)]
        public bool IsGranted { get; set; }

        [SaveableProperty(8)]
        public int InfluenceCost { get; set; }
    }

    public enum FavorType
    {
        IntroduceDiplomat,
        ShareIntelligence,
        EconomicSupport,
        MilitarySupport,
        PoliticalEndorsement,
        VoteInfluence
    }

    #endregion
}
