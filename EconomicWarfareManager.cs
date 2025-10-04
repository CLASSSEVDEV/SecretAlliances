using System;
using System.Collections.Generic;
using System.Linq;
using SecretAlliances.Behaviors;
using SecretAlliances.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Economic warfare and trade manipulation system
    /// Includes: Sanctions, embargoes, trade monopolies, market manipulation
    /// Compatible with Bannerlord v1.2.9 API and .NET Framework 4.7.2
    /// </summary>
    public class EconomicWarfareManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private List<TradeEmbargo> _embargoes = new List<TradeEmbargo>();

        [SaveableField(2)]
        private List<TradeMonopoly> _monopolies = new List<TradeMonopoly>();

        [SaveableField(3)]
        private Dictionary<string, float> _marketInfluence = new Dictionary<string, float>();

        private readonly AllianceService _allianceService;
        private readonly DiplomacyManager _diplomacyManager;

        public EconomicWarfareManager(AllianceService allianceService, DiplomacyManager diplomacyManager)
        {
            _allianceService = allianceService;
            _diplomacyManager = diplomacyManager;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_TradeEmbargoes", ref _embargoes);
            dataStore.SyncData("SecretAlliances_TradeMonopolies", ref _monopolies);
            dataStore.SyncData("SecretAlliances_MarketInfluence", ref _marketInfluence);
        }

        private void OnDailyTick()
        {
            ProcessEmbargoes();
            ProcessMonopolies();
            UpdateMarketInfluence();
        }

        private void OnWeeklyTick()
        {
            GenerateAIEconomicActions();
        }

        #region Trade Embargoes

        /// <summary>
        /// Impose a trade embargo on a target clan
        /// </summary>
        public bool ImposeEmbargo(Clan initiator, Clan target, EmbargoType type, int duration)
        {
            if (initiator == null || target == null) return false;

            // Check if initiator has enough influence
            int influenceCost = GetEmbargoCost(type);
            if (_diplomacyManager.GetClanInfluence(initiator) < influenceCost)
            {
                if (initiator == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient influence to impose embargo",
                        Colors.Red));
                }
                return false;
            }

            // Create embargo
            var embargo = new TradeEmbargo
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                InitiatorClanId = initiator.Id,
                TargetClanId = target.Id,
                StartDate = CampaignTime.Now,
                ExpiryDate = CampaignTime.DaysFromNow(duration),
                IsActive = true
            };

            _embargoes.Add(embargo);

            // Pay influence cost
            _diplomacyManager.ModifyInfluence(initiator, -influenceCost, $"Trade embargo on {target.Name}");

            // Apply immediate effects
            ApplyEmbargoEffects(embargo, true);

            InformationManager.DisplayMessage(new InformationMessage(
                $"{initiator.Name} has imposed a {type} embargo on {target.Name}!",
                Colors.Yellow));

            return true;
        }

        /// <summary>
        /// Lift an embargo early
        /// </summary>
        public void LiftEmbargo(TradeEmbargo embargo)
        {
            if (embargo == null || !embargo.IsActive) return;

            embargo.IsActive = false;
            ApplyEmbargoEffects(embargo, false);

            var initiator = GetClanById(embargo.InitiatorClanId);
            var target = GetClanById(embargo.TargetClanId);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Trade embargo lifted: {initiator.Name} -> {target.Name}",
                Color.FromUint(0x0000FF00)));
        }

        private int GetEmbargoCost(EmbargoType type)
        {
            switch (type)
            {
                case EmbargoType.PartialEmbargo: return 20;
                case EmbargoType.FullEmbargo: return 40;
                case EmbargoType.FinancialSanctions: return 30;
                case EmbargoType.MilitaryEmbargo: return 35;
                default: return 20;
            }
        }

        private void ApplyEmbargoEffects(TradeEmbargo embargo, bool isApplying)
        {
            var target = GetClanById(embargo.TargetClanId);
            if (target == null) return;

            float multiplier = isApplying ? 0.7f : 1.0f / 0.7f; // 30% reduction

            switch (embargo.Type)
            {
                case EmbargoType.PartialEmbargo:
                    // Reduce trade income
                    ApplyTradeReduction(target, 0.15f * (isApplying ? 1 : -1));
                    break;

                case EmbargoType.FullEmbargo:
                    // Severe trade reduction
                    ApplyTradeReduction(target, 0.35f * (isApplying ? 1 : -1));
                    // Also reduce prosperity
                    ReduceProsperity(target, 0.1f * (isApplying ? 1 : -1));
                    break;

                case EmbargoType.FinancialSanctions:
                    // Reduce gold income and access
                    if (isApplying)
                    {
                        // Reduce gold reserves
                        target.Leader.ChangeHeroGold(-target.Gold / 10);
                    }
                    break;

                case EmbargoType.MilitaryEmbargo:
                    // Increase recruitment costs (abstract effect)
                    // In practice, this makes it harder to build armies
                    break;
            }
        }

        private void ApplyTradeReduction(Clan clan, float reduction)
        {
            foreach (var settlement in clan.Settlements)
            {
                if (settlement.IsTown)
                {
                    // Reduce prosperity as a proxy for trade income
                    settlement.Town.Prosperity = Math.Max(100, settlement.Town.Prosperity * (1 - reduction));
                }
            }
        }

        private void ReduceProsperity(Clan clan, float reduction)
        {
            foreach (var settlement in clan.Settlements)
            {
                if (settlement.IsTown || settlement.IsVillage)
                {
                    var prosperity = settlement.Town?.Prosperity ?? settlement.Village.Hearth;
                    var newValue = Math.Max(100, prosperity * (1 - reduction));
                    
                    if (settlement.IsTown)
                        settlement.Town.Prosperity = newValue;
                    else if (settlement.IsVillage)
                        settlement.Village.Hearth = newValue;
                }
            }
        }

        private void ProcessEmbargoes()
        {
            var expiredEmbargoes = _embargoes.Where(e => 
                e.IsActive && CampaignTime.Now > e.ExpiryDate).ToList();

            foreach (var embargo in expiredEmbargoes)
            {
                LiftEmbargo(embargo);
            }
        }

        #endregion

        #region Trade Monopolies

        /// <summary>
        /// Establish a trade monopoly in a specific good
        /// </summary>
        public bool EstablishMonopoly(Clan clan, ItemCategory category, int duration)
        {
            if (clan == null) return false;

            // Very expensive operation
            int influenceCost = 50;
            int goldCost = 50000;

            if (_diplomacyManager.GetClanInfluence(clan) < influenceCost || clan.Gold < goldCost)
            {
                if (clan == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Cannot establish monopoly. Need {influenceCost} influence and {goldCost} gold",
                        Colors.Red));
                }
                return false;
            }

            var monopoly = new TradeMonopoly
            {
                Id = Guid.NewGuid().ToString(),
                ClanId = clan.Id,
                Category = category,
                StartDate = CampaignTime.Now,
                ExpiryDate = CampaignTime.DaysFromNow(duration),
                IsActive = true,
                ProfitMultiplier = 1.5f
            };

            _monopolies.Add(monopoly);

            // Pay costs
            _diplomacyManager.ModifyInfluence(clan, -influenceCost, $"Trade monopoly: {category}");
            clan.Leader.ChangeHeroGold(-goldCost);

            InformationManager.DisplayMessage(new InformationMessage(
                $"{clan.Name} has established a trade monopoly on {category}!",
                Color.FromUint(0x00F16D26)));

            return true;
        }

        /// <summary>
        /// Break a trade monopoly (requires influence)
        /// </summary>
        public bool BreakMonopoly(Clan breaker, TradeMonopoly monopoly)
        {
            if (breaker == null || monopoly == null || !monopoly.IsActive) return false;

            // Costs influence to break someone else's monopoly
            int influenceCost = 30;
            if (_diplomacyManager.GetClanInfluence(breaker) < influenceCost)
            {
                if (breaker == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient influence to break this monopoly",
                        Colors.Red));
                }
                return false;
            }

            monopoly.IsActive = false;
            _diplomacyManager.ModifyInfluence(breaker, -influenceCost, "Breaking trade monopoly");

            var owner = GetClanById(monopoly.ClanId);

            // Relationship penalty
            if (breaker.Leader != null && owner?.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    breaker.Leader, owner.Leader, -15);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"{breaker.Name} has broken {owner.Name}'s monopoly on {monopoly.Category}!",
                Colors.Yellow));

            return true;
        }

        private void ProcessMonopolies()
        {
            var expiredMonopolies = _monopolies.Where(m => 
                m.IsActive && CampaignTime.Now > m.ExpiryDate).ToList();

            foreach (var monopoly in expiredMonopolies)
            {
                monopoly.IsActive = false;

                var owner = GetClanById(monopoly.ClanId);
                if (owner == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Your trade monopoly on {monopoly.Category} has expired",
                        Colors.Yellow));
                }
            }

            // Apply daily profits from monopolies
            foreach (var monopoly in _monopolies.Where(m => m.IsActive))
            {
                var owner = GetClanById(monopoly.ClanId);
                if (owner?.Leader != null)
                {
                    // Daily profit from monopoly
                    int dailyProfit = (int)(1000 * monopoly.ProfitMultiplier);
                    owner.Leader.ChangeHeroGold(dailyProfit);
                }
            }
        }

        #endregion

        #region Market Manipulation

        /// <summary>
        /// Manipulate market prices in owned settlements
        /// </summary>
        public bool ManipulateMarket(Clan clan, Settlement settlement, ItemCategory category, 
            float priceMultiplier, int duration)
        {
            if (clan == null || settlement == null) return false;

            // Must own or control the settlement
            if (settlement.OwnerClan != clan)
            {
                if (clan == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "You don't control this settlement",
                        Colors.Red));
                }
                return false;
            }

            // Cost influence
            int influenceCost = 15;
            if (_diplomacyManager.GetClanInfluence(clan) < influenceCost)
            {
                if (clan == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient influence for market manipulation",
                        Colors.Red));
                }
                return false;
            }

            _diplomacyManager.ModifyInfluence(clan, -influenceCost, "Market manipulation");

            // Store market influence
            var key = $"{settlement.Id}_{category}";
            _marketInfluence[key] = priceMultiplier;

            // Effect lasts for duration days (tracked in daily tick)

            if (clan == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Market manipulation active in {settlement.Name} for {category}",
                    Color.FromUint(0x00F16D26)));
            }

            return true;
        }

        private void UpdateMarketInfluence()
        {
            // Slowly decay market influence over time
            var keys = _marketInfluence.Keys.ToList();
            foreach (var key in keys)
            {
                var influence = _marketInfluence[key];
                influence -= 0.05f; // Decay by 5% per day

                if (Math.Abs(influence - 1.0f) < 0.01f)
                {
                    _marketInfluence.Remove(key); // Back to normal
                }
                else
                {
                    _marketInfluence[key] = influence;
                }
            }
        }

        /// <summary>
        /// Get current market price multiplier for a category in a settlement
        /// </summary>
        public float GetMarketMultiplier(Settlement settlement, ItemCategory category)
        {
            if (settlement == null) return 1.0f;

            var key = $"{settlement.Id}_{category}";
            return _marketInfluence.ContainsKey(key) ? _marketInfluence[key] : 1.0f;
        }

        #endregion

        #region Coordinated Economic Attacks

        /// <summary>
        /// Launch a coordinated economic attack using alliance members
        /// </summary>
        public bool LaunchCoordinatedAttack(Alliance alliance, Clan target, EconomicAttackType attackType)
        {
            if (alliance == null || target == null || !alliance.IsActive) return false;

            var members = alliance.GetMemberClans();
            if (members.Count < 2) return false;

            // Calculate combined influence cost (distributed among members)
            int totalInfluenceCost = GetCoordinatedAttackCost(attackType);
            int costPerMember = totalInfluenceCost / members.Count;

            // Check if all members can afford it
            foreach (var member in members)
            {
                if (_diplomacyManager.GetClanInfluence(member) < costPerMember)
                {
                    if (members.Contains(Hero.MainHero.Clan))
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"Alliance members lack sufficient influence for coordinated attack",
                            Colors.Red));
                    }
                    return false;
                }
            }

            // Execute attack
            foreach (var member in members)
            {
                _diplomacyManager.ModifyInfluence(member, -costPerMember, 
                    $"Coordinated attack on {target.Name}");

                // Each member contributes to the attack
                switch (attackType)
                {
                    case EconomicAttackType.MultipleEmbargoes:
                        ImposeEmbargo(member, target, EmbargoType.PartialEmbargo, 60);
                        break;

                    case EconomicAttackType.TradeBlockade:
                        // Severe combined effect
                        ApplyTradeReduction(target, 0.1f);
                        break;

                    case EconomicAttackType.MarketFlood:
                        // Flood market with goods to crash prices
                        ReduceProsperity(target, 0.05f);
                        break;

                    case EconomicAttackType.FinancialIsolation:
                        // Cut off financial access
                        if (target.Leader != null)
                        {
                            target.Leader.ChangeHeroGold(-target.Gold / 20);
                        }
                        break;
                }
            }

            // Massive reputation penalty for target
            _diplomacyManager.ModifyReputation(target, -0.1f, 
                $"Isolated by {alliance.Name}");

            InformationManager.DisplayMessage(new InformationMessage(
                $"The {alliance.Name} has launched a coordinated economic attack on {target.Name}!",
                Colors.Red));

            return true;
        }

        private int GetCoordinatedAttackCost(EconomicAttackType attackType)
        {
            switch (attackType)
            {
                case EconomicAttackType.MultipleEmbargoes: return 60;
                case EconomicAttackType.TradeBlockade: return 80;
                case EconomicAttackType.MarketFlood: return 50;
                case EconomicAttackType.FinancialIsolation: return 100;
                default: return 50;
            }
        }

        #endregion

        #region AI Economic Actions

        private void GenerateAIEconomicActions()
        {
            // AI occasionally uses economic warfare
            if (MBRandom.RandomFloat > 0.15f) return; // 15% chance per week

            var eligibleClans = Clan.All
                .Where(c => !c.IsEliminated && c != Hero.MainHero.Clan &&
                           _diplomacyManager.GetClanInfluence(c) > 30)
                .ToList();

            if (!eligibleClans.Any()) return;

            var attacker = eligibleClans.GetRandomElementInefficiently();

            // Find a target (enemy or rival)
            var potentialTargets = Clan.All
                .Where(c => !c.IsEliminated && c != attacker &&
                           c.Leader != null && attacker.Leader != null &&
                           attacker.Leader.GetRelation(c.Leader) < -10)
                .ToList();

            if (!potentialTargets.Any()) return;

            var target = potentialTargets.GetRandomElementInefficiently();

            // Decide on action
            var action = MBRandom.RandomInt(0, 3);
            switch (action)
            {
                case 0:
                    // Impose embargo
                    ImposeEmbargo(attacker, target, EmbargoType.PartialEmbargo, MBRandom.RandomInt(30, 90));
                    break;

                case 1:
                    // Try to establish monopoly
                    var categories = Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToList();
                    var category = categories.GetRandomElementInefficiently();
                    EstablishMonopoly(attacker, category, MBRandom.RandomInt(60, 180));
                    break;

                case 2:
                    // Market manipulation in own settlements
                    var settlement = attacker.Settlements.GetRandomElementInefficiently();
                    if (settlement != null)
                    {
                        var cat = Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().ToList()
                            .GetRandomElementInefficiently();
                        ManipulateMarket(attacker, settlement, cat, 1.3f, 30);
                    }
                    break;
            }
        }

        #endregion

        #region Helper Methods

        private Clan GetClanById(MBGUID clanId)
        {
            return Clan.All.FirstOrDefault(c => c.Id == clanId);
        }

        #endregion
    }

    #region Supporting Classes

    [SaveableClass(3)]
    public class TradeEmbargo
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public EmbargoType Type { get; set; }

        [SaveableProperty(3)]
        public MBGUID InitiatorClanId { get; set; }

        [SaveableProperty(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(5)]
        public CampaignTime StartDate { get; set; }

        [SaveableProperty(6)]
        public CampaignTime ExpiryDate { get; set; }

        [SaveableProperty(7)]
        public bool IsActive { get; set; }
    }

    public enum EmbargoType
    {
        PartialEmbargo,
        FullEmbargo,
        FinancialSanctions,
        MilitaryEmbargo
    }

    [SaveableClass(4)]
    public class TradeMonopoly
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public MBGUID ClanId { get; set; }

        [SaveableProperty(3)]
        public ItemCategory Category { get; set; }

        [SaveableProperty(4)]
        public CampaignTime StartDate { get; set; }

        [SaveableProperty(5)]
        public CampaignTime ExpiryDate { get; set; }

        [SaveableProperty(6)]
        public bool IsActive { get; set; }

        [SaveableProperty(7)]
        public float ProfitMultiplier { get; set; }
    }

    public enum EconomicAttackType
    {
        MultipleEmbargoes,
        TradeBlockade,
        MarketFlood,
        FinancialIsolation
    }

    #endregion
}
