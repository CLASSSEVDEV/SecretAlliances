using System;
using System.Collections.Generic;
using System.Linq;
using SecretAlliances.Behaviors;
using SecretAlliances.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Espionage and spy network system
    /// Includes: Spy networks, intelligence gathering, sabotage, counter-intelligence, double agents
    /// Compatible with Bannerlord v1.2.9 API and .NET Framework 4.7.2
    /// </summary>
    public class EspionageManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private List<SpyNetwork> _spyNetworks = new List<SpyNetwork>();

        [SaveableField(2)]
        private List<EspionageOperation> _activeOperations = new List<EspionageOperation>();

        [SaveableField(3)]
        private Dictionary<string, int> _counterIntelligenceLevel = new Dictionary<string, int>();

        [SaveableField(4)]
        private List<IntelligenceReport> _intelReports = new List<IntelligenceReport>();

        private readonly AllianceService _allianceService;
        private readonly DiplomacyManager _diplomacyManager;
        private readonly LeakBehavior _leakBehavior;

        public EspionageManager(AllianceService allianceService, DiplomacyManager diplomacyManager, LeakBehavior leakBehavior)
        {
            _allianceService = allianceService;
            _diplomacyManager = diplomacyManager;
            _leakBehavior = leakBehavior;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_SpyNetworks", ref _spyNetworks);
            dataStore.SyncData("SecretAlliances_EspionageOperations", ref _activeOperations);
            dataStore.SyncData("SecretAlliances_CounterIntel", ref _counterIntelligenceLevel);
            dataStore.SyncData("SecretAlliances_IntelReports", ref _intelReports);
        }

        private void OnDailyTick()
        {
            ProcessSpyNetworks();
            ProcessEspionageOperations();
            UpdateIntelligenceReports();
        }

        private void OnHourlyTick()
        {
            CheckForCounterIntelligenceEvents();
        }

        #region Spy Network Management

        /// <summary>
        /// Establish a spy network in a target clan's territory
        /// </summary>
        public bool EstablishSpyNetwork(Clan owner, Clan target, int tier)
        {
            if (owner == null || target == null) return false;

            // Check if network already exists
            var existing = _spyNetworks.FirstOrDefault(s => 
                s.OwnerClanId == owner.Id && s.TargetClanId == target.Id && s.IsActive);
            
            if (existing != null)
            {
                if (owner == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You already have a spy network in {target.Name}",
                        Colors.Yellow));
                }
                return false;
            }

            // Cost calculation
            int influenceCost = 15 * tier;
            int goldCost = 5000 * tier;

            if (_diplomacyManager.GetClanInfluence(owner) < influenceCost || owner.Gold < goldCost)
            {
                if (owner == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Insufficient resources. Need {influenceCost} influence and {goldCost} gold",
                        Colors.Red));
                }
                return false;
            }

            // Create spy network
            var network = new SpyNetwork
            {
                Id = Guid.NewGuid().ToString(),
                OwnerClanId = owner.Id,
                TargetClanId = target.Id,
                Tier = tier,
                EstablishedDate = CampaignTime.Now,
                IsActive = true,
                Effectiveness = 0.3f + (tier * 0.15f), // 30% base + 15% per tier
                ExposureRisk = 0.1f + (tier * 0.05f)   // Higher tier = higher risk
            };

            _spyNetworks.Add(network);

            // Pay costs
            _diplomacyManager.ModifyInfluence(owner, -influenceCost, $"Spy network in {target.Name}");
            owner.Leader.ChangeHeroGold(-goldCost);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Spy network established in {target.Name} (Tier {tier})",
                Color.FromUint(0x00F16D26)));

            return true;
        }

        /// <summary>
        /// Upgrade an existing spy network
        /// </summary>
        public bool UpgradeSpyNetwork(SpyNetwork network)
        {
            if (network == null || !network.IsActive || network.Tier >= 5) return false;

            var owner = GetClanById(network.OwnerClanId);
            if (owner == null) return false;

            int newTier = network.Tier + 1;
            int influenceCost = 10 * newTier;
            int goldCost = 3000 * newTier;

            if (_diplomacyManager.GetClanInfluence(owner) < influenceCost || owner.Gold < goldCost)
            {
                if (owner == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient resources to upgrade spy network",
                        Colors.Red));
                }
                return false;
            }

            network.Tier = newTier;
            network.Effectiveness = 0.3f + (newTier * 0.15f);
            network.ExposureRisk = 0.1f + (newTier * 0.05f);

            _diplomacyManager.ModifyInfluence(owner, -influenceCost, "Spy network upgrade");
            owner.Leader.ChangeHeroGold(-goldCost);

            if (owner == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Spy network upgraded to Tier {newTier}",
                    Color.FromUint(0x0000FF00)));
            }

            return true;
        }

        /// <summary>
        /// Dismantle a spy network
        /// </summary>
        public void DismantleSpyNetwork(SpyNetwork network)
        {
            if (network == null) return;

            network.IsActive = false;

            var owner = GetClanById(network.OwnerClanId);
            if (owner == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Spy network dismantled",
                    Colors.Yellow));
            }
        }

        private void ProcessSpyNetworks()
        {
            foreach (var network in _spyNetworks.Where(n => n.IsActive))
            {
                var target = GetClanById(network.TargetClanId);
                if (target == null || target.IsEliminated)
                {
                    network.IsActive = false;
                    continue;
                }

                // Generate intelligence
                if (MBRandom.RandomFloat < network.Effectiveness)
                {
                    GenerateIntelligenceReport(network);
                }

                // Check for exposure
                var counterIntelLevel = GetCounterIntelligenceLevel(target);
                var detectionChance = network.ExposureRisk * (1 + counterIntelLevel * 0.2f);

                if (MBRandom.RandomFloat < detectionChance)
                {
                    ExposeSpyNetwork(network);
                }

                // Daily maintenance cost
                var owner = GetClanById(network.OwnerClanId);
                if (owner != null && owner.Leader != null)
                {
                    int dailyCost = 100 * network.Tier;
                    if (owner.Gold >= dailyCost)
                    {
                        owner.Leader.ChangeHeroGold(-dailyCost);
                    }
                    else
                    {
                        // Cannot maintain - network degrades
                        network.Effectiveness *= 0.95f;
                        if (network.Effectiveness < 0.2f)
                        {
                            network.IsActive = false;
                        }
                    }
                }
            }
        }

        private void ExposeSpyNetwork(SpyNetwork network)
        {
            var owner = GetClanById(network.OwnerClanId);
            var target = GetClanById(network.TargetClanId);

            if (owner == null || target == null) return;

            // Severe consequences
            network.IsActive = false;

            // Reputation damage
            _diplomacyManager.ModifyReputation(owner, -0.15f, $"Caught spying on {target.Name}");

            // Relationship penalty
            if (owner.Leader != null && target.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    owner.Leader, target.Leader, -30);
            }

            // Exposure risk for alliances
            var alliances = _allianceService.GetAlliancesForClan(owner);
            foreach (var alliance in alliances)
            {
                _leakBehavior.AddExposureRisk(owner, 0.2f, "Exposed spy network");
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"{target.Name} has discovered {owner.Name}'s spy network!",
                Colors.Red));
        }

        #endregion

        #region Espionage Operations

        /// <summary>
        /// Launch an espionage operation
        /// </summary>
        public bool LaunchOperation(Clan operator, Clan target, OperationType operationType, SpyNetwork network = null)
        {
            if (operator == null || target == null) return false;

            // Verify spy network exists if required
            if (network == null)
            {
                network = _spyNetworks.FirstOrDefault(s => 
                    s.OwnerClanId == operator.Id && 
                    s.TargetClanId == target.Id && 
                    s.IsActive);
                
                if (network == null)
                {
                    if (operator == Hero.MainHero.Clan)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"No active spy network in {target.Name}",
                            Colors.Red));
                    }
                    return false;
                }
            }

            // Check if network tier is sufficient
            int requiredTier = GetRequiredTierForOperation(operationType);
            if (network.Tier < requiredTier)
            {
                if (operator == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Operation requires Tier {requiredTier} spy network",
                        Colors.Red));
                }
                return false;
            }

            // Cost calculation
            int influenceCost = GetOperationCost(operationType);
            if (_diplomacyManager.GetClanInfluence(operator) < influenceCost)
            {
                if (operator == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient influence for this operation",
                        Colors.Red));
                }
                return false;
            }

            // Create operation
            var operation = new EspionageOperation
            {
                Id = Guid.NewGuid().ToString(),
                Type = operationType,
                OperatorClanId = operator.Id,
                TargetClanId = target.Id,
                SpyNetworkId = network.Id,
                StartDate = CampaignTime.Now,
                CompletionDate = CampaignTime.DaysFromNow(GetOperationDuration(operationType)),
                SuccessChance = CalculateSuccessChance(network, operationType),
                IsActive = true
            };

            _activeOperations.Add(operation);

            // Pay cost
            _diplomacyManager.ModifyInfluence(operator, -influenceCost, $"Operation: {operationType}");

            if (operator == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Espionage operation launched: {operationType}",
                    Color.FromUint(0x00F16D26)));
            }

            return true;
        }

        private int GetRequiredTierForOperation(OperationType type)
        {
            switch (type)
            {
                case OperationType.GatherIntelligence: return 1;
                case OperationType.Sabotage: return 2;
                case OperationType.Assassination: return 4;
                case OperationType.StealTechnology: return 3;
                case OperationType.IncitRebellion: return 5;
                case OperationType.PlantDoubleAgent: return 3;
                default: return 1;
            }
        }

        private int GetOperationCost(OperationType type)
        {
            switch (type)
            {
                case OperationType.GatherIntelligence: return 10;
                case OperationType.Sabotage: return 25;
                case OperationType.Assassination: return 50;
                case OperationType.StealTechnology: return 30;
                case OperationType.IncitRebellion: return 40;
                case OperationType.PlantDoubleAgent: return 35;
                default: return 10;
            }
        }

        private int GetOperationDuration(OperationType type)
        {
            switch (type)
            {
                case OperationType.GatherIntelligence: return 3;
                case OperationType.Sabotage: return 5;
                case OperationType.Assassination: return 10;
                case OperationType.StealTechnology: return 7;
                case OperationType.IncitRebellion: return 15;
                case OperationType.PlantDoubleAgent: return 7;
                default: return 5;
            }
        }

        private float CalculateSuccessChance(SpyNetwork network, OperationType type)
        {
            float baseChance = network.Effectiveness;

            // Operation difficulty modifier
            switch (type)
            {
                case OperationType.GatherIntelligence:
                    baseChance *= 1.2f; // Easier
                    break;
                case OperationType.Sabotage:
                    baseChance *= 0.9f;
                    break;
                case OperationType.Assassination:
                    baseChance *= 0.5f; // Very difficult
                    break;
                case OperationType.StealTechnology:
                    baseChance *= 0.8f;
                    break;
                case OperationType.IncitRebellion:
                    baseChance *= 0.6f;
                    break;
                case OperationType.PlantDoubleAgent:
                    baseChance *= 0.7f;
                    break;
            }

            // Counter-intelligence reduces success
            var target = GetClanById(network.TargetClanId);
            if (target != null)
            {
                var counterIntel = GetCounterIntelligenceLevel(target);
                baseChance *= (1 - counterIntel * 0.1f);
            }

            return Math.Max(0.1f, Math.Min(0.95f, baseChance));
        }

        private void ProcessEspionageOperations()
        {
            var completedOps = _activeOperations.Where(o => 
                o.IsActive && CampaignTime.Now >= o.CompletionDate).ToList();

            foreach (var operation in completedOps)
            {
                operation.IsActive = false;

                // Determine success
                bool success = MBRandom.RandomFloat < operation.SuccessChance;

                if (success)
                {
                    ExecuteSuccessfulOperation(operation);
                }
                else
                {
                    ExecuteFailedOperation(operation);
                }
            }
        }

        private void ExecuteSuccessfulOperation(EspionageOperation operation)
        {
            var operator = GetClanById(operation.OperatorClanId);
            var target = GetClanById(operation.TargetClanId);

            if (operator == null || target == null) return;

            switch (operation.Type)
            {
                case OperationType.GatherIntelligence:
                    // Gain intelligence report
                    GenerateDetailedIntelReport(operation);
                    _diplomacyManager.ModifyInfluence(operator, 5, "Successful intelligence gathering");
                    break;

                case OperationType.Sabotage:
                    // Reduce target's prosperity
                    ReduceTargetProsperity(target, 0.15f);
                    _diplomacyManager.ModifyInfluence(operator, 10, "Successful sabotage");
                    break;

                case OperationType.Assassination:
                    // Attempt to kill random hero from target clan
                    AttemptAssassination(target);
                    _diplomacyManager.ModifyInfluence(operator, 15, "Successful assassination");
                    break;

                case OperationType.StealTechnology:
                    // Gain research points or bonuses (abstract)
                    operator.Leader.ChangeHeroGold(5000); // Compensation for tech
                    _diplomacyManager.ModifyInfluence(operator, 12, "Technology theft");
                    break;

                case OperationType.IncitRebellion:
                    // Reduce target's stability
                    if (target.Kingdom != null)
                    {
                        target.Influence = Math.Max(0, target.Influence - 20);
                    }
                    _diplomacyManager.ModifyInfluence(operator, 15, "Incited rebellion");
                    break;

                case OperationType.PlantDoubleAgent:
                    // Future operations have higher success rate
                    var network = _spyNetworks.FirstOrDefault(s => s.Id == operation.SpyNetworkId);
                    if (network != null)
                    {
                        network.Effectiveness += 0.2f;
                    }
                    _diplomacyManager.ModifyInfluence(operator, 10, "Double agent planted");
                    break;
            }

            if (operator == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Espionage operation successful: {operation.Type}",
                    Color.FromUint(0x0000FF00)));
            }
        }

        private void ExecuteFailedOperation(EspionageOperation operation)
        {
            var operator = GetClanById(operation.OperatorClanId);
            var target = GetClanById(operation.TargetClanId);

            if (operator == null || target == null) return;

            // Chance of exposure
            if (MBRandom.RandomFloat < 0.5f)
            {
                // Operation detected
                _diplomacyManager.ModifyReputation(operator, -0.1f, "Failed espionage operation");

                if (operator.Leader != null && target.Leader != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        operator.Leader, target.Leader, -15);
                }

                if (operator == Hero.MainHero.Clan || target == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{target.Name} has foiled {operator.Name}'s espionage operation!",
                        Colors.Red));
                }
            }
            else
            {
                // Failed but not detected
                if (operator == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Espionage operation failed: {operation.Type}",
                        Colors.Yellow));
                }
            }
        }

        #endregion

        #region Counter-Intelligence

        /// <summary>
        /// Invest in counter-intelligence capabilities
        /// </summary>
        public bool InvestInCounterIntelligence(Clan clan, int levels)
        {
            if (clan == null || levels <= 0) return false;

            int influenceCost = 20 * levels;
            int goldCost = 3000 * levels;

            if (_diplomacyManager.GetClanInfluence(clan) < influenceCost || clan.Gold < goldCost)
            {
                if (clan == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Insufficient resources for counter-intelligence investment",
                        Colors.Red));
                }
                return false;
            }

            var key = clan.Id.ToString();
            if (!_counterIntelligenceLevel.ContainsKey(key))
                _counterIntelligenceLevel[key] = 0;

            _counterIntelligenceLevel[key] = Math.Min(5, _counterIntelligenceLevel[key] + levels);

            _diplomacyManager.ModifyInfluence(clan, -influenceCost, "Counter-intelligence investment");
            clan.Leader.ChangeHeroGold(-goldCost);

            if (clan == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Counter-intelligence level increased to {_counterIntelligenceLevel[key]}",
                    Color.FromUint(0x0000FF00)));
            }

            return true;
        }

        public int GetCounterIntelligenceLevel(Clan clan)
        {
            if (clan == null) return 0;

            var key = clan.Id.ToString();
            return _counterIntelligenceLevel.ContainsKey(key) ? _counterIntelligenceLevel[key] : 0;
        }

        private void CheckForCounterIntelligenceEvents()
        {
            // Clans with high counter-intel can detect spy networks
            foreach (var clan in Clan.All.Where(c => !c.IsEliminated))
            {
                var counterIntel = GetCounterIntelligenceLevel(clan);
                if (counterIntel == 0) continue;

                var enemyNetworks = _spyNetworks.Where(s => 
                    s.IsActive && s.TargetClanId == clan.Id).ToList();

                foreach (var network in enemyNetworks)
                {
                    float detectionChance = counterIntel * 0.02f; // 2% per level per hour
                    if (MBRandom.RandomFloat < detectionChance)
                    {
                        ExposeSpyNetwork(network);
                    }
                }
            }
        }

        #endregion

        #region Intelligence Reports

        private void GenerateIntelligenceReport(SpyNetwork network)
        {
            var target = GetClanById(network.TargetClanId);
            if (target == null) return;

            var report = new IntelligenceReport
            {
                Id = Guid.NewGuid().ToString(),
                SpyNetworkId = network.Id,
                TargetClanId = target.Id,
                GeneratedDate = CampaignTime.Now,
                Quality = network.Tier,
                Content = GenerateIntelContent(target, network.Tier)
            };

            _intelReports.Add(report);

            // Keep only recent reports (last 20)
            if (_intelReports.Count > 20)
            {
                var oldest = _intelReports.OrderBy(r => r.GeneratedDate.ElapsedDaysUntilNow).First();
                _intelReports.Remove(oldest);
            }
        }

        private void GenerateDetailedIntelReport(EspionageOperation operation)
        {
            var network = _spyNetworks.FirstOrDefault(s => s.Id == operation.SpyNetworkId);
            if (network != null)
            {
                GenerateIntelligenceReport(network);
            }
        }

        private string GenerateIntelContent(Clan target, int quality)
        {
            var content = new List<string>();

            content.Add($"Military Strength: {target.TotalStrength}");
            content.Add($"Gold Reserves: ~{target.Gold}");
            content.Add($"Settlements: {target.Settlements.Count}");

            if (quality >= 2)
            {
                content.Add($"Active Parties: {target.WarPartyComponents?.Count() ?? 0}");
                if (target.Kingdom != null)
                {
                    content.Add($"Kingdom: {target.Kingdom.Name}");
                    content.Add($"At war with: {target.Kingdom.ActiveWars?.Count() ?? 0} factions");
                }
            }

            if (quality >= 3)
            {
                var alliances = _allianceService.GetAlliancesForClan(target);
                content.Add($"Secret Alliances: {alliances.Count}");
            }

            if (quality >= 4)
            {
                // Reveal specific plans (abstract)
                content.Add("Strategic intentions: Preparing for expansion");
            }

            return string.Join("\n", content);
        }

        private void UpdateIntelligenceReports()
        {
            // Clean up old reports
            var oldReports = _intelReports.Where(r => 
                r.GeneratedDate.ElapsedDaysUntilNow > 30).ToList();

            foreach (var report in oldReports)
            {
                _intelReports.Remove(report);
            }
        }

        /// <summary>
        /// Get all intelligence reports for a specific clan
        /// </summary>
        public List<IntelligenceReport> GetIntelligenceReports(Clan target)
        {
            if (target == null) return new List<IntelligenceReport>();

            return _intelReports.Where(r => r.TargetClanId == target.Id)
                .OrderByDescending(r => r.GeneratedDate.ElapsedDaysUntilNow)
                .ToList();
        }

        #endregion

        #region Helper Methods

        private Clan GetClanById(MBGUID clanId)
        {
            return Clan.All.FirstOrDefault(c => c.Id == clanId);
        }

        private void ReduceTargetProsperity(Clan target, float reduction)
        {
            foreach (var settlement in target.Settlements)
            {
                if (settlement.IsTown)
                {
                    settlement.Town.Prosperity = Math.Max(100, settlement.Town.Prosperity * (1 - reduction));
                }
            }
        }

        private void AttemptAssassination(Clan target)
        {
            // Get a random non-leader hero from the clan
            var heroes = target.Heroes.Where(h => 
                h.IsAlive && !h.IsChild && h != target.Leader).ToList();

            if (heroes.Any())
            {
                var victim = heroes.GetRandomElementInefficiently();
                
                // Don't actually kill (too severe), just wound
                if (victim.HitPoints > 10)
                {
                    victim.HitPoints = 10;
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"{victim.Name} of {target.Name} has been wounded in a mysterious attack!",
                    Colors.Red));
            }
        }

        #endregion
    }

    #region Supporting Classes

    [SaveableClass(5)]
    public class SpyNetwork
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public MBGUID OwnerClanId { get; set; }

        [SaveableProperty(3)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(4)]
        public int Tier { get; set; } // 1-5

        [SaveableProperty(5)]
        public CampaignTime EstablishedDate { get; set; }

        [SaveableProperty(6)]
        public bool IsActive { get; set; }

        [SaveableProperty(7)]
        public float Effectiveness { get; set; } // 0-1

        [SaveableProperty(8)]
        public float ExposureRisk { get; set; } // 0-1
    }

    [SaveableClass(6)]
    public class EspionageOperation
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public OperationType Type { get; set; }

        [SaveableProperty(3)]
        public MBGUID OperatorClanId { get; set; }

        [SaveableProperty(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(5)]
        public string SpyNetworkId { get; set; }

        [SaveableProperty(6)]
        public CampaignTime StartDate { get; set; }

        [SaveableProperty(7)]
        public CampaignTime CompletionDate { get; set; }

        [SaveableProperty(8)]
        public float SuccessChance { get; set; }

        [SaveableProperty(9)]
        public bool IsActive { get; set; }
    }

    public enum OperationType
    {
        GatherIntelligence,
        Sabotage,
        Assassination,
        StealTechnology,
        IncitRebellion,
        PlantDoubleAgent
    }

    [SaveableClass(7)]
    public class IntelligenceReport
    {
        [SaveableProperty(1)]
        public string Id { get; set; }

        [SaveableProperty(2)]
        public string SpyNetworkId { get; set; }

        [SaveableProperty(3)]
        public MBGUID TargetClanId { get; set; }

        [SaveableProperty(4)]
        public CampaignTime GeneratedDate { get; set; }

        [SaveableProperty(5)]
        public int Quality { get; set; } // 1-5 based on spy network tier

        [SaveableProperty(6)]
        public string Content { get; set; }
    }

    #endregion
}
