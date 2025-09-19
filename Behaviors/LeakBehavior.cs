using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using SecretAlliances.Models;

namespace SecretAlliances.Behaviors
{
    /// <summary>
    /// Handles leak/exposure consequences for secret alliances
    /// Manages secrecy, rumors, and political consequences when alliances are discovered
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    [SaveableClass(10)]
    public class LeakBehavior : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private List<LeakRecord> _recentLeaks = new List<LeakRecord>();
        
        [SaveableField(2)]
        private Dictionary<string, float> _clanExposureScores = new Dictionary<string, float>();
        
        private readonly AllianceService _allianceService;
        
        // Configuration constants
        private const float BASE_LEAK_CHANCE = 0.02f; // 2% base chance per day
        private const float EXPOSURE_DECAY_RATE = 0.01f; // Daily exposure decay
        private const int MAX_LEAK_HISTORY = 20; // Keep last 20 leaks for performance

        public LeakBehavior(AllianceService allianceService)
        {
            _allianceService = allianceService;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_LeakRecords", ref _recentLeaks);
            dataStore.SyncData("SecretAlliances_ExposureScores", ref _clanExposureScores);
        }

        private void OnDailyTick()
        {
            ProcessDailyLeakChecks();
            DecayExposureScores();
        }

        private void OnWeeklyTick()
        {
            CleanupOldLeaks();
        }

        #region Public Methods

        /// <summary>
        /// Get exposure score for a specific clan
        /// </summary>
        public float GetClanExposureScore(Clan clan)
        {
            if (clan == null) return 0f;
            
            _clanExposureScores.TryGetValue(clan.Id.ToString(), out float score);
            return score;
        }

        /// <summary>
        /// Add exposure risk for a clan (called by other behaviors)
        /// </summary>
        public void AddExposureRisk(Clan clan, float risk, string source = "Unknown")
        {
            if (clan == null) return;
            
            var clanKey = clan.Id.ToString();
            if (!_clanExposureScores.ContainsKey(clanKey))
                _clanExposureScores[clanKey] = 0f;
                
            _clanExposureScores[clanKey] += risk;
            _clanExposureScores[clanKey] = MathF.Min(1f, _clanExposureScores[clanKey]); // Cap at 100%
            
            Debug.Print($"[SecretAlliances] Exposure risk added to {clan.Name}: +{risk:F2} from {source} (Total: {_clanExposureScores[clanKey]:F2})");
        }

        /// <summary>
        /// Force a leak for testing purposes
        /// </summary>
        public void ForceLeakForTesting(Alliance alliance)
        {
            if (alliance == null) return;
            
            var leakClan = alliance.GetMemberClans().GetRandomElementInefficiently();
            if (leakClan != null)
            {
                GenerateLeak(alliance, leakClan, 1.0f, "Testing");
            }
        }

        /// <summary>
        /// Get recent leak information for a clan
        /// </summary>
        public List<LeakRecord> GetRecentLeaksForClan(Clan clan)
        {
            if (clan == null) return new List<LeakRecord>();
            
            return _recentLeaks.Where(l => l.InvolvesClan(clan)).ToList();
        }

        /// <summary>
        /// Check if a clan has any rumors about them
        /// </summary>
        public bool HasActiveRumors(Clan clan)
        {
            return GetClanExposureScore(clan) > 0.1f || GetRecentLeaksForClan(clan).Any(l => l.IsRecent());
        }

        #endregion

        #region Private Methods

        private void ProcessDailyLeakChecks()
        {
            var alliances = _allianceService?.GetAllActiveAlliances();
            if (alliances == null) return;

            foreach (var alliance in alliances)
            {
                if (!alliance.IsSecret) continue; // Only secret alliances can leak
                
                CheckForAllianceLeak(alliance);
            }
        }

        private void CheckForAllianceLeak(Alliance alliance)
        {
            var leakChance = CalculateLeakChance(alliance);
            
            if (MBRandom.RandomFloat < leakChance)
            {
                var leakSource = DetermineLeakSource(alliance);
                var severity = CalculateLeakSeverity(alliance);
                
                GenerateLeak(alliance, leakSource, severity, "Daily check");
            }
        }

        private float CalculateLeakChance(Alliance alliance)
        {
            float chance = BASE_LEAK_CHANCE;
            
            // Secrecy level affects leak chance (lower secrecy = higher chance)
            chance += (1f - alliance.SecrecyLevel) * 0.05f;
            
            // Number of members increases leak chance
            var memberCount = alliance.GetMemberClans().Count;
            chance += (memberCount - 2) * 0.01f; // Each member beyond 2 adds 1% chance
            
            // Trust level affects leak chance (lower trust = higher chance)
            chance += (1f - alliance.TrustLevel) * 0.03f;
            
            // Age of alliance (older = more established = less likely to leak)
            var ageInYears = (CampaignTime.Now - alliance.StartTime).ToYears;
            chance -= ageInYears * 0.002f; // -0.2% per year
            
            // Recent activity increases leak chance
            if (alliance.HistoryLog.Count > 0)
            {
                var recentEntries = alliance.HistoryLog.Where(entry => 
                    entry.Contains(CampaignTime.Now.ToYears.ToString("F1"))).Count();
                chance += recentEntries * 0.005f;
            }
            
            return MathF.Max(0f, MathF.Min(0.5f, chance)); // Cap between 0% and 50%
        }

        private Clan DetermineLeakSource(Alliance alliance)
        {
            var members = alliance.GetMemberClans();
            
            // Weight by clan exposure scores and traits
            return members.OrderByDescending(c => GetLeakProbabilityForClan(c)).FirstOrDefault();
        }

        private float GetLeakProbabilityForClan(Clan clan)
        {
            if (clan?.Leader == null) return 0f;
            
            float probability = GetClanExposureScore(clan);
            
            // Trait considerations
            var leader = clan.Leader;
            if (leader.GetTraitLevel(DefaultTraits.Honor) < 0)
                probability += 0.2f; // Dishonorable leaders more likely to leak
            if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                probability -= 0.1f; // Calculating leaders less likely to leak accidentally
            if (leader.GetTraitLevel(DefaultTraits.Generosity) > 0)
                probability += 0.05f; // Generous leaders might share information
                
            // Financial pressure increases leak chance
            if (clan.Gold < 5000)
                probability += 0.1f;
                
            return probability;
        }

        private float CalculateLeakSeverity(Alliance alliance)
        {
            // Base severity
            float severity = 0.3f;
            
            // Alliance strength affects how damaging the leak is
            severity += alliance.TrustLevel * 0.4f; // Stronger alliances = bigger scandal
            
            // Number of members affects severity
            severity += (alliance.GetMemberClans().Count - 2) * 0.1f;
            
            // Political context
            var memberKingdoms = alliance.GetMemberClans()
                .Where(c => c.Kingdom != null)
                .Select(c => c.Kingdom)
                .Distinct()
                .ToList();
                
            if (memberKingdoms.Count > 1)
            {
                // Cross-kingdom alliances are more scandalous
                severity += 0.3f;
                
                // At-war kingdoms are extremely scandalous
                for (int i = 0; i < memberKingdoms.Count - 1; i++)
                {
                    for (int j = i + 1; j < memberKingdoms.Count; j++)
                    {
                        if (memberKingdoms[i].IsAtWarWith(memberKingdoms[j]))
                        {
                            severity += 0.5f;
                            break;
                        }
                    }
                }
            }
            
            return MathF.Min(1f, severity);
        }

        private void GenerateLeak(Alliance alliance, Clan leakSource, float severity, string context)
        {
            var leak = new LeakRecord(alliance, leakSource, severity, context);
            _recentLeaks.Add(leak);
            
            // Apply immediate consequences
            ApplyLeakConsequences(leak);
            
            // Reduce alliance secrecy
            alliance.SecrecyLevel = MathF.Max(0f, alliance.SecrecyLevel - severity * 0.3f);
            alliance.AddHistoryEntry($"Information leaked by {leakSource?.Name ?? "unknown source"} (Severity: {severity:F2})");
            
            // Show notification
            if (alliance.HasMember(Hero.MainHero.Clan))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Rumors about your secret alliance with {GetOtherMemberNames(alliance, Hero.MainHero.Clan)} are spreading!",
                    Colors.Red));
            }
            
            Debug.Print($"[SecretAlliances] Leak generated for {alliance.Name} by {leakSource?.Name} (Severity: {severity:F2}, Context: {context})");
        }

        private void ApplyLeakConsequences(LeakRecord leak)
        {
            var alliance = _allianceService?.GetAllActiveAlliances()?.FirstOrDefault(a => a.Id == leak.AllianceId);
            if (alliance == null) return;
            
            var members = alliance.GetMemberClans();
            
            foreach (var member in members)
            {
                // Increase exposure for all members
                AddExposureRisk(member, leak.Severity * 0.2f, "Leak consequence");
                
                // Apply relationship penalties with non-allied clans
                ApplyRelationshipPenalties(member, alliance, leak.Severity);
                
                // Apply kingdom-level consequences if appropriate
                ApplyKingdomConsequences(member, alliance, leak.Severity);
            }
        }

        private void ApplyRelationshipPenalties(Clan targetClan, Alliance alliance, float severity)
        {
            if (targetClan?.Leader == null) return;
            
            var penaltyAmount = (int)(severity * 20f); // Up to -20 relation
            
            // Apply penalties with other clan leaders who might disapprove
            foreach (var otherClan in Clan.All)
            {
                if (otherClan == targetClan || otherClan.IsEliminated || otherClan.Leader == null)
                    continue;
                    
                if (alliance.HasMember(otherClan))
                    continue; // Don't penalize alliance members
                
                // Chance of disapproval based on various factors
                var disapprovalChance = 0.3f;
                
                // Same kingdom = more likely to disapprove of secret alliances
                if (otherClan.Kingdom == targetClan.Kingdom)
                    disapprovalChance += 0.4f;
                    
                // Enemy kingdoms might actually approve of instability
                if (otherClan.Kingdom != null && targetClan.Kingdom != null && 
                    otherClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
                    disapprovalChance -= 0.2f;
                
                if (MBRandom.RandomFloat < disapprovalChance)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        targetClan.Leader, otherClan.Leader, -penaltyAmount);
                }
            }
        }

        private void ApplyKingdomConsequences(Clan targetClan, Alliance alliance, float severity)
        {
            if (targetClan?.Kingdom?.Leader == null || targetClan.Leader == null)
                return;
                
            // Check if the alliance involves external kingdoms
            var hasExternalMembers = alliance.GetMemberClans()
                .Any(c => c.Kingdom != targetClan.Kingdom);
                
            if (!hasExternalMembers)
                return; // Internal alliances are less concerning
            
            // Apply consequences based on severity
            if (severity > 0.7f && MBRandom.RandomFloat < 0.4f) // High severity, 40% chance
            {
                // Severe consequences: major influence loss and relation penalty with ruler
                ChangeClanInfluenceAction.Apply(targetClan, -100f);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    targetClan.Leader, targetClan.Kingdom.Leader, -30);
                    
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{targetClan.Kingdom.Leader.Name} is displeased with {targetClan.Name}'s secret dealings",
                    Colors.Red));
            }
            else if (severity > 0.4f && MBRandom.RandomFloat < 0.6f) // Medium severity, 60% chance
            {
                // Moderate consequences: influence loss
                ChangeClanInfluenceAction.Apply(targetClan, -50f);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    targetClan.Leader, targetClan.Kingdom.Leader, -15);
            }
        }

        private void DecayExposureScores()
        {
            var keys = _clanExposureScores.Keys.ToList();
            foreach (var key in keys)
            {
                _clanExposureScores[key] = MathF.Max(0f, _clanExposureScores[key] - EXPOSURE_DECAY_RATE);
                
                // Remove entries that have decayed to zero
                if (_clanExposureScores[key] <= 0.001f)
                {
                    _clanExposureScores.Remove(key);
                }
            }
        }

        private void CleanupOldLeaks()
        {
            // Remove leaks older than 1 year to prevent memory bloat
            _recentLeaks.RemoveAll(l => !l.IsRecent() && (CampaignTime.Now - l.LeakTime).ToYears > 1f);
            
            // Also limit total number of leaks stored
            if (_recentLeaks.Count > MAX_LEAK_HISTORY)
            {
                _recentLeaks = _recentLeaks.OrderByDescending(l => l.LeakTime.ElapsedDaysUntilNow).Take(MAX_LEAK_HISTORY).ToList();
            }
        }

        private string GetOtherMemberNames(Alliance alliance, Clan excludeClan)
        {
            var otherMembers = alliance.GetMemberClans().Where(c => c != excludeClan).ToList();
            if (otherMembers.Count == 1)
                return otherMembers[0].Name.ToString();
            else if (otherMembers.Count == 2)
                return $"{otherMembers[0].Name} and {otherMembers[1].Name}";
            else
                return $"{otherMembers[0].Name} and {otherMembers.Count - 1} others";
        }

        #endregion
    }

    /// <summary>
    /// Record of a leak occurrence
    /// </summary>
    [SaveableClass(11)]
    public class LeakRecord
    {
        [SaveableField(1)]
        public string AllianceId { get; set; }
        
        [SaveableField(2)]
        public string LeakSourceClanId { get; set; }
        
        [SaveableField(3)]
        public float Severity { get; set; }
        
        [SaveableField(4)]
        public CampaignTime LeakTime { get; set; }
        
        [SaveableField(5)]
        public string Context { get; set; }

        public LeakRecord()
        {
            LeakTime = CampaignTime.Now;
        }

        public LeakRecord(Alliance alliance, Clan leakSource, float severity, string context) : this()
        {
            AllianceId = alliance?.Id ?? "";
            LeakSourceClanId = leakSource?.Id.ToString() ?? "";
            Severity = severity;
            Context = context ?? "";
        }

        public bool InvolvesClan(Clan clan)
        {
            return clan != null && LeakSourceClanId == clan.Id.ToString();
        }

        public bool IsRecent()
        {
            return (CampaignTime.Now - LeakTime).ToDays < 30; // Recent = within 30 days
        }

        public Clan GetLeakSourceClan()
        {
            if (string.IsNullOrEmpty(LeakSourceClanId)) return null;
            
            return Clan.All.FirstOrDefault(c => c.Id.ToString() == LeakSourceClanId);
        }
    }
}