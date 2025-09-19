using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using SecretAlliances.Models;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// AI decision-making behavior using utility-based system
    /// Provides rational decision making for NPCs regarding alliances, betrayals, assistance, etc.
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public class AiDecisionBehavior : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private Dictionary<string, CampaignTime> _decisionCooldowns = new Dictionary<string, CampaignTime>();
        
        [SaveableField(2)]
        private Dictionary<string, List<string>> _recentDecisions = new Dictionary<string, List<string>>();

        private readonly AllianceService _allianceService;
        private readonly RequestsBehavior _requestsBehavior;
        private readonly LeakBehavior _leakBehavior;

        // Configuration
        private const float DECISION_THRESHOLD = 60f; // Minimum utility score to take action
        private const int DECISION_MEMORY_DAYS = 30; // Remember decisions for 30 days
        private const int MAX_DECISIONS_PER_CLAN_PER_DAY = 2; // Prevent decision spam

        public AiDecisionBehavior(AllianceService allianceService, RequestsBehavior requestsBehavior, LeakBehavior leakBehavior)
        {
            _allianceService = allianceService;
            _requestsBehavior = requestsBehavior;
            _leakBehavior = leakBehavior;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_AI_DecisionCooldowns", ref _decisionCooldowns);
            dataStore.SyncData("SecretAlliances_AI_RecentDecisions", ref _recentDecisions);
        }

        private void OnDailyTick()
        {
            ProcessAIDecisions();
            CleanupExpiredCooldowns();
        }

        private void OnWeeklyTick()
        {
            CleanupRecentDecisions();
        }

        #region Main AI Decision Processing

        private void ProcessAIDecisions()
        {
            // Process decisions for a subset of clans each day to distribute computational load
            var eligibleClans = GetEligibleClansForDecisions();
            var clansToProcess = eligibleClans.Take(10).ToList(); // Process max 10 clans per day

            foreach (var clan in clansToProcess)
            {
                if (HasReachedDailyDecisionLimit(clan))
                    continue;

                ProcessClanDecisions(clan);
            }
        }

        private void ProcessClanDecisions(Clan clan)
        {
            // Process different types of decisions in order of importance
            
            // 1. Alliance-related decisions (highest priority)
            ProcessAllianceDecisions(clan);
            
            // 2. Assistance request decisions
            ProcessAssistanceDecisions(clan);
            
            // 3. Investment decisions (secrecy, counter-intelligence)
            ProcessInvestmentDecisions(clan);
            
            // 4. Opportunistic decisions (betrayal, etc.) - lowest priority
            if (MBRandom.RandomFloat < 0.1f) // 10% chance to consider opportunistic actions
            {
                ProcessOpportunisticDecisions(clan);
            }
        }

        #endregion

        #region Alliance Decisions

        private void ProcessAllianceDecisions(Clan clan)
        {
            // Check for alliance creation opportunities
            if (ShouldConsiderAllianceCreation(clan))
            {
                ConsiderAllianceCreation(clan);
            }

            // Check for joining existing alliances
            if (ShouldConsiderJoiningAlliance(clan))
            {
                ConsiderJoiningExistingAlliance(clan);
            }

            // Evaluate existing alliances for potential departure
            EvaluateExistingAlliances(clan);
        }

        private void ConsiderAllianceCreation(Clan clan)
        {
            var potentialTargets = GetPotentialAllianceTargets(clan);
            
            foreach (var target in potentialTargets.Take(3)) // Consider top 3 candidates
            {
                var utility = UtilityModel.CalculateAllianceCreationUtility(clan, target);
                
                if (utility >= DECISION_THRESHOLD)
                {
                    // Attempt to create alliance
                    if (_allianceService.ProposeAlliance(clan, target))
                    {
                        RecordDecision(clan, $"proposed_alliance_{target.Id}");
                        Debug.Print($"[SecretAlliances AI] {clan.Name} proposed alliance to {target.Name} (Utility: {utility:F1})");
                        break; // Only propose one alliance per decision cycle
                    }
                }
            }
        }

        private void ConsiderJoiningExistingAlliance(Clan clan)
        {
            var availableAlliances = GetJoinableAlliances(clan);
            
            foreach (var alliance in availableAlliances)
            {
                var utility = UtilityModel.CalculateAllianceJoinUtility(clan, alliance);
                
                if (utility >= DECISION_THRESHOLD)
                {
                    if (_allianceService.JoinAlliance(alliance, clan))
                    {
                        RecordDecision(clan, $"joined_alliance_{alliance.Id}");
                        Debug.Print($"[SecretAlliances AI] {clan.Name} joined alliance {alliance.Name} (Utility: {utility:F1})");
                        break;
                    }
                }
            }
        }

        private void EvaluateExistingAlliances(Clan clan)
        {
            var existingAlliances = _allianceService.GetAlliancesForClan(clan);
            
            foreach (var alliance in existingAlliances)
            {
                // Calculate dissatisfaction with current alliance
                var dissatisfaction = CalculateAllianceDissatisfaction(clan, alliance);
                
                if (dissatisfaction >= DECISION_THRESHOLD && alliance.GetLeaderClan() != clan)
                {
                    // Consider leaving the alliance
                    if (_allianceService.LeaveAlliance(alliance, clan))
                    {
                        RecordDecision(clan, $"left_alliance_{alliance.Id}");
                        Debug.Print($"[SecretAlliances AI] {clan.Name} left alliance {alliance.Name} (Dissatisfaction: {dissatisfaction:F1})");
                        break;
                    }
                }
            }
        }

        #endregion

        #region Assistance Decisions

        private void ProcessAssistanceDecisions(Clan clan)
        {
            // Process pending requests to this clan
            var pendingRequests = _requestsBehavior.GetPendingRequestsForClan(clan);
            
            foreach (var request in pendingRequests)
            {
                var alliance = _allianceService.GetAlliance(clan, request.GetRequesterClan());
                var utility = UtilityModel.CalculateAssistanceUtility(clan, request, alliance);
                
                if (utility >= DECISION_THRESHOLD)
                {
                    _requestsBehavior.AcceptRequest(request);
                    RecordDecision(clan, $"accepted_request_{request.Id}");
                    Debug.Print($"[SecretAlliances AI] {clan.Name} accepted assistance request from {request.GetRequesterClan()?.Name} (Utility: {utility:F1})");
                }
                else if (utility < 20f) // Low utility = decline
                {
                    _requestsBehavior.DeclineRequest(request, "Current circumstances do not permit assistance");
                    RecordDecision(clan, $"declined_request_{request.Id}");
                    Debug.Print($"[SecretAlliances AI] {clan.Name} declined assistance request from {request.GetRequesterClan()?.Name} (Utility: {utility:F1})");
                }
                // Medium utility (20-60) = wait and see
            }

            // Consider making new assistance requests
            if (ShouldMakeAssistanceRequest(clan))
            {
                ConsiderMakingAssistanceRequest(clan);
            }
        }

        private void ConsiderMakingAssistanceRequest(Clan clan)
        {
            var alliances = _allianceService.GetAlliancesForClan(clan);
            var needs = AssessAssistanceNeeds(clan);
            
            foreach (var need in needs)
            {
                var bestAlly = FindBestAllyForRequest(clan, alliances, need.Type);
                if (bestAlly != null)
                {
                    var request = _requestsBehavior.CreateAssistanceRequest(clan, bestAlly, need.Type, need.Description, need.Reward);
                    if (request != null)
                    {
                        RecordDecision(clan, $"requested_{need.Type}_{bestAlly.Id}");
                        Debug.Print($"[SecretAlliances AI] {clan.Name} requested {need.Type} from {bestAlly.Name}");
                        break; // Only make one request per cycle
                    }
                }
            }
        }

        #endregion

        #region Investment Decisions

        private void ProcessInvestmentDecisions(Clan clan)
        {
            var alliances = _allianceService.GetAlliancesForClan(clan);
            
            foreach (var alliance in alliances)
            {
                // Consider secrecy investment
                if (alliance.SecrecyLevel < 0.7f && clan.Gold > 5000)
                {
                    var investmentCost = 2000;
                    var utility = UtilityModel.CalculateSecrecyInvestmentUtility(clan, alliance, investmentCost);
                    
                    if (utility >= DECISION_THRESHOLD)
                    {
                        // Simulate secrecy investment
                        alliance.SecrecyLevel = MathF.Min(1f, alliance.SecrecyLevel + 0.2f);
                        clan.Leader.ChangeHeroGold(-investmentCost);
                        alliance.AddHistoryEntry($"{clan.Name} invested in alliance secrecy");
                        
                        RecordDecision(clan, $"secrecy_investment_{alliance.Id}");
                        Debug.Print($"[SecretAlliances AI] {clan.Name} invested in secrecy for {alliance.Name} (Utility: {utility:F1})");
                        break;
                    }
                }
            }
        }

        #endregion

        #region Opportunistic Decisions

        private void ProcessOpportunisticDecisions(Clan clan)
        {
            // Consider betrayal opportunities (rare and heavily penalized by traits)
            if (ShouldConsiderBetrayal(clan))
            {
                ConsiderBetrayalOpportunities(clan);
            }

            // Consider leak opportunities (for dishonorable clans)
            if (ShouldConsiderLeaking(clan))
            {
                ConsiderLeakingInformation(clan);
            }
        }

        private void ConsiderBetrayalOpportunities(Clan clan)
        {
            var alliances = _allianceService.GetAlliancesForClan(clan);
            
            foreach (var alliance in alliances)
            {
                var otherMembers = alliance.GetMemberClans().Where(c => c != clan).ToList();
                
                foreach (var target in otherMembers)
                {
                    // Find potential beneficiaries of betrayal
                    var potentialBeneficiaries = GetPotentialBetrayalBeneficiaries(clan, target);
                    
                    foreach (var beneficiary in potentialBeneficiaries)
                    {
                        var utility = UtilityModel.CalculateBetrayalUtility(clan, target, beneficiary, alliance);
                        
                        // Very high threshold for betrayal due to severe consequences
                        if (utility >= 80f)
                        {
                            ExecuteBetrayal(clan, target, beneficiary, alliance);
                            return; // Only one betrayal per decision cycle
                        }
                    }
                }
            }
        }

        private void ConsiderLeakingInformation(Clan clan)
        {
            var alliances = _allianceService.GetAlliancesForClan(clan);
            
            // Only dishonorable clans with financial pressure consider leaking
            if (clan.Leader?.GetTraitLevel(DefaultTraits.Honor) >= 0 || clan.Gold > 5000)
                return;

            foreach (var alliance in alliances)
            {
                if (alliance.SecrecyLevel > 0.3f && MBRandom.RandomFloat < 0.05f) // 5% chance
                {
                    _leakBehavior?.ForceLeakForTesting(alliance);
                    RecordDecision(clan, $"leaked_alliance_{alliance.Id}");
                    Debug.Print($"[SecretAlliances AI] {clan.Name} leaked information about {alliance.Name}");
                    break;
                }
            }
        }

        #endregion

        #region Helper Methods

        private List<Clan> GetEligibleClansForDecisions()
        {
            return Clan.All.Where(c => 
                c != null && 
                !c.IsEliminated && 
                c != Hero.MainHero.Clan && // Don't make decisions for player
                c.Leader != null &&
                !HasRecentDecisionCooldown(c)
            ).ToList();
        }

        private bool HasRecentDecisionCooldown(Clan clan)
        {
            var cooldownKey = $"daily_{clan.Id}";
            return _decisionCooldowns.ContainsKey(cooldownKey) && 
                   CampaignTime.Now < _decisionCooldowns[cooldownKey];
        }

        private bool HasReachedDailyDecisionLimit(Clan clan)
        {
            var clanKey = clan.Id.ToString();
            if (!_recentDecisions.ContainsKey(clanKey))
                return false;

            var todayDecisions = _recentDecisions[clanKey]
                .Count(d => d.Contains(CampaignTime.Now.GetYear.ToString()));

            return todayDecisions >= MAX_DECISIONS_PER_CLAN_PER_DAY;
        }

        private void RecordDecision(Clan clan, string decision)
        {
            var clanKey = clan.Id.ToString();
            if (!_recentDecisions.ContainsKey(clanKey))
                _recentDecisions[clanKey] = new List<string>();

            var timestampedDecision = $"{CampaignTime.Now.GetYear}_{CampaignTime.Now.GetDayOfYear}_{decision}";
            _recentDecisions[clanKey].Add(timestampedDecision);

            // Set cooldown to prevent rapid consecutive decisions
            var cooldownKey = $"decision_{clan.Id}_{decision}";
            _decisionCooldowns[cooldownKey] = CampaignTime.HoursFromNow(6); // 6 hour cooldown
        }

        private bool ShouldConsiderAllianceCreation(Clan clan)
        {
            // Don't consider if already in multiple alliances
            var currentAlliances = _allianceService.GetAlliancesForClan(clan).Count;
            if (currentAlliances >= 2) return false;

            // Don't consider if recently made alliance decisions
            var clanKey = clan.Id.ToString();
            if (_recentDecisions.ContainsKey(clanKey))
            {
                var recentAllianceDecisions = _recentDecisions[clanKey]
                    .Count(d => d.Contains("alliance") && IsRecentDecision(d));
                if (recentAllianceDecisions > 0) return false;
            }

            return true;
        }

        private bool ShouldConsiderJoiningAlliance(Clan clan)
        {
            return _allianceService.GetAlliancesForClan(clan).Count < 3; // Max 3 alliances
        }

        private bool ShouldMakeAssistanceRequest(Clan clan)
        {
            // Only if clan has pressing needs and existing alliances
            return _allianceService.GetAlliancesForClan(clan).Any() && 
                   (clan.Gold < 5000 || clan.TotalStrength < 300);
        }

        private bool ShouldConsiderBetrayal(Clan clan)
        {
            // Only dishonorable clans with very specific circumstances
            return clan.Leader?.GetTraitLevel(DefaultTraits.Honor) < 0 &&
                   MBRandom.RandomFloat < 0.02f; // 2% chance for dishonorable
        }

        private bool ShouldConsiderLeaking(Clan clan)
        {
            // Only for financially desperate, dishonorable clans
            return clan.Leader?.GetTraitLevel(DefaultTraits.Honor) < 0 &&
                   clan.Gold < 2000;
        }

        private List<Clan> GetPotentialAllianceTargets(Clan clan)
        {
            return Clan.All.Where(c => 
                c != null && 
                c != clan && 
                !c.IsEliminated &&
                _allianceService.CanProposeAlliance(clan, c)
            ).OrderByDescending(c => UtilityModel.CalculateAllianceCreationUtility(clan, c))
            .Take(10)
            .ToList();
        }

        private List<Alliance> GetJoinableAlliances(Clan clan)
        {
            return _allianceService.GetAllActiveAlliances()
                .Where(a => !a.HasMember(clan) && a.GetMemberClans().Count < 5) // Max 5 members
                .ToList();
        }

        private float CalculateAllianceDissatisfaction(Clan clan, Alliance alliance)
        {
            float dissatisfaction = 0f;

            // Low trust = high dissatisfaction
            dissatisfaction += (1f - alliance.TrustLevel) * 40f;

            // High exposure = high dissatisfaction
            dissatisfaction += (1f - alliance.SecrecyLevel) * 30f;

            // Recent negative events
            var recentNegativeEvents = alliance.HistoryLog.Count(entry => 
                entry.Contains("failed") || entry.Contains("leaked") || entry.Contains("betrayed"));
            dissatisfaction += recentNegativeEvents * 10f;

            return dissatisfaction;
        }

        private List<AssistanceNeed> AssessAssistanceNeeds(Clan clan)
        {
            var needs = new List<AssistanceNeed>();

            if (clan.Gold < 3000)
            {
                needs.Add(new AssistanceNeed
                {
                    Type = RequestType.Tribute,
                    Description = "Financial assistance needed for clan maintenance",
                    Reward = 0 // Tribute requests don't offer rewards
                });
            }

            if (clan.TotalStrength < 200)
            {
                needs.Add(new AssistanceNeed
                {
                    Type = RequestType.BattleAssistance,
                    Description = "Military assistance needed for upcoming conflicts",
                    Reward = 1000
                });
            }

            return needs;
        }

        private Clan FindBestAllyForRequest(Clan requester, List<Alliance> alliances, RequestType requestType)
        {
            var potentialAllies = alliances.SelectMany(a => a.GetMemberClans())
                .Where(c => c != requester)
                .Distinct()
                .ToList();

            return potentialAllies.OrderByDescending(ally => 
                UtilityModel.CalculateAssistanceUtility(ally, 
                    new Request(requestType, requester, ally, "AI generated request"))).FirstOrDefault();
        }

        private List<Clan> GetPotentialBetrayalBeneficiaries(Clan betrayer, Clan target)
        {
            // Find clans that might benefit from the betrayal
            return Clan.All.Where(c => 
                c != betrayer && 
                c != target && 
                !c.IsEliminated &&
                c.Kingdom != target.Kingdom // Different kingdom = potential beneficiary
            ).Take(3).ToList();
        }

        private void ExecuteBetrayal(Clan betrayer, Clan target, Clan beneficiary, Alliance alliance)
        {
            // Remove betrayer from alliance
            _allianceService.LeaveAlliance(alliance, betrayer);

            // Apply severe relationship penalties
            if (betrayer.Leader != null)
            {
                foreach (var member in alliance.GetMemberClans())
                {
                    if (member != betrayer && member.Leader != null)
                    {
                        // Severe penalty for betrayal
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                            betrayer.Leader, member.Leader, -50);
                    }
                }

                // Potential relationship bonus with beneficiary
                if (beneficiary?.Leader != null)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        betrayer.Leader, beneficiary.Leader, 15);
                }
            }

            RecordDecision(betrayer, $"betrayed_{target.Id}");
            Debug.Print($"[SecretAlliances AI] {betrayer.Name} betrayed {target.Name} in favor of {beneficiary?.Name}");
        }

        private bool IsRecentDecision(string decision)
        {
            // Check if decision was made within last 7 days
            var parts = decision.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int dayOfYear))
            {
                var decisionTime = CampaignTime.Years(year) + CampaignTime.Days(dayOfYear);
                return (CampaignTime.Now - decisionTime).ToDays <= 7;
            }
            return false;
        }

        private void CleanupExpiredCooldowns()
        {
            var expiredKeys = _decisionCooldowns.Where(kvp => CampaignTime.Now > kvp.Value)
                .Select(kvp => kvp.Key).ToList();
            
            foreach (var key in expiredKeys)
            {
                _decisionCooldowns.Remove(key);
            }
        }

        private void CleanupRecentDecisions()
        {
            foreach (var clanKey in _recentDecisions.Keys.ToList())
            {
                _recentDecisions[clanKey] = _recentDecisions[clanKey]
                    .Where(IsRecentDecision).ToList();

                if (_recentDecisions[clanKey].Count == 0)
                {
                    _recentDecisions.Remove(clanKey);
                }
            }
        }

        #endregion

        #region Helper Classes

        private class AssistanceNeed
        {
            public RequestType Type { get; set; }
            public string Description { get; set; }
            public int Reward { get; set; }
        }

        #endregion
    }
}