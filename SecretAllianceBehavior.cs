using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using SecretAlliances.Infrastructure;

namespace SecretAlliances
{
    public class SecretAllianceBehavior : CampaignBehaviorBase
    {
        private List<SecretAllianceRecord> _alliances = new List<SecretAllianceRecord>();
        private List<AllianceIntelligence> _intelligence = new List<AllianceIntelligence>();

        // Advanced feature data collections
        private List<AllianceContract> _contracts = new List<AllianceContract>();
        private List<MilitaryCoordinationData> _militaryData = new List<MilitaryCoordinationData>();
        private List<EconomicNetworkData> _economicData = new List<EconomicNetworkData>();
        private List<SpyNetworkData> _spyData = new List<SpyNetworkData>();

        private int _nextGroupId = 1; // For coalition support
        private bool _hasRunFirstDayDiagnostics = false;
        private int _lastEvaluationDay = -1;

        // Operation cooldown tracking (ephemeral - not saved for simplicity)
        private Dictionary<MBGUID, Dictionary<int, int>> _operationCooldowns = new Dictionary<MBGUID, Dictionary<int, int>>();

        // Trade transfer tracking for magnitude percentile calculations
        private Dictionary<MBGUID, List<TradeTransferRecord>> _recentTransfers = new Dictionary<MBGUID, List<TradeTransferRecord>>();

        // Advanced feature caches for performance
        private Dictionary<MBGUID, float> _allianceInfluenceCache = new Dictionary<MBGUID, float>();
        private Dictionary<MBGUID, int> _diplomaticImmunityCache = new Dictionary<MBGUID, int>();
        private int _lastCacheUpdateDay = -1;

        // Configuration constants - replaced by dynamic config
        private AllianceConfig Config => AllianceConfig.Instance;

        public override void RegisterEvents()
        {
            // Daily clan processing
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);

            // Battle events for alliance considerations
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnBattleStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnded);

            // Political events
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);

            // Hero death affects alliances
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

            // Peace/war declarations affect alliance dynamics
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceDeclared);

            // Additional events for advanced features
            CampaignEvents.HeroesMarried.AddNonSerializedListener(this, OnHeroMarried);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnCaravanTransactionCompletedEvent.AddNonSerializedListener(this, OnCaravanTransaction);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageRaided);
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_Alliances", ref _alliances);
            dataStore.SyncData("SecretAlliances_Intelligence", ref _intelligence);
            dataStore.SyncData("SecretAlliances_NextGroupId", ref _nextGroupId);
            dataStore.SyncData("SecretAlliances_Contracts", ref _contracts);
            dataStore.SyncData("SecretAlliances_MilitaryData", ref _militaryData);
            dataStore.SyncData("SecretAlliances_EconomicData", ref _economicData);
            dataStore.SyncData("SecretAlliances_SpyData", ref _spyData);
            dataStore.SyncData("SecretAlliances_HasRunFirstDayDiagnostics", ref _hasRunFirstDayDiagnostics);
        }

        private void OnDailyTickClan(Clan clan)
        {
            try
            {
                if (clan == null || clan.IsEliminated) return;

                // Ensure Config is accessible - if not, skip processing to avoid crashes
                if (Config == null)
                {
                    Debug.Print("[SecretAlliances] Config is null, skipping daily processing");
                    return;
                }

                // Ensure collections are initialized
                if (_alliances == null) _alliances = new List<SecretAllianceRecord>();

                // First-day diagnostics (run once)
                if (!_hasRunFirstDayDiagnostics && clan == Clan.All.FirstOrDefault())
                {
                    RunInitializationDiagnostics();
                    _hasRunFirstDayDiagnostics = true;
                }

                // Process all alliances involving this clan
                var relevantAlliances = _alliances.Where(a =>
                    a != null && a.IsActive &&
                    (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id)).ToList();

                foreach (var alliance in relevantAlliances)
                {
                    EvaluateAlliance(alliance);

                    // Process pact effects
                    ProcessTradePactEffects(alliance);
                    ProcessMilitaryPactEffects(alliance);

                    // Process aging and decay
                    ProcessAllianceAging(alliance);

                    // Age cooldown per alliance
                    if (alliance.CooldownDays > 0)
                    {
                        alliance.CooldownDays--;
                    }

                    // Age betrayal cooldown
                    if (alliance.BetrayalCooldownDays > 0)
                    {
                        alliance.BetrayalCooldownDays--;
                    }
                }

                // Check for new alliance opportunities with enhanced AI
                EvaluateAllianceFormationAI(clan);

                // Process operations framework
                ProcessOperationsDaily(clan);

                // Process intelligence aging daily
                ProcessIntelligenceAging();

                // Global processes (once per day, triggered by first clan)
                if (clan == Clan.All.FirstOrDefault())
                {
                    CalculateCoalitionCohesion();
                    ProcessForcedRevealMechanics();
                    ProcessBetrayalEvaluations();
                    CleanupOldData();

                    // Advanced feature processing
                    if (Config.EnableAdvancedFeatures)
                    {
                        ProcessAdvancedFeatures();
                        ProcessContractManagement();
                        ProcessReputationDecay();
                        ProcessAllianceRankProgression();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error in OnDailyTickClan for clan {clan?.Name.ToString() ?? "Unknown"}: {ex.Message}");
            }
        }

        #region Initialization and Diagnostics

        private void RunInitializationDiagnostics()
        {
            try
            {
                // Ensure collections are initialized
                if (_alliances == null) _alliances = new List<SecretAllianceRecord>();
                if (_intelligence == null) _intelligence = new List<AllianceIntelligence>();

                int activeAlliances = _alliances.Count(a => a != null && a.IsActive);
                float avgStrength = activeAlliances > 0 ? _alliances.Where(a => a != null && a.IsActive).Average(a => a.Strength) : 0f;
                float avgSecrecy = activeAlliances > 0 ? _alliances.Where(a => a != null && a.IsActive).Average(a => a.Secrecy) : 0f;
                int numGroups = _alliances.Where(a => a != null && a.IsActive && a.GroupId > 0).Select(a => a.GroupId).Distinct().Count();

                Debug.Print($"[SecretAlliances] Initialization Diagnostics:");
                Debug.Print($"  Active Alliances: {activeAlliances}");
                Debug.Print($"  Average Strength: {avgStrength:F3}");
                Debug.Print($"  Average Secrecy: {avgSecrecy:F3}");
                Debug.Print($"  Coalition Groups: {numGroups}");
                Debug.Print($"  Intelligence Records: {_intelligence.Count}");

                // Repair invalid UniqueId entries
                RepairUniqueIds();
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error in RunInitializationDiagnostics: {ex.Message}");
            }
        }

        private void RepairUniqueIds()
        {
            try
            {
                if (_alliances == null) return;

                int repaired = 0;
                foreach (var alliance in _alliances)
                {
                    if (alliance == null) continue;

                    if (alliance.UniqueId == default(MBGUID))
                    {
                        // Ensure InitiatorClanId is not null before using it
                        if (alliance.InitiatorClanId != default(MBGUID))
                        {
                            alliance.UniqueId = alliance.InitiatorClanId; // Use InitiatorClanId as fallback
                            repaired++;
                        }
                        else if (alliance.TargetClanId != default(MBGUID))
                        {
                            alliance.UniqueId = alliance.TargetClanId; // Fallback to TargetClanId
                            repaired++;
                        }
                    }
                }

                if (repaired > 0)
                {
                    Debug.Print($"[SecretAlliances] Repaired {repaired} invalid UniqueId entries");
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error in RepairUniqueIds: {ex.Message}");
            }
        }

        #endregion

        #region Alliance Formation AI

        private void EvaluateAllianceFormationAI(Clan clan)
        {
            try
            {
                if (clan == null || Config == null) return;
                if (MBRandom.RandomFloat > Config.FormationBaseChance) return;

                // Check daily formation limit
                int formationsToday = _alliances.Count(a => a != null && a.CreatedGameDay == CampaignTime.Now.GetDayOfYear);
                if (formationsToday >= Config.MaxDailyFormations) return;

                // Find potential alliance targets
                var candidates = Clan.All.Where(c =>
                    c != null &&
                    c != clan &&
                    !c.IsEliminated &&
                    !c.IsMinorFaction &&
                    !HasExistingAlliance(clan, c)).ToList();

                if (!candidates.Any()) return;

                foreach (var candidate in candidates.Take(3)) // Limit evaluation to prevent performance issues
                {
                    if (candidate == null) continue;

                    float formationChance = CalculateFormationChance(clan, candidate);

                    if (Config.DebugVerbose && formationChance > 0.05f)
                    {
                        Debug.Print($"[SecretAlliances] Formation chance {clan.Name} -> {candidate.Name}: {formationChance:F3}");
                    }

                    if (MBRandom.RandomFloat < formationChance)
                    {
                        CreateNewAlliance(clan, candidate);
                        break; // Only one formation per clan per day
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error in EvaluateAllianceFormationAI for clan {clan?.Name.ToString() ?? "Unknown"}: {ex.Message}");
            }
        }

        private float CalculateFormationChance(Clan initiator, Clan target)
        {
            try
            {
                if (initiator == null || target == null || Config == null) return 0f;

                float baseChance = Config.FormationBaseChance;

                // Mutual enemies factor
                float mutualEnemiesFactor = 1.0f;
                try
                {
                    mutualEnemiesFactor = HasCommonEnemies(initiator, target) ? 1.5f : 1.0f;
                }
                catch (Exception ex)
                {
                    Debug.Print($"[SecretAlliances] Error in HasCommonEnemies: {ex.Message}");
                }

                // Economic disparity factor (desperation)
                float desperationFactor = 1.0f;
                try
                {
                    float initiatorDesperation = CalculateDesperationLevel(initiator);
                    float targetDesperation = CalculateDesperationLevel(target);
                    desperationFactor = 1.0f + (initiatorDesperation + targetDesperation) * 0.5f;
                }
                catch (Exception ex)
                {
                    Debug.Print($"[SecretAlliances] Error in CalculateDesperationLevel: {ex.Message}");
                }

                // Political pressure factor
                float pressureFactor = 1.0f;
                try
                {
                    float politicalPressure = CalculatePoliticalPressure(initiator, target);
                    pressureFactor = 1.0f + politicalPressure * 0.3f;
                }
                catch (Exception ex)
                {
                    Debug.Print($"[SecretAlliances] Error in CalculatePoliticalPressure: {ex.Message}");
                }

                // Military power relationship
                float militaryFactor = 1.0f;
                try
                {
                    militaryFactor = CalculateMilitaryPowerFactor(initiator, target);
                }
                catch (Exception ex)
                {
                    Debug.Print($"[SecretAlliances] Error in CalculateMilitaryPowerFactor: {ex.Message}");
                }

                float finalChance = baseChance * mutualEnemiesFactor * desperationFactor * pressureFactor * militaryFactor;
                float cappedChance = MathF.Min(finalChance, 0.5f);

                // Add detailed debugging output to match the format seen in logs
                if (Config.DebugVerbose || cappedChance > 0.05f)
                {
                    float initiatorDesperation = (desperationFactor - 1.0f) / 0.5f; // Reverse the calculation to get original desperation
                    float politicalPressure = (pressureFactor - 1.0f) / 0.3f; // Reverse the calculation to get original pressure
                    bool commonEnemies = mutualEnemiesFactor > 1.0f;

                    Debug.Print($"[SecretAlliances] AI Formation evaluation {initiator.Name} -> {target.Name}: " +
                              $"chance={cappedChance:F3}, desperation={initiatorDesperation:F2}, " +
                              $"commonEnemies={commonEnemies}, political_pressure={politicalPressure:F2}");

                    if (cappedChance < 0.1f)
                    {
                        if (!commonEnemies)
                            Debug.Print($"  Low formation chance due to: no common enemies");
                        else if (initiatorDesperation < 0.3f)
                            Debug.Print($"  Low formation chance due to: low desperation");
                        else if (politicalPressure < 0.2f)
                            Debug.Print($"  Low formation chance due to: low political pressure");
                    }
                }

                return cappedChance;
            }
            catch (Exception ex)
            {
                Debug.Print($"[SecretAlliances] Error in CalculateFormationChance: {ex.Message}");
                return 0f;
            }
        }



        private float CalculateMilitaryPowerFactor(Clan clan1, Clan clan2)
        {
            if (clan1.TotalStrength <= 0 || clan2.TotalStrength <= 0) return 1.0f;

            float ratio = clan1.TotalStrength / clan2.TotalStrength;

            // Prefer alliances between similarly powered clans, or strong helping weak
            if (ratio >= 0.5f && ratio <= 2.0f)
            {
                return 1.2f; // Balanced power
            }
            else if (ratio > 2.0f)
            {
                return 1.1f; // Strong helping weak (moderate bonus)
            }
            else
            {
                return 0.8f; // Very unbalanced (less likely)
            }
        }


        private void CreateNewAlliance(Clan initiator, Clan target)
        {
            var alliance = new SecretAllianceRecord
            {
                InitiatorClanId = initiator.Id,
                TargetClanId = target.Id,
                UniqueId = DeriveAllianceUniqueId(initiator.Id, target.Id),
                IsActive = true,
                Strength = MBRandom.RandomFloatRanged(0.2f, 0.4f),
                Secrecy = MBRandom.RandomFloatRanged(0.6f, 0.9f),
                TrustLevel = MBRandom.RandomFloatRanged(0.3f, 0.5f),
                CreatedGameDay = CampaignTime.Now.GetDayOfYear,
                GroupId = 0, // Will be assigned later if joins coalition
                HasCommonEnemies = HasCommonEnemies(initiator, target),
                PoliticalPressure = CalculatePoliticalPressure(initiator, target),
                NextEligibleOperationDay = CampaignTime.Now.GetDayOfYear + Config.OperationIntervalDays
            };

            _alliances.Add(alliance);

            // Enhanced debug and player information
            string reasonsText = "";
            if (alliance.HasCommonEnemies) reasonsText += "mutual enemies, ";
            if (CalculateDesperationLevel(initiator) > 0.5f || CalculateDesperationLevel(target) > 0.5f)
                reasonsText += "desperation, ";
            if (alliance.PoliticalPressure > 0.4f) reasonsText += "political pressure, ";
            reasonsText = reasonsText.TrimEnd(' ', ',');

            Debug.Print($"[SecretAlliances] New alliance formed: {initiator.Name} <-> {target.Name} " +
                       $"(S:{alliance.Strength:F2}, Sec:{alliance.Secrecy:F2}) Reasons: {reasonsText}");

            // Inform player with more context if they're involved or nearby
            if (initiator == Clan.PlayerClan || target == Clan.PlayerClan)
            {
                var otherClan = initiator == Clan.PlayerClan ? target : initiator;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You have formed a secret alliance with {otherClan.Name}!", Colors.Green));
            }
            else if (Config.DebugVerbose || MBRandom.RandomFloat < 0.3f) // 30% chance for intel
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Spies report that {initiator.Name} and {target.Name} may be coordinating secretly...",
                    Colors.Yellow));
            }
        }

        #endregion

        #region Operations Framework

        private void ProcessOperationsDaily(Clan clan)
        {
            // Process operations for alliances involving this clan
            var relevantAlliances = _alliances.Where(a =>
                a.IsActive &&
                (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id)).ToList();

            foreach (var alliance in relevantAlliances)
            {
                EvaluateAndExecuteOperations(alliance);
            }
        }

        private void EvaluateAndExecuteOperations(SecretAllianceRecord alliance)
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;

            // Check if operation is eligible
            if (currentDay < alliance.NextEligibleOperationDay) return;

            // Adjust interval based on conditions
            int baseInterval = Config.OperationIntervalDays;
            if (alliance.PoliticalPressure > 0.6f || alliance.TrustLevel > 0.75f)
            {
                baseInterval = Math.Max(Config.OperationAdaptiveMinDays, baseInterval - 5);
            }

            // Roll for operation
            if (MBRandom.RandomFloat < 0.4f) // 40% chance per eligible day
            {
                ExecuteRandomOperation(alliance);
                alliance.NextEligibleOperationDay = currentDay + baseInterval + MBRandom.RandomInt(-2, 3);
            }
        }

        private void ExecuteRandomOperation(SecretAllianceRecord alliance)
        {
            // Check cooldowns
            if (!_operationCooldowns.ContainsKey(alliance.UniqueId))
            {
                _operationCooldowns[alliance.UniqueId] = new Dictionary<int, int>();
            }

            var cooldowns = _operationCooldowns[alliance.UniqueId];
            int currentDay = CampaignTime.Now.GetDayOfYear;

            // Filter available operations by cooldown
            var availableOps = new List<OperationType>();
            if (!cooldowns.ContainsKey(1) || cooldowns[1] <= currentDay) availableOps.Add(OperationType.CovertAid);
            if (!cooldowns.ContainsKey(2) || cooldowns[2] <= currentDay) availableOps.Add(OperationType.SpyProbe);
            if (!cooldowns.ContainsKey(3) || cooldowns[3] <= currentDay) availableOps.Add(OperationType.RecruitmentFeelers);
            if (!cooldowns.ContainsKey(4) || cooldowns[4] <= currentDay) availableOps.Add(OperationType.SabotageRaid);
            if (!cooldowns.ContainsKey(5) || cooldowns[5] <= currentDay) availableOps.Add(OperationType.CounterIntelligence);

            if (!availableOps.Any()) return;

            // Select operation type (weighted by alliance characteristics)
            OperationType selectedOp = SelectOperationByWeight(alliance, availableOps);

            ExecuteOperation(alliance, selectedOp);
        }

        private OperationType SelectOperationByWeight(SecretAllianceRecord alliance, List<OperationType> availableOps)
        {
            // Weight operations based on alliance state
            var weights = new Dictionary<OperationType, float>();

            foreach (var op in availableOps)
            {
                float w;
                switch (op)
                {
                    case OperationType.CovertAid:
                        w = 0.3f + (alliance.TrustLevel * 0.2f);
                        break;
                    case OperationType.SpyProbe:
                        w = 0.25f + (alliance.PoliticalPressure * 0.15f);
                        break;
                    case OperationType.RecruitmentFeelers:
                        w = 0.15f + (alliance.Strength * 0.1f);
                        break;
                    case OperationType.SabotageRaid:
                        w = 0.2f + ((1f - alliance.Secrecy) * 0.1f);
                        break;
                    case OperationType.CounterIntelligence:
                        w = 0.1f + (alliance.LeakAttempts * 0.05f);
                        break;
                    default:
                        w = 0.1f;
                        break;
                }
                weights[op] = w;
            }

            float totalWeight = weights.Values.Sum();
            float roll = MBRandom.RandomFloat * totalWeight;
            float current = 0f;

            foreach (var kvp in weights)
            {
                current += kvp.Value;
                if (roll <= current)
                {
                    return kvp.Key;
                }
            }

            return availableOps.FirstOrDefault();
        }

        private void ExecuteOperation(SecretAllianceRecord alliance, OperationType operationType)
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;
            bool success = RollOperationSuccess(alliance, operationType);

            // Set cooldown
            if (!_operationCooldowns.ContainsKey(alliance.UniqueId))
            {
                _operationCooldowns[alliance.UniqueId] = new Dictionary<int, int>();
            }

            var cooldowns = _operationCooldowns[alliance.UniqueId];
            cooldowns[(int)operationType] = currentDay + GetOperationCooldown(operationType);

            // Execute operation effects
            switch (operationType)
            {
                case OperationType.CovertAid:
                    ExecuteCovertAid(alliance, success);
                    break;
                case OperationType.SpyProbe:
                    ExecuteSpyProbe(alliance, success);
                    break;
                case OperationType.RecruitmentFeelers:
                    ExecuteRecruitmentFeelers(alliance, success);
                    break;
                case OperationType.SabotageRaid:
                    ExecuteSabotageRaid(alliance, success);
                    break;
                case OperationType.CounterIntelligence:
                    ExecuteCounterIntelligence(alliance, success);
                    break;
            }

            if (success)
            {
                alliance.SuccessfulOperations++;
            }
            else
            {
                // Apply failure penalties
                ApplyOperationFailure(alliance, operationType);
            }

            if (Config.DebugVerbose)
            {
                Debug.Print($"[SecretAlliances] Operation {operationType} executed for alliance " +
                          $"{alliance.GetInitiatorClan()?.Name} <-> {alliance.GetTargetClan()?.Name} " +
                          $"(Success: {success})");
            }
        }

        private bool RollOperationSuccess(SecretAllianceRecord alliance, OperationType operationType)
        {
            float difficulty;
            switch (operationType)
            {
                case OperationType.CovertAid:
                    difficulty = 0.2f;
                    break;
                case OperationType.SpyProbe:
                    difficulty = 0.4f;
                    break;
                case OperationType.RecruitmentFeelers:
                    difficulty = 0.45f;
                    break;
                case OperationType.SabotageRaid:
                    difficulty = 0.7f;
                    break;
                case OperationType.CounterIntelligence:
                    difficulty = 0.3f;
                    break;
                default:
                    difficulty = 0.5f;
                    break;
            }

            float successChance = alliance.TrustLevel * 0.4f
                                + alliance.Strength * 0.4f
                                + MBRandom.RandomFloat * 0.2f;

            var initiator = alliance.GetInitiatorClan();
            if (initiator != null && initiator.Leader != null)
            {
                successChance += initiator.Leader.GetAttributeValue(DefaultCharacterAttributes.Cunning) / 200f;
            }

            return successChance > difficulty;
        }

        private int GetOperationCooldown(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.CovertAid:
                    return 15;
                case OperationType.SpyProbe:
                    return Config.SpyProbeCooldownDays;
                case OperationType.RecruitmentFeelers:
                    return Config.RecruitmentCooldownDays;
                case OperationType.SabotageRaid:
                    return Config.SabotageCooldownDays;
                case OperationType.CounterIntelligence:
                    return Config.CounterIntelCooldownDays;
                default:
                    return 20;
            }
        }

        private void ExecuteCovertAid(SecretAllianceRecord alliance, bool success)
        {
            if (success)
            {
                alliance.Strength += MBRandom.RandomFloatRanged(0.01f, 0.03f);
                alliance.TrustLevel += MBRandom.RandomFloatRanged(0.01f, 0.02f);
                alliance.Secrecy -= MBRandom.RandomFloatRanged(0.005f, 0.015f);

                // Clamp values
                alliance.Strength = MathF.Min(1f, alliance.Strength);
                alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel);
                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy);
            }
        }

        private void ExecuteSpyProbe(SecretAllianceRecord alliance, bool success)
        {
            if (success)
            {
                // Create intelligence about a third clan
                var targetClan = FindSpyTarget(alliance);
                if (targetClan != null)
                {
                    CreateSpyIntelligence(alliance, targetClan);
                }
            }
            else
            {
                // Failure increases leak chance
                CheckForLeaks(alliance, 2.0f); // Double leak chance
                alliance.Secrecy -= MBRandom.RandomFloatRanged(0.02f, 0.04f);
                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy);
            }
        }

        private void ExecuteRecruitmentFeelers(SecretAllianceRecord alliance, bool success)
        {
            if (success)
            {
                var candidate = FindRecruitmentCandidate(alliance);
                if (candidate != null && MBRandom.RandomFloat < CalculateRecruitmentSuccessChance(alliance, candidate))
                {
                    CreateRecruitedAlliance(alliance, candidate);
                }
            }
        }

        private void ExecuteSabotageRaid(SecretAllianceRecord alliance, bool success)
        {
            if (success)
            {
                var targetClan = FindSabotageTarget(alliance);
                if (targetClan != null)
                {
                    // Log virtual impact and create high-severity intel
                    CreateSabotageIntelligence(alliance, targetClan);

                    if (Config.DebugVerbose)
                    {
                        Debug.Print($"[SecretAlliances] Sabotage executed against {targetClan.Name}");
                    }
                }
            }
            else
            {
                // High failure chance generates leak
                if (MBRandom.RandomFloat < 0.3f)
                {
                    CheckForLeaks(alliance, 3.0f); // Triple leak chance
                }
                alliance.Secrecy -= MBRandom.RandomFloatRanged(0.03f, 0.08f);
                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy);
            }
        }

        private void ExecuteCounterIntelligence(SecretAllianceRecord alliance, bool success)
        {
            if (success)
            {
                // Reduce existing intelligence reliability
                var relevantIntel = _intelligence.Where(i => i.AllianceId == alliance.UniqueId).ToList();
                foreach (var intel in relevantIntel)
                {
                    intel.ReliabilityScore = MathF.Max(0f, intel.ReliabilityScore - MBRandom.RandomFloatRanged(0.1f, 0.3f));
                }

                // Set counter-intel buff
                alliance.CounterIntelBuffExpiryDay = CampaignTime.Now.GetDayOfYear + 30;

                Debug.Print($"[SecretAlliances] Counter-intelligence operation reduced {relevantIntel.Count} intel records");
            }
        }

        private void ApplyOperationFailure(SecretAllianceRecord alliance, OperationType operationType)
        {
            float secrecyLoss;
            switch (operationType)
            {
                case OperationType.CovertAid:
                    secrecyLoss = MBRandom.RandomFloatRanged(0.01f, 0.02f);
                    break;
                case OperationType.SpyProbe:
                    secrecyLoss = MBRandom.RandomFloatRanged(0.02f, 0.04f);
                    break;
                case OperationType.RecruitmentFeelers:
                    secrecyLoss = MBRandom.RandomFloatRanged(0.015f, 0.03f);
                    break;
                case OperationType.SabotageRaid:
                    secrecyLoss = MBRandom.RandomFloatRanged(0.03f, 0.08f);
                    break;
                case OperationType.CounterIntelligence:
                    secrecyLoss = MBRandom.RandomFloatRanged(0.01f, 0.025f);
                    break;
                default:
                    secrecyLoss = 0.02f;
                    break;
            }

            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);
        }

        #endregion

        #region Helper Methods for Operations

        private Clan FindSpyTarget(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Kingdom == null) return null;

            // Prefer enemies or neutrals
            var candidates = Clan.All.Where(c =>
                c != initiator &&
                c != target &&
                !c.IsEliminated &&
                !c.IsMinorFaction &&
                (initiator.Kingdom.IsAtWarWith(c.Kingdom) || c.Kingdom == null)).ToList();

            return candidates.Any() ? candidates[MBRandom.RandomInt(candidates.Count)] : null;
        }

        private Clan FindRecruitmentCandidate(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Find clans that complement the alliance's power and have shared interests
            var candidates = Clan.All.Where(c =>
                c != initiator &&
                c != target &&
                !c.IsEliminated &&
                !c.IsMinorFaction &&
                !HasExistingAlliance(initiator, c) &&
                !HasExistingAlliance(target, c) &&
                (HasCommonEnemies(initiator, c) || HasCommonEnemies(target, c))).ToList();

            return candidates.Any() ? candidates[MBRandom.RandomInt(candidates.Count)] : null;
        }

        private static MBGUID DeriveAllianceUniqueId(MBGUID initiatorClanId, MBGUID targetClanId)
        {
            // Per your PR requirement: use InitiatorClanId if no composite scheme
            if (!initiatorClanId.Equals(default(MBGUID)))
                return initiatorClanId;
            if (!targetClanId.Equals(default(MBGUID)))
                return targetClanId;
            return default(MBGUID);
        }

        private Clan FindSabotageTarget(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Kingdom == null) return null;

            // Target enemy clans
            var enemies = Clan.All.Where(c =>
                c != initiator &&
                c != target &&
                !c.IsEliminated &&
                c.Kingdom != null &&
                (initiator.Kingdom.IsAtWarWith(c.Kingdom) || target.Kingdom?.IsAtWarWith(c.Kingdom) == true)).ToList();

            return enemies.Any() ? enemies[MBRandom.RandomInt(enemies.Count)] : null;
        }

        private float CalculateRecruitmentSuccessChance(SecretAllianceRecord alliance, Clan candidate)
        {
            float baseChance = alliance.Strength * 0.3f;
            float desperationBonus = CalculateDesperationLevel(candidate) * 0.4f;
            float trustBonus = alliance.TrustLevel * 0.2f;

            return MathF.Min(baseChance + desperationBonus + trustBonus, 0.8f);
        }

        private static float ExpF(float x)
        {
            return (float)Math.Exp(x);
        }

        private void CreateRecruitedAlliance(SecretAllianceRecord parentAlliance, Clan newClan)
        {
            var initiator = parentAlliance.GetInitiatorClan();
            if (initiator == null) return;

            var newAlliance = new SecretAllianceRecord
            {
                InitiatorClanId = initiator.Id,
                TargetClanId = newClan.Id,
                UniqueId = DeriveAllianceUniqueId(initiator.Id, newClan.Id),
                IsActive = true,
                Strength = parentAlliance.Strength * 0.7f, // Inherited but reduced
                Secrecy = parentAlliance.Secrecy * 0.8f,
                TrustLevel = 0.4f, // Start with moderate trust
                CreatedGameDay = CampaignTime.Now.GetDayOfYear,
                GroupId = parentAlliance.GroupId > 0 ? parentAlliance.GroupId : AssignNewGroupId(parentAlliance),
                HasCommonEnemies = HasCommonEnemies(initiator, newClan),
                PoliticalPressure = CalculatePoliticalPressure(initiator, newClan),
                NextEligibleOperationDay = CampaignTime.Now.GetDayOfYear + Config.OperationIntervalDays
            };

            _alliances.Add(newAlliance);

            Debug.Print($"[SecretAlliances] Recruitment successful: {newClan.Name} joined coalition (Group {newAlliance.GroupId})");
        }

        private int AssignNewGroupId(SecretAllianceRecord alliance)
        {
            if (alliance.GroupId > 0) return alliance.GroupId;

            int newGroupId = _nextGroupId++;
            alliance.GroupId = newGroupId;

            return newGroupId;
        }

        #endregion

        private void EvaluateAlliance(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator == null || target == null || initiator.IsEliminated || target.IsEliminated)
            {
                alliance.IsActive = false;
                return;
            }

            // Update alliance dynamics
            UpdateAllianceSecrecy(alliance);
            UpdateAllianceStrength(alliance);
            CheckForLeaks(alliance);
            EvaluateCoupOpportunity(alliance);
            UpdateTrustLevel(alliance);

            alliance.DaysWithoutLeak++;
        }

        private void UpdateAllianceSecrecy(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Base decay
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - AllianceConfig.Instance.DailySecrecyDecay);

            // Factors that decrease secrecy
            float secrecyLoss = 0f;

            // More heroes in clans = harder to keep secret
            int totalHeroes = (initiator.Heroes?.Count ?? 0) + (target.Heroes?.Count ?? 0);
            MathF.Min(0.002f, totalHeroes * 0.0005f);

            // If clans are at war with each other's allies, secrecy decreases faster
            if (AreClansInConflictingSituations(initiator, target))
            {
                secrecyLoss += 0.003f;
            }

            // Distance between clan settlements affects secrecy maintenance
            float distance = GetClanDistance(initiator, target);
            if (distance > 100f) // Long distance makes coordination harder
            {
                secrecyLoss += MathF.Min(0.002f, (distance - 100f) / 10000f); // Cap distance effect
            }

            // Leader personality affects secrecy maintenance
            if (initiator.Leader != null)
            {
                // Calculating personality-based secrecy loss
                var traits = initiator.Leader.GetHeroTraits();
                if (traits.Honor > 0) secrecyLoss += MathF.Min(0.001f, traits.Honor * 0.0005f); // Reduced and bounded
                if (traits.Generosity > 0) secrecyLoss += MathF.Min(0.0005f, traits.Generosity * 0.0002f); // Reduced and bounded
            }

            if (alliance.MilitaryPact)
            {
                secrecyLoss += 0.001f;
            }

            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);

            // Pact effects (combined to avoid duplication)
            if (alliance.TradePact) secrecyLoss += 0.0005f;
            if (alliance.MilitaryPact) secrecyLoss += 0.001f;

            // Apply bounded secrecy loss
            secrecyLoss = MathF.Min(0.01f, secrecyLoss); // Cap total daily secrecy loss to 1%
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);

        }

        private void UpdateAllianceStrength(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Base growth
            float strengthGain = AllianceConfig.Instance.DailyStrengthGrowth;

            // Factors that increase strength

            // Mutual benefit increases strength (bounded)
            float mutualBenefit = MathF.Min(1.5f, CalculateMutualBenefit(initiator, target));
            strengthGain *= (1f + mutualBenefit * 0.5f); // Cap mutual benefit effect

            // Trust level affects strength growth (bounded)
            float trustMultiplier = MathF.Min(1.5f, 0.5f + alliance.TrustLevel * 0.5f);
            strengthGain *= trustMultiplier;

            // Economic incentives boost strength
            if (alliance.BribeAmount > 0)
            {
                float bribeEffect = MathF.Min(0.5f, alliance.BribeAmount / 10000f);
                strengthGain *= (1f + bribeEffect);
            }

            // Common enemies strengthen alliance
            if (alliance.HasCommonEnemies)
            {
                strengthGain *= 1.2f;
            }

            // Recent successful operations boost strength
            if (alliance.SuccessfulOperations > 0)
            {
                float opsBonus = MathF.Min(0.5f, alliance.SuccessfulOperations * 0.05f); // Reduced multiplier
                strengthGain *= (1f + opsBonus);
            }

            if (alliance.MilitaryPact)
            {
                strengthGain *= 1.3f;
            }


            // Political pressure can drive clans together (bounded)
            float politicalPressure = MathF.Min(1.0f, CalculatePoliticalPressure(initiator, target));
            strengthGain *= (1f + politicalPressure * 0.1f); // Reduced multiplier

            // Cap daily strength gain to prevent excessive growth
            strengthGain = MathF.Min(0.01f, strengthGain); // Max 1% strength gain per day


            alliance.Strength = MathF.Min(1f, alliance.Strength + strengthGain);
        }

        private void CheckForLeaks(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Base leak chance increases as secrecy decreases
            float leakChance = AllianceConfig.Instance.LeakBaseChance * (1f - alliance.Secrecy);

            // Recent leaks make future leaks more likely (word spreads)
            if (alliance.LeakAttempts > 0)
            {
                leakChance *= 1f + (alliance.LeakAttempts * 0.2f);
            }

            // Heroes with low loyalty are more likely to leak
            var potentialInformants = GetPotentialInformants(initiator, target, alliance);
            if (potentialInformants.Any())
            {
                leakChance *= 1f + (potentialInformants.Count * 0.1f);
            }

            // Time without leaks slightly reduces chance (people forget)
            if (alliance.DaysWithoutLeak > 30)
            {
                leakChance *= MathF.Max(0.5f, 1f - (alliance.DaysWithoutLeak - 30) * 0.01f);
            }

            if (alliance.TradePact) leakChance *= 1.1f;
            if (alliance.MilitaryPact) leakChance *= 1.15f;

            if (MBRandom.RandomFloat < leakChance)
            {
                ProcessLeak(alliance, potentialInformants);
            }
        }

        private void ProcessLeak(SecretAllianceRecord alliance, List<Hero> potentialInformants)
        {
            alliance.LeakAttempts++;
            alliance.DaysWithoutLeak = 0;

            // Select an informant
            Hero informant = null;
            if (potentialInformants.Any())
            {
                informant = potentialInformants[MBRandom.RandomInt(potentialInformants.Count)];
            }

            // Determine leak severity based on informant's position and knowledge
            float severity = CalculateLeakSeverity(alliance, informant);
            alliance.LastLeakSeverity = severity;

            // Create intelligence record
            if (informant != null)
            {
                var intel = new AllianceIntelligence
                {
                    AllianceId = alliance.UniqueId, // Using unique identifier
                    InformerHeroId = informant.Id,
                    ReliabilityScore = CalculateInformerReliability(informant),
                    DaysOld = 0,
                    IsConfirmed = false,
                    SeverityLevel = severity,
                    ClanAId = alliance.InitiatorClanId,
                    ClanBId = alliance.TargetClanId,
                    IntelCategory = (int)AllianceIntelType.General

                };
                _intelligence.Add(intel);

                Debug.Print($"[Secret Alliances] Intelligence leaked by {informant.Name} (Reliability: {intel.ReliabilityScore:F2}, Severity: {severity:F2})");
            }

            // Apply consequences
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - severity * 0.1f);
            alliance.TrustLevel = MathF.Max(0f, alliance.TrustLevel - severity * 0.05f);

            // Severe leaks might end the alliance
            if (severity > 0.8f && alliance.Secrecy < 0.2f)
            {
                alliance.BetrayalRevealed = true;
                ProcessAllianceExposure(alliance, informant);
            }
        }

        private void EvaluateCoupOpportunity(SecretAllianceRecord alliance)
        {
            if (alliance.CoupAttempted ||
                alliance.Strength < AllianceConfig.Instance.CoupStrengthThreshold ||
                alliance.Secrecy > AllianceConfig.Instance.CoupSecrecyThreshold)
                return;

            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Kingdom == null || initiator.Kingdom.Leader == null)
                return;

            // Calculate coup probability
            float coupChance = CalculateCoupProbability(alliance, initiator, target);

            if (MBRandom.RandomFloat < coupChance)
            {
                AttemptCoup(alliance, initiator, target);
            }
        }

        private float CalculateCoupProbability(SecretAllianceRecord alliance, Clan initiator, Clan target)
        {
            float baseChance = 0.05f; // 5% base chance

            // Alliance strength directly affects chance
            baseChance *= alliance.Strength;

            // Desperation increases coup likelihood
            float desperationLevel = CalculateDesperationLevel(initiator);
            baseChance *= (1f + desperationLevel);

            // Military strength comparison
            float initiatorStrength = initiator.TotalStrength;
            float kingdomStrength = initiator.Kingdom.TotalStrength;
            float relativeStrength = initiatorStrength / MathF.Max(1f, kingdomStrength);

            if (relativeStrength > 0.3f) // If clan is significantly strong
            {
                baseChance *= (1f + relativeStrength);
            }

            // Target clan's contribution
            float targetContribution = target.TotalStrength / MathF.Max(1f, kingdomStrength);
            baseChance *= (1f + targetContribution * 0.5f);

            // Leader relations with current ruler
            if (initiator.Leader != null && initiator.Kingdom.Leader != null)
            {
                int relation = initiator.Leader.GetRelation(initiator.Kingdom.Leader);
                if (relation < 0)
                {
                    baseChance *= (1f + MathF.Abs(relation) * 0.01f);
                }
            }

            // Economic pressure
            if (initiator.Gold < 5000) // Clan is poor
            {
                baseChance *= 1.5f;
            }

            // Recent losses increase desperation
            if (HasRecentMilitaryLosses(initiator))
            {
                baseChance *= 1.3f;
            }
            // Military pact gives coup bonus
            if (alliance.MilitaryPact)
            {
                baseChance *= 1.15f;
            }

            return MathF.Min(0.3f, baseChance); // Cap at 30%
        }

        private void AttemptCoup(SecretAllianceRecord alliance, Clan initiator, Clan target)
        {
            alliance.CoupAttempted = true;

            var currentRuler = initiator.Kingdom.Leader;
            if (currentRuler == null || currentRuler.IsDead)
            {
                alliance.IsActive = false;
                return;
            }

            // Calculate success probability
            float successChance = CalculateCoupSuccessChance(alliance, initiator, target);

            if (MBRandom.RandomFloat < successChance)
            {
                // Successful coup
                ExecuteSuccessfulCoup(alliance, initiator, target, currentRuler);
            }
            else
            {
                // Failed coup
                ExecuteFailedCoup(alliance, initiator, target, currentRuler);
            }

            alliance.IsActive = false;
        }

        private void ExecuteSuccessfulCoup(SecretAllianceRecord alliance, Clan initiator, Clan target, Hero currentRuler)
        {
            // Determine coup type based on strength and circumstances
            float coupSeverity = alliance.Strength + (1f - alliance.Secrecy);

            if (coupSeverity > 1.5f && MBRandom.RandomFloat < 0.7f)
            {
                // Assassination coup - ruler is killed
                var executor = GetBestExecutor(initiator, target);
                if (executor != null)
                {
                    KillCharacterAction.ApplyByExecution(currentRuler, executor, true, false);
                }
            }
            else if (coupSeverity > 1.2f)
            {
                // Rebellion coup - leave kingdom with rebellion
                ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(initiator, true);

                // Target clan might follow
                if (alliance.TrustLevel > 0.7f && MBRandom.RandomFloat < 0.6f)
                {
                    ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(target, true);
                }
            }
            else
            {
                // Defection coup - join another kingdom or create new one
                var bestKingdom = FindBestKingdomForDefection(initiator, target);
                if (bestKingdom != null)
                {
                    ChangeKingdomAction.ApplyByJoinToKingdomByDefection(initiator, bestKingdom, true);

                    // Apply the bribe if there was one
                    if (alliance.BribeAmount > 0)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(target.Leader, initiator.Leader, (int)alliance.BribeAmount, false);
                    }
                }
            }

            alliance.SuccessfulOperations++;
        }

        private void ExecuteFailedCoup(SecretAllianceRecord alliance, Clan initiator, Clan target, Hero currentRuler)
        {
            // Failed coups have consequences

            // Reduce influence and relation with ruler
            if (initiator.Leader != null)
            {
                // AFTER
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(initiator.Leader, currentRuler, -30);
                ChangeClanInfluenceAction.Apply(initiator, -50f);
            }

            if (target.Leader != null)
            {
                // CHANGE TO THIS (Example for lines 420-421)
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(target.Leader, currentRuler, -20);
                ChangeClanInfluenceAction.Apply(target, -30f);
            }

            // Possible exile or punishment
            if (MBRandom.RandomFloat < 0.3f) // 30% chance of harsh punishment
            {
                ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(initiator, true);
            }
            else if (MBRandom.RandomFloat < 0.2f) // 20% chance target is also punished
            {
                if (target.Kingdom == initiator.Kingdom)
                {
                    ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(target, false);
                }
            }
        }

        // Helper Methods

        private void ProcessTradePactEffects(SecretAllianceRecord alliance)
        {
            if (!alliance.TradePact || !alliance.IsActive) return;

            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Leader == null || target?.Leader == null) return;

            // Calculate daily gold transfer based on EconomicIncentive and TrustLevel
            int baseAmount = MBRandom.RandomInt(25, 150);
            // Bound multipliers to prevent runaway effects
            float economicMultiplier = MathF.Min(2.0f, MathF.Max(0.1f, alliance.EconomicIncentive));
            float trustMultiplier = MathF.Min(1.5f, MathF.Max(0.2f, alliance.TrustLevel));

            int transferAmount = (int)(baseAmount * economicMultiplier * trustMultiplier);


            if (transferAmount < 5) return; // Skip very small amounts

            // Adaptive wealth balancing - consider wealth disparity
            int initiatorGold = initiator.Gold;
            int targetGold = target.Gold;
            int totalGold = initiatorGold + targetGold;

            if (totalGold < 1000) return; // Skip if both clans are too poor

            // Calculate wealth imbalance ratio (0 = perfect balance, 1 = extreme imbalance)
            float wealthImbalance = MathF.Abs(initiatorGold - targetGold) / (float)MathF.Max(1, totalGold);

            // Scale transfer based on imbalance (more imbalance = larger transfers)
            transferAmount = (int)(transferAmount * (0.5f + wealthImbalance));

            // Cap transfer to prevent excessive amounts
            int maxTransfer = MathF.Max(initiatorGold, targetGold) / 20; // Max 5% of richer clan's wealth
            transferAmount = MathF.Min(transferAmount, maxTransfer);


            // Transfer from richer to poorer clan
            Hero richer, poorer;
            if (initiatorGold > targetGold)
            {
                richer = initiator.Leader;
                poorer = target.Leader;
            }
            else
            {
                richer = target.Leader;
                poorer = initiator.Leader;
            }

            // Only transfer if the richer clan can afford it
            if (richer.Gold >= transferAmount)
            {
                GiveGoldAction.ApplyBetweenCharacters(richer, poorer, transferAmount, false);

                // Secrecy erosion with bounds - more activity = more secrecy loss
                float secrecyLoss = MathF.Min(0.005f, 0.0005f + (transferAmount / 50000f));
                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);

                // Incremental trust building (bounded)
                float trustGain = MathF.Min(0.002f, transferAmount / 100000f);
                alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + trustGain);


                Debug.Print($"[Secret Alliances] Trade Pact: {richer.Name} transferred {transferAmount} denars to {poorer.Name} (Alliance GroupId: {alliance.GroupId}, Trust: {alliance.TrustLevel:F2})");
            }
        }

        private void ProcessMilitaryPactEffects(SecretAllianceRecord alliance)
        {
            if (!alliance.MilitaryPact || !alliance.IsActive) return;

            Debug.Print($"[Secret Alliances] Military Pact active for alliance GroupId {alliance.GroupId} - enhanced strength growth and coordination");

            // Military pacts are handled in UpdateAllianceStrength (1.5x multiplier) and UpdateAllianceSecrecy (additional decay)
        }

        private bool AreClansInConflictingSituations(Clan clan1, Clan clan2)
        {
            if (clan1.Kingdom == null || clan2.Kingdom == null) return false;

            // Check if their kingdoms are at war
            if (clan1.Kingdom.IsAtWarWith(clan2.Kingdom)) return true;

            // Check if they have conflicting war stances
            var clan1Wars = clan1.Kingdom.GetStanceWith(clan2.Kingdom);

            return clan1.Kingdom.IsAtWarWith(clan2.Kingdom);
        }

        private float GetClanDistance(Clan clan1, Clan clan2)
        {
            // AFTER (The API added a direct property for the clan's main home)
            var settlement1 = clan1.HomeSettlement;
            var settlement2 = clan2.HomeSettlement;

            if (settlement1 == null || settlement2 == null) return 50f; // Default moderate distance

            return settlement1.Position2D.Distance(settlement2.Position2D);
        }

        private float CalculateMutualBenefit(Clan clan1, Clan clan2)
        {
            float benefit = 0f;

            // Economic complementarity
            if (clan1.Gold < clan2.Gold * 0.5f || clan2.Gold < clan1.Gold * 0.5f)
            {
                benefit += 0.2f; // Economic disparity creates mutual benefit
            }

            // Military complementarity
            float strengthRatio = clan1.TotalStrength / MathF.Max(1f, clan2.TotalStrength);
            if (strengthRatio > 2f || strengthRatio < 0.5f)
            {
                benefit += 0.15f; // Military imbalance creates benefit
            }

            // Territorial complementarity
            if (GetClanDistance(clan1, clan2) < 80f) // Close clans benefit from coordination
            {
                benefit += 0.1f;
            }

            return MathF.Min(0.5f, benefit);
        }

        private float CalculatePoliticalPressure(Clan clan1, Clan clan2)
        {
            float pressure = 0f;

            // War pressure
            // CHANGE TO THIS
            if (clan1.Kingdom != null && clan1.Kingdom.Settlements.Any(s => s.IsUnderSiege))
            {
                pressure += 0.3f;
            }

            // Low influence creates pressure to find alternatives
            // CHANGE TO THIS
            if (clan1.Leader != null && clan1.Influence < 20f)
            {
                pressure += 0.2f;
            }

            // Recent territorial losses
            if (clan1.Settlements.Count < 2) // Small clans feel more pressure
            {
                pressure += 0.15f;
            }

            return MathF.Min(1f, pressure);
        }

        private List<Hero> GetPotentialInformants(Clan initiator, Clan target, SecretAllianceRecord alliance)
        {
            var informants = new List<Hero>();

            // Heroes from both clans could be informants
            var allHeroes = new List<Hero>();
            if (initiator.Heroes != null) allHeroes.AddRange(initiator.Heroes);
            if (target.Heroes != null) allHeroes.AddRange(target.Heroes);

            foreach (var hero in allHeroes)
            {
                if (hero == null || hero.IsDead) continue;

                // Calculate likelihood of being an informant
                float informantChance = 0.1f; // Base 10% chance

                // Personality traits affect likelihood
                var traits = hero.GetHeroTraits();
                if (traits.Honor > 0) informantChance += traits.Honor * 0.05f;
                if (traits.Calculating > 0) informantChance += traits.Calculating * 0.03f;

                // Relations with current ruler
                if (hero.Clan.Kingdom?.Leader != null)
                {
                    int relation = hero.GetRelation(hero.Clan.Kingdom.Leader);
                    if (relation > 20) informantChance += 0.15f;
                }

                // Low trust in alliance increases chance
                informantChance += (1f - alliance.TrustLevel) * 0.2f;

                if (MBRandom.RandomFloat < informantChance)
                {
                    informants.Add(hero);
                }
            }

            return informants;
        }

        private float CalculateLeakSeverity(SecretAllianceRecord alliance, Hero informant)
        {
            float severity = 0.3f; // Base severity

            if (informant != null)
            {
                // Higher tier heroes create more severe leaks
                if (informant.Clan?.Leader == informant) severity += 0.3f;

                // Hero's social skills affect leak impact
                severity += informant.GetSkillValue(DefaultSkills.Charm) * 0.002f;

                // Relation with faction leader affects credibility
                if (informant.Clan?.Kingdom?.Leader != null)
                {
                    int relation = informant.GetRelation(informant.Clan.Kingdom.Leader);
                    severity += MathF.Max(0f, relation * 0.01f);
                }
            }

            // Alliance strength affects how damaging the leak is
            severity += alliance.Strength * 0.4f;

            return MathF.Min(1f, severity);
        }

        private float CalculateInformerReliability(Hero informer)
        {
            if (informer == null) return 0.5f;

            float reliability = 0.5f;

            // Leader status increases reliability
            if (informer.Clan?.Leader == informer) reliability += 0.3f;

            // Skills affect reliability
            reliability += informer.GetSkillValue(DefaultSkills.Roguery) * 0.001f;
            reliability += informer.GetSkillValue(DefaultSkills.Charm) * 0.0015f;

            // Personality traits
            var traits = informer.GetHeroTraits();
            if (traits.Honor > 0) reliability += traits.Honor * 0.05f;
            if (traits.Calculating > 0) reliability += traits.Calculating * 0.03f;

            return MathF.Min(1f, reliability);
        }

        private void ProcessAllianceExposure(SecretAllianceRecord alliance, Hero informant)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Apply penalties for exposed alliance
            if (initiator?.Leader != null && initiator.Kingdom?.Leader != null)
            {

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(initiator.Leader, initiator.Kingdom.Leader, -25);
                ChangeClanInfluenceAction.Apply(initiator, -40f);
            }

            if (target?.Leader != null && target.Kingdom?.Leader != null)
            {
                // AFTER
                // AFTER
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(target.Leader, target.Kingdom.Leader, -15);
                ChangeClanInfluenceAction.Apply(target, -25f);
            }

            // Reward the informant
            if (informant?.Clan?.Kingdom?.Leader != null)
            {
                // AFTER
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(informant, informant.Clan.Kingdom.Leader, 10);
                ChangeClanInfluenceAction.Apply(informant.Clan, 15f);
            }

            alliance.IsActive = false;
        }

        private void ProcessIntelligence()
        {
            for (int i = _intelligence.Count - 1; i >= 0; i--)
            {
                var intel = _intelligence[i];
                intel.DaysOld++;

                // Intelligence becomes less reliable over time
                intel.ReliabilityScore *= 0.995f;

                // Remove very old or unreliable intelligence
                if (intel.DaysOld > 60 || intel.ReliabilityScore < 0.1f)
                {
                    _intelligence.RemoveAt(i);
                }
            }
        }

        private void ConsiderNewAlliances(Clan clan)
        {
            if (clan?.Leader == null || clan.IsEliminated) return;

            // Don't consider new alliances if already heavily involved
            var existingAlliances = _alliances.Count(a => a.IsActive &&
                (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id));

            if (existingAlliances >= 2) return;

            // Find potential targets
            var potentialTargets = Clan.All.Where(c =>
                c != clan &&
                !c.IsEliminated &&
                c.Leader != null &&
                !HasExistingAlliance(clan, c)).ToList();

            foreach (var target in potentialTargets.Take(3)) // Only consider top 3 candidates
            {
                float allianceDesirability = CalculateAllianceDesirability(clan, target);

                if (allianceDesirability > 0.6f && MBRandom.RandomFloat < allianceDesirability * 0.1f)
                {
                    // AI-initiated alliance
                    CreateAllianceAI(clan, target, allianceDesirability);
                    Debug.Print($"[Secret Alliances] AI-initiated alliance: {clan.Name} -> {target.Name} (desirability: {allianceDesirability:F2})");
                }
            }
        }

        private float CalculateAllianceDesirability(Clan initiator, Clan target)
        {
            float desirability = 0f;

            // Base desirability factors
            float mutualBenefit = CalculateMutualBenefit(initiator, target);
            float politicalPressure = CalculatePoliticalPressure(initiator, target);

            desirability += mutualBenefit * 0.4f;
            desirability += politicalPressure * 0.3f;

            // Relationship factor
            if (initiator.Leader != null && target.Leader != null)
            {
                int relation = initiator.Leader.GetRelation(target.Leader);
                desirability += MathF.Max(0f, (float)relation) * 0.01f;

                // Negative relations can still lead to alliances if desperate
                if (relation < -10 && politicalPressure > 0.5f)
                {
                    desirability += 0.1f; // Desperation alliance
                }
            }

            // Military strength considerations
            float combinedStrength = initiator.TotalStrength + target.TotalStrength;
            if (initiator.Kingdom != null)
            {
                float kingdomStrength = initiator.Kingdom.TotalStrength;
                if (combinedStrength > kingdomStrength * 0.4f) // Strong enough to matter
                {
                    desirability += 0.2f;
                }
            }

            // Economic factors
            if (target.Gold > initiator.Gold * 1.5f) // Target is wealthy
            {
                desirability += 0.15f;
            }

            // Common enemies
            if (HasCommonEnemies(initiator, target))
            {
                desirability += 0.2f;
            }

            // Geographic proximity
            float distance = GetClanDistance(initiator, target);
            if (distance < 100f)
            {
                desirability += 0.1f;
            }

            // Risk assessment - cautious clans are less likely
            if (initiator.Leader != null)
            {
                var traits = initiator.Leader.GetHeroTraits();
                if (traits.Valor > 0) desirability += traits.Valor * 0.02f;
                if (traits.Calculating > 0) desirability -= traits.Calculating * 0.01f; // Calculating leaders more cautious
            }

            return MathF.Min(1f, desirability);
        }

        private void CreateAllianceAI(Clan initiator, Clan target, float desirability)
        {
            float initialSecrecy = 0.7f + (MBRandom.RandomFloat * 0.2f); // 0.7-0.9
            float initialStrength = 0.05f + (desirability * 0.15f); // 0.05-0.2

            // Calculate potential bribe based on desperation and wealth
            float bribeAmount = 0f;
            if (desirability > 0.8f && initiator.Gold > 10000)
            {
                bribeAmount = MBRandom.RandomInt(1000, Math.Min(5000, initiator.Gold / 4));
            }

            CreateAlliance(initiator, target, initialSecrecy, initialStrength, bribeAmount);
            Debug.Print($"[Secret Alliances] AI alliance created: {initiator.Name} -> {target.Name}, strength: {initialStrength:F2}, secrecy: {initialSecrecy:F2}");
        }

        private bool HasExistingAlliance(Clan clan1, Clan clan2)
        {
            return _alliances.Any(a => a.IsActive &&
                ((a.InitiatorClanId == clan1.Id && a.TargetClanId == clan2.Id) ||
                 (a.InitiatorClanId == clan2.Id && a.TargetClanId == clan1.Id)));
        }

        private bool HasCommonEnemies(Clan clan1, Clan clan2)
        {
            if (clan1.Kingdom == null || clan2.Kingdom == null) return false;

            // Check if they're both at war with the same kingdoms
            var clan1Enemies = Kingdom.All.Where(k => clan1.Kingdom.IsAtWarWith(k));
            var clan2Enemies = Kingdom.All.Where(k => clan2.Kingdom.IsAtWarWith(k));

            return clan1Enemies.Intersect(clan2Enemies).Any();
        }

        private float CalculateDesperationLevel(Clan clan)
        {
            float desperation = 0f;

            // Economic desperation
            if (clan.Gold < 5000) desperation += 0.3f;
            if (clan.Gold < 2000) desperation += 0.2f;

            // Military desperation
            if (clan.TotalStrength < 100f) desperation += 0.2f;

            // Political desperation
            if (clan.Leader != null && clan.Kingdom != null)
            {

                float influence = clan.Influence;
                if (influence < 20f) desperation += 0.25f;
                if (influence < 10f) desperation += 0.15f;
            }

            // Territorial desperation
            if (clan.Settlements.Count == 0) desperation += 0.4f;
            else if (clan.Settlements.Count == 1) desperation += 0.2f;

            // War losses
            if (HasRecentMilitaryLosses(clan)) desperation += 0.3f;

            return MathF.Min(1f, desperation);
        }

        private bool HasRecentMilitaryLosses(Clan clan)
        {
            // Simple heuristic: if clan has very few parties compared to settlements
            // AFTER
            int partyCount = clan.WarPartyComponents?.Count ?? 0;
            int settlementCount = clan.Settlements?.Count ?? 0;

            return partyCount < settlementCount || clan.TotalStrength < 50f;
        }

        private Hero GetBestExecutor(Clan initiator, Clan target)
        {
            var candidates = new List<Hero>();

            if (initiator.Heroes != null)
                candidates.AddRange(initiator.Heroes.Where(h => !h.IsHumanPlayerCharacter && !h.IsDead));

            if (target.Heroes != null)
                candidates.AddRange(target.Heroes.Where(h => !h.IsHumanPlayerCharacter && !h.IsDead));

            if (!candidates.Any())
                return initiator.Leader;

            // Prefer heroes with high roguery or combat skills
            return candidates.OrderByDescending(h =>
                h.GetSkillValue(DefaultSkills.Roguery) +
                h.GetSkillValue(DefaultSkills.OneHanded) +
                h.GetSkillValue(DefaultSkills.TwoHanded)).FirstOrDefault();
        }

        private Kingdom FindBestKingdomForDefection(Clan initiator, Clan target)
        {
            var availableKingdoms = Kingdom.All.Where(k =>
                k != initiator.Kingdom &&
                k != target.Kingdom &&
                !k.IsEliminated).ToList();

            if (!availableKingdoms.Any()) return null;

            // Prefer kingdoms that:
            // 1. Are at war with current kingdom
            // 2. Are stronger
            // 3. Have good relations with initiator

            return availableKingdoms.OrderByDescending(k =>
            {
                float score = 0f;

                if (initiator.Kingdom != null && k.IsAtWarWith(initiator.Kingdom))
                    score += 100f;

                score += k.TotalStrength * 0.01f;

                if (initiator.Leader != null && k.Leader != null)
                    score += initiator.Leader.GetRelation(k.Leader);

                return score;
            }).FirstOrDefault();
        }

        private float CalculateCoupSuccessChance(SecretAllianceRecord alliance, Clan initiator, Clan target)
        {
            float baseChance = 0.3f;

            // Alliance factors
            baseChance *= alliance.Strength;
            baseChance *= (2f - alliance.Secrecy); // Lower secrecy can mean more support

            // Military strength
            float totalStrength = initiator.TotalStrength + target.TotalStrength;
            float kingdomStrength = initiator.Kingdom.TotalStrength;
            float strengthRatio = totalStrength / MathF.Max(1f, kingdomStrength);

            baseChance *= MathF.Min(2f, 0.5f + strengthRatio);

            // Leadership capabilities
            if (initiator.Leader != null)
            {
                var traits = initiator.Leader.GetHeroTraits();
                baseChance *= (1f + traits.Calculating * 0.1f);
                baseChance *= (1f + traits.Valor * 0.05f);

                // Leadership skill
                int leadership = initiator.Leader.GetSkillValue(DefaultSkills.Leadership);
                baseChance *= (1f + leadership * 0.002f);
            }

            // Current ruler's strength
            if (initiator.Kingdom.Leader != null)
            {
                int rulerRelation = 0;
                foreach (var clan in initiator.Kingdom.Clans)
                {
                    if (clan.Leader != null)
                        rulerRelation += clan.Leader.GetRelation(initiator.Kingdom.Leader);
                }

                float avgRelation = rulerRelation / MathF.Max(1f, (float)initiator.Kingdom.Clans.Count);
                if (avgRelation < 0) baseChance *= (1f - avgRelation * 0.01f);
            }

            return MathF.Min(0.8f, baseChance);
        }

        private void UpdateTrustLevel(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            float trustChange = 0f;

            // Successful operations build trust
            if (alliance.SuccessfulOperations > 0)
            {
                trustChange += alliance.SuccessfulOperations * 0.02f;
            }

            // Time builds trust if no betrayals
            if (alliance.DaysWithoutLeak > 10)
            {
                trustChange += 0.001f;
            }

            // Recent leaks damage trust
            if (alliance.LeakAttempts > 0)
            {
                trustChange -= alliance.LastLeakSeverity * 0.1f;
            }

            // Personal relations affect trust
            if (initiator?.Leader != null && target?.Leader != null)
            {
                int relation = initiator.Leader.GetRelation(target.Leader);
                trustChange += relation * 0.0005f;
            }

            alliance.TrustLevel = MathF.Max(0f, MathF.Min(1f, alliance.TrustLevel + trustChange));
        }

        // Event Handlers
        private void OnBattleStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            // Evaluate potential betrayals before battle
            EvaluatePreBattleBetrayals(mapEvent);
            // Battles affect alliance considerations
            var attackerClan = attackerParty?.LeaderHero?.Clan;
            var defenderClan = defenderParty?.LeaderHero?.Clan;

            if (attackerClan != null && defenderClan != null)
            {
                // Check if allies are fighting each other (potential betrayal)
                var alliance = FindAlliance(attackerClan, defenderClan);
                if (alliance != null && alliance.IsActive)
                {
                    // Fighting your secret ally damages the alliance
                    alliance.TrustLevel = MathF.Max(0f, alliance.TrustLevel - 0.3f);
                    alliance.Strength = MathF.Max(0f, alliance.Strength - 0.2f);

                    if (alliance.TrustLevel < 0.2f)
                    {
                        alliance.IsActive = false;
                    }
                }
            }
        }

        private void EvaluatePreBattleBetrayals(MapEvent mapEvent)
        {
            if (mapEvent == null) return;

            // Get all parties involved in the battle
            var attackerParties = mapEvent?.AttackerSide?.Parties != null
    ? mapEvent.AttackerSide.Parties.Select(p => p.Party).Where(p => p != null).ToList()
    : new List<PartyBase>();

            var defenderParties = mapEvent?.DefenderSide?.Parties != null
                ? mapEvent.DefenderSide.Parties.Select(p => p.Party).Where(p => p != null).ToList()
                : new List<PartyBase>();

            // Check each side for potential defectors
            EvaluateSideDefections(mapEvent, attackerParties, defenderParties);
            EvaluateSideDefections(mapEvent, defenderParties, attackerParties);
        }

        private void EvaluateSideDefections(MapEvent mapEvent, List<PartyBase> sidePaties, List<PartyBase> opposingParties)
        {
            foreach (var party in sidePaties)
            {
                var sideClan = party?.LeaderHero?.Clan;
                if (sideClan == null || sideClan.Leader == null) continue;

                foreach (var opposingParty in opposingParties)
                {
                    var opposingClan = opposingParty?.LeaderHero?.Clan;
                    if (opposingClan == null) continue;

                    // Check if there's a secret alliance between these clans
                    var alliance = FindAlliance(sideClan, opposingClan);
                    if (alliance != null && alliance.IsActive)
                    {
                        var relevantAlliances = new List<SecretAllianceRecord> { alliance };

                        // Also check for shared group alliances
                        var sideAlliances = GetAlliancesForClan(sideClan);
                        var opposingAlliances = GetAlliancesForClan(opposingClan);

                        var sharedGroupAlliances = sideAlliances.Where(a =>
                            opposingAlliances.Any(o => o.GroupId == a.GroupId && a.GroupId != 0)).ToList();

                        relevantAlliances.AddRange(sharedGroupAlliances);

                        if (EvaluateSideDefection(mapEvent, sideClan, opposingClan, relevantAlliances))
                        {
                            // Execute proper side switching - join the opposing clan's kingdom
                            if (sideClan.Kingdom != null && opposingClan.Kingdom != null && sideClan.Kingdom != opposingClan.Kingdom)
                            {
                                // First leave current kingdom peacefully (no rebellion to avoid becoming hostile)
                                ChangeKingdomAction.ApplyByLeaveKingdom(sideClan, false);

                                // Then join the opposing clan's kingdom as defection
                                ChangeKingdomAction.ApplyByJoinToKingdomByDefection(sideClan, opposingClan.Kingdom, true);

                                // Update alliance strength from successful coordination
                                foreach (var relevantAlliance in relevantAlliances)
                                {
                                    relevantAlliance.Strength += 0.1f; // Reward successful battle coordination
                                    relevantAlliance.Secrecy = MathF.Max(0f, relevantAlliance.Secrecy - 0.15f); // Some secrecy loss from public defection
                                    relevantAlliance.SuccessfulOperations++;
                                    relevantAlliance.MilitaryCoordination += 0.05f;
                                }

                                Debug.Print($"[Secret Alliances] Battle defection: {sideClan.Name} switched from {sideClan.Kingdom?.Name} to {opposingClan.Kingdom.Name} to help allied clan {opposingClan.Name}");
                            }
                            return; // Only one defection per evaluation
                        }
                    }
                }
            }
        }

        private static float ComputeMapEventSideStrength(MapEventSide side)
        {
            if (side == null || side.Parties == null || side.Parties.Count == 0)
                return 1f;

            float sum = 0f;
            for (int i = 0; i < side.Parties.Count; i++)
            {
                var mep = side.Parties[i];         // MapEventParty
                var party = mep?.Party;            // PartyBase
                if (party != null)
                    sum += MathF.Max(0f, party.TotalStrength);
            }
            // Avoid zero to prevent divide-by-zero elsewhere
            return MathF.Max(1f, sum);
        }

        private bool EvaluateSideDefection(MapEvent mapEvent, Clan sideClan, Clan opposingClan, List<SecretAllianceRecord> relevantAlliances)
        {
            float defectionProbability = 0.05f; // Base 5% chance
            var factors = new List<string> { "Base: 0.05" }; // Debug factor breakdown

            // Alliance factors
            foreach (var alliance in relevantAlliances)
            {
                // Alliance strength increases defection chance (bounded)
                float strengthBonus = MathF.Min(0.3f, alliance.Strength * 0.3f);
                defectionProbability += strengthBonus;
                factors.Add($"Strength: +{strengthBonus:F3}");

                // Trust level affects loyalty (bounded)
                float trustBonus = MathF.Min(0.2f, alliance.TrustLevel * 0.2f);
                defectionProbability += trustBonus;
                factors.Add($"Trust: +{trustBonus:F3}");

                // Military pact makes defection more likely
                if (alliance.MilitaryPact)
                {
                    defectionProbability += 0.15f;
                    factors.Add($"MilitaryPact: +0.15");
                }
                // Coalition strength bonus
                if (alliance.GroupStrengthCache > 0)
                {
                    float coalitionBonus = MathF.Min(0.1f, alliance.GroupStrengthCache * 0.1f);
                    defectionProbability += coalitionBonus;
                    factors.Add($"Coalition: +{coalitionBonus:F3}");
                }
            }

            // Political pressure increases defection likelihood
            float pressure = CalculatePoliticalPressure(sideClan, opposingClan);
            defectionProbability += pressure * 0.2f;

            // Desperation increases defection chance
            float desperation = CalculateDesperationLevel(sideClan);
            float pressureBonus = MathF.Min(0.2f, pressure * 0.2f);
            defectionProbability += pressureBonus;
            factors.Add($"Pressure: +{pressureBonus:F3}");


            // Relative battlefield power affects decision
            try
            {
                float sideStrength = ComputeMapEventSideStrength(mapEvent?.AttackerSide);
                float opposingStrength = ComputeMapEventSideStrength(mapEvent?.DefenderSide);

                // If the side they're considering switching to is much stronger
                if (opposingStrength > sideStrength * 1.5f)
                {
                    defectionProbability += 0.1f;
                    factors.Add($"PowerImbalance: +0.1");
                }
            }
            catch
            {
                // Fallback if battle strength calculation fails
                defectionProbability += 0.05f;
                factors.Add($"PowerFallback: +0.05");
            }

            // Cap the probability
            float originalProbability = defectionProbability;
            defectionProbability = MathF.Min(0.4f, defectionProbability);

            if (originalProbability != defectionProbability)
            {
                factors.Add($"Capped: {originalProbability:F3} -> {defectionProbability:F3}");
            }

            bool shouldDefect = MBRandom.RandomFloat < defectionProbability;

            // Enhanced debug logging with factor breakdown
            string factorBreakdown = string.Join(", ", factors);
            Debug.Print($"[Secret Alliances] {sideClan.Name} defection eval vs {opposingClan.Name}: {defectionProbability:F3} [{factorBreakdown}] -> {(shouldDefect ? "DEFECTING" : "STAYING")}");

            return shouldDefect;
        }


        private void OnBattleEnded(MapEvent mapEvent)
        {
            // Analyze battle outcomes for alliance implications
            if (mapEvent.WinningSide == BattleSideEnum.None) return;

            var winnerParties = mapEvent.WinningSide == BattleSideEnum.Attacker ?
                mapEvent.AttackerSide.Parties : mapEvent.DefenderSide.Parties;

            foreach (var party in winnerParties)
            {

                var clan = party.Party?.MobileParty?.LeaderHero?.Clan;
                if (clan != null)
                {
                    // Victories strengthen alliances involving this clan
                    var alliances = _alliances.Where(a => a.IsActive &&
                        (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id));

                    foreach (var alliance in alliances)
                    {
                        alliance.Strength += 0.01f;
                        alliance.SuccessfulOperations++;
                    }
                }
            }
        }

        public int GetAcceptanceScore(Clan proposer, Clan target)
        {
            if (proposer == null || target == null) return 0;

            int score = 50; // Base 50% chance

            // Relationship factor (most important)
            if (target.Leader != null && proposer.Leader != null)
            {
                int relation = target.Leader.GetRelation(proposer.Leader);
                score += relation / 2; // Each relation point = 0.5% acceptance
            }

            // Economic factors
            if (proposer.Gold > target.Gold * 1.5f)
            {
                score += 10; // Proposer is wealthy
            }
            else if (proposer.Gold < target.Gold * 0.5f)
            {
                score -= 10; // Proposer is poor
            }

            // Military strength comparison
            float strengthRatio = proposer.TotalStrength / MathF.Max(1f, target.TotalStrength);
            if (strengthRatio > 1.5f)
            {
                score += 15; // Proposer is much stronger
            }
            else if (strengthRatio < 0.5f)
            {
                score -= 10; // Proposer is much weaker
            }

            // Political situation
            if (proposer.Kingdom != null && target.Kingdom != null)
            {
                if (proposer.Kingdom.IsAtWarWith(target.Kingdom))
                {
                    score -= 30; // Much harder if at war
                }
                else if (proposer.Kingdom == target.Kingdom)
                {
                    score += 10; // Easier if same kingdom
                }
            }

            // Target clan's current situation (desperation)
            if (target.Gold < 2000)
            {
                score += 15; // Desperate for resources
            }

            if (target.TotalStrength < 100f)
            {
                score += 10; // Militarily weak, needs allies
            }

            // Leader personality traits
            if (target.Leader != null)
            {
                var traits = target.Leader.GetHeroTraits();
                score += traits.Calculating * 5; // Calculating leaders more likely to accept
                score -= traits.Honor * 3; // Honorable leaders less likely
                score += traits.Valor * 2; // Brave leaders more willing to take risks
            }

            // Proposer's reputation and skills
            if (proposer.Leader != null)
            {
                int leadership = proposer.Leader.GetSkillValue(DefaultSkills.Leadership);
                int charm = proposer.Leader.GetSkillValue(DefaultSkills.Charm);
                int roguery = proposer.Leader.GetSkillValue(DefaultSkills.Roguery);
                score += (leadership + charm + roguery) / 10; // Skills help persuasion
            }

            // Geographic factors
            float distance = GetClanDistance(proposer, target);
            if (distance < 100f)
            {
                score += 5; // Close clans easier to coordinate
            }

            // Political pressure (settlements under siege, low influence)
            if (target.Kingdom != null && target.Kingdom.Settlements.Any(s => s.IsUnderSiege))
            {
                score += 20; // Under pressure
            }

            if (target.Influence < 20f)
            {
                score += 10; // Low influence creates desperation
            }

            // Random factor for unpredictability (bounded)
            score += MBRandom.RandomInt(-10, 10);

            return MathF.Max(0, MathF.Min(100, score));
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            // Kingdom changes affect alliances
            var relevantAlliances = _alliances.Where(a => a.IsActive &&
                (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id)).ToList();

            foreach (var alliance in relevantAlliances)
            {
                var otherClan = alliance.InitiatorClanId == clan.Id ?
                    alliance.GetTargetClan() : alliance.GetInitiatorClan();

                if (otherClan?.Kingdom == newKingdom)
                {
                    // Both clans now in same kingdom - alliance succeeded
                    alliance.SuccessfulOperations++;
                    alliance.Strength += 0.1f;
                }
                else if (otherClan?.Kingdom != null && newKingdom != null &&
                         newKingdom.IsAtWarWith(otherClan.Kingdom))
                {
                    // Now enemies - alliance fails
                    alliance.IsActive = false;
                }
            }
        }

        private void OnKingdomDestroyed(Kingdom kingdom)
        {
            // Kingdom destruction affects related alliances
            var affectedAlliances = _alliances.Where(a => a.IsActive).ToList();

            foreach (var alliance in affectedAlliances)
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();

                if ((initiator?.Kingdom == kingdom) || (target?.Kingdom == kingdom))
                {
                    // Kingdom destruction changes alliance dynamics
                    alliance.PoliticalPressure = 1f; // Maximum pressure
                    alliance.Strength += 0.2f; // Desperation strengthens alliance
                }
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            // Hero deaths affect alliances
            var victimClan = victim.Clan;
            if (victimClan == null) return;

            var relevantAlliances = _alliances.Where(a => a.IsActive &&
                (a.InitiatorClanId == victimClan.Id || a.TargetClanId == victimClan.Id)).ToList();

            foreach (var alliance in relevantAlliances)
            {
                // Leader death significantly impacts alliance
                if (victim == victimClan.Leader)
                {
                    alliance.Strength *= 0.7f; // Major disruption
                    alliance.TrustLevel *= 0.8f;
                }
                else
                {
                    alliance.Strength *= 0.95f; // Minor impact
                }
            }
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            // War declarations create alliance opportunities
            if (faction1 is Kingdom k1 && faction2 is Kingdom k2)
            {
                // Clans in these kingdoms might seek secret alliances
                foreach (var clan in k1.Clans.Concat(k2.Clans))
                {
                    var alliances = _alliances.Where(a => a.IsActive &&
                        (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id));

                    foreach (var alliance in alliances)
                    {
                        alliance.PoliticalPressure += 0.2f;
                        alliance.HasCommonEnemies = true;
                    }
                }
            }
        }

        private void OnPeaceDeclared(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
        {
            // Peace reduces some alliance pressures
            if (faction1 is Kingdom k1 && faction2 is Kingdom k2)
            {
                foreach (var clan in k1.Clans.Concat(k2.Clans))
                {
                    var alliances = _alliances.Where(a => a.IsActive &&
                        (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id));

                    foreach (var alliance in alliances)
                    {
                        alliance.PoliticalPressure = MathF.Max(0f, alliance.PoliticalPressure - 0.15f);
                    }
                }
            }
        }

        // Advanced event handlers for new features
        private void OnHeroMarried(Hero hero, Hero spouse, bool _)
        {
            if (hero?.Clan == null || spouse?.Clan == null || hero.Clan == spouse.Clan) return;

            var alliance = FindAlliance(hero.Clan, spouse.Clan);
            if (alliance != null)
            {
                alliance.MarriageAlliance = true;
                alliance.Strength += 0.15f;
                alliance.TrustLevel += 0.1f;
                alliance.ReputationScore += 0.05f;
                Debug.Print($"[SecretAlliances] Marriage alliance formed between {hero.Clan.Name} and {spouse.Clan.Name}");
            }
            else if (Config.EnableAdvancedFeatures && MBRandom.RandomFloat < 0.3f)
            {
                CreateAlliance(hero.Clan, spouse.Clan, 0.85f, 0.2f);
                // Optional: you can mark it as a marriage alliance after creation if you keep a ref
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool _, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (newOwner?.Clan == null) return;

            var relevantAlliances = _alliances.Where(a => a.IsActive &&
                (a.InitiatorClanId == newOwner.Clan.Id || a.TargetClanId == newOwner.Clan.Id) &&
                a.TerritoryAgreementType > 0).ToList();

            foreach (var alliance in relevantAlliances)
            {
                var otherClan = alliance.InitiatorClanId == newOwner.Clan.Id
                    ? alliance.GetTargetClan()
                    : alliance.GetInitiatorClan();

                if (otherClan != null)
                {
                    if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege ||
                        detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion)
                    {
                        alliance.JointCampaignCount++;
                        alliance.MilitaryCoordination += 0.05f;
                    }

                    alliance.ReputationScore += 0.02f;
                }
            }
        }


        private void OnCaravanTransaction(MobileParty caravan, Town town, List<(EquipmentElement, int)> tradedItems)
        {
            var caravanClan = caravan?.LeaderHero?.Clan;
            var townOwnerClan = town?.Settlement?.OwnerClan;
            if (caravanClan == null || townOwnerClan == null) return;

            // Derive a transaction magnitude from item quantities (v1.2.9 does not provide gold directly here)
            int transactionMagnitude = tradedItems?.Sum(t => Math.Abs(t.Item2)) ?? 0;

            var transferRecord = new TradeTransferRecord
            {
                FromClan = caravanClan.Id,
                ToClan = townOwnerClan.Id,
                Day = CampaignTime.Now.GetDayOfYear,
                Amount = transactionMagnitude, // use magnitude as Amount
                TransferType = 1, // Goods
                IsCovert = false
            };

            if (!_recentTransfers.ContainsKey(caravanClan.Id))
                _recentTransfers[caravanClan.Id] = new List<TradeTransferRecord>();

            _recentTransfers[caravanClan.Id].Add(transferRecord);

            var alliance = FindAlliance(caravanClan, townOwnerClan);
            if (alliance?.TradePact == true)
            {
                alliance.EconomicIntegration += 0.001f;
                alliance.ReputationScore += 0.001f;
            }
        }



        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner?.Clan == null || capturer?.LeaderHero?.Clan == null) return;

            // Check if prisoner is from an allied clan
            var alliance = FindAlliance(capturer.LeaderHero.Clan, prisoner.Clan);
            if (alliance != null)
            {
                // Taking ally prisoner damages alliance
                alliance.TrustLevel -= 0.1f;
                alliance.ReputationScore -= 0.05f;
                if (alliance.DiplomaticImmunity)
                {
                    alliance.TrustLevel -= 0.2f; // Extra penalty for violating immunity
                }
            }

            // Check for spy network implications
            var spyData = _spyData.FirstOrDefault(s => s.EmbeddedAgents.Contains(prisoner.Id));
            if (spyData != null)
            {
                // Spy captured - damage to network
                spyData.EmbeddedAgents.Remove(prisoner.Id);
                spyData.CounterIntelDefense -= 10;
            }
        }

        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail)
        {
            if (prisoner?.Clan == null || party?.LeaderHero?.Clan == null) return;

            var alliance = FindAlliance(party.LeaderHero.Clan, prisoner.Clan);
            if (alliance != null && detail != EndCaptivityDetail.Ransom)
            {
                // Releasing ally without ransom improves alliance
                alliance.TrustLevel += 0.05f;
                alliance.ReputationScore += 0.03f;
            }
        }

        private void OnVillageRaided(Village village)
        {


        }



        private void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
        {
            if (raidEvent?.MapEvent?.AttackerSide?.LeaderParty?.LeaderHero?.Clan == null) return;

            var raiderClan = raidEvent.MapEvent.AttackerSide.LeaderParty.LeaderHero.Clan;
            var targetSettlement = raidEvent.MapEvent.MapEventSettlement;

            if (targetSettlement?.OwnerClan != null)
            {
                var targetClan = targetSettlement.OwnerClan;

                // If the raider attacked an allied clan, apply penalties
                var allianceWithTarget = FindAlliance(raiderClan, targetClan);
                if (allianceWithTarget != null)
                {
                    allianceWithTarget.TrustLevel -= 0.15f;
                    allianceWithTarget.Strength -= 0.1f;
                    allianceWithTarget.ReputationScore -= 0.1f;
                }

                // Your existing “successful raids vs common enemies improve coordination”
                var alliances = _alliances.Where(a => a.IsActive &&
                    (a.InitiatorClanId == raiderClan.Id || a.TargetClanId == raiderClan.Id) &&
                    a.HasCommonEnemies).ToList();

                foreach (var alliance in alliances)
                {
                    alliance.MilitaryCoordination += 0.02f;
                    alliance.SuccessfulOperations++;
                }
            }
        }

        // Public interface methods
        public void CreateAlliance(Clan initiator, Clan target, float initialSecrecy = 0.8f,
            float initialStrength = 0.1f, float bribe = 0f, int groupId = 0)
        {
            if (initiator == null || target == null || HasExistingAlliance(initiator, target))
                return;

            // Assign group ID - if not provided, create new group
            int allianceGroupId = groupId;
            if (allianceGroupId == 0)
            {
                allianceGroupId = _nextGroupId++;
            }


            var alliance = new SecretAllianceRecord
            {

                InitiatorClanId = initiator.Id,
                TargetClanId = target.Id,
                UniqueId = new MBGUID(),
                Secrecy = initialSecrecy,
                Strength = initialStrength,
                BribeAmount = bribe,
                IsActive = true,
                CreatedGameDay = CampaignTime.Now.GetDayOfYear,
                LastInteractionDay = CampaignTime.Now.GetDayOfYear,
                CooldownDays = 5,
                TradePact = false,
                MilitaryPact = false,
                TrustLevel = 0.5f,
                RiskTolerance = MBRandom.RandomFloat,
                EconomicIncentive = CalculateMutualBenefit(initiator, target),
                PoliticalPressure = CalculatePoliticalPressure(initiator, target),
                MilitaryAdvantage = (initiator.TotalStrength + target.TotalStrength) /
                                   MathF.Max(1f, initiator.Kingdom?.TotalStrength ?? 1f),
                HasCommonEnemies = HasCommonEnemies(initiator, target),
                GroupId = allianceGroupId,


            };

            _alliances.Add(alliance);
            Debug.Print($"[Secret Alliances] New alliance created: {initiator.Name} <-> {target.Name}, GroupId: {allianceGroupId}");
        }

        public bool TryRecruitClanToGroup(Clan recruiter, Clan candidate)
        {
            if (recruiter == null || candidate == null) return false;

            // Find recruiter's group ID
            var recruiterAlliance = GetAlliancesForClan(recruiter).FirstOrDefault();
            if (recruiterAlliance == null) return false;

            int groupId = recruiterAlliance.GroupId;

            // Check acceptance and desirability
            int acceptanceScore = GetAcceptanceScore(recruiter, candidate);
            if (acceptanceScore < 60) return false; // Lower threshold for group recruitment

            // Don't recruit if candidate already has alliances
            if (GetAlliancesForClan(candidate).Any()) return false;

            // Create alliance with the same group ID
            CreateAlliance(recruiter, candidate, groupId: groupId);

            Debug.Print($"[Secret Alliances] Clan {candidate.Name} recruited to coalition GroupId: {groupId}");
            return true;
        }



        public List<SecretAllianceRecord> GetActiveAlliances()
        {
            return _alliances.Where(a => a.IsActive).ToList();
        }

        public List<SecretAllianceRecord> GetAlliancesForClan(Clan clan)
        {
            if (clan == null) return new List<SecretAllianceRecord>();

            return _alliances.Where(a => a.IsActive &&
                (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id)).ToList();
        }

        public List<AllianceIntelligence> GetIntelligence()
        {
            return _intelligence.ToList();
        }

        public bool TrySetTradePact(Clan clanA, Clan clanB)
        {
            var alliance = FindAlliance(clanA, clanB);
            if (alliance == null || !alliance.IsActive || alliance.IsOnCooldown())
                return false;

            // Check if alliance is strong enough for trade pact
            if (alliance.Strength < 0.3f || alliance.Secrecy < 0.2f)
                return false;

            alliance.TradePact = true;
            alliance.LastInteractionDay = CampaignTime.Now.GetDayOfYear;
            alliance.CooldownDays = 5;
            alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + 0.05f);
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - 0.02f);

            // Generate intelligence about the trade pact
            GenerateTradePactIntelligence(alliance);

            Debug.Print($"[Secret Alliances] Trade pact established between {clanA.Name} and {clanB.Name}");
            return true;
        }

        public bool TrySetMilitaryPact(Clan clanA, Clan clanB)
        {
            var alliance = FindAlliance(clanA, clanB);
            if (alliance == null || !alliance.IsActive || alliance.IsOnCooldown())
                return false;

            // Military pacts require higher thresholds
            if (alliance.Strength < 0.5f || alliance.TrustLevel < 0.6f)
                return false;


            alliance.MilitaryPact = true;
            alliance.LastInteractionDay = CampaignTime.Now.GetDayOfYear;
            alliance.CooldownDays = 7;
            alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + 0.08f);
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - 0.05f);

            // Generate intelligence about the military pact
            GenerateMilitaryPactIntelligence(alliance);

            Debug.Print($"[Secret Alliances] Military pact established between {clanA.Name} and {clanB.Name}");

            return true;
        }

        public bool TryDissolveAlliance(Clan clanA, Clan clanB, bool blamePlayer)
        {
            var alliance = FindAlliance(clanA, clanB);
            if (alliance == null || !alliance.IsActive)
                return false;

            alliance.IsActive = false;
            alliance.LastInteractionDay = CampaignTime.Now.GetDayOfYear;

            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Leader != null && target?.Leader != null)
            {
                if (blamePlayer)
                {
                    // Player gets blamed more
                    if (Clan.PlayerClan == initiator)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(target.Leader, -15, true, false);
                        ChangeClanInfluenceAction.Apply(initiator, -20f);
                    }
                    else if (Clan.PlayerClan == target)
                    {
                        ChangeRelationAction.ApplyPlayerRelation(initiator.Leader, -15, true, false);
                        ChangeClanInfluenceAction.Apply(target, -20f);
                    }
                }
                else
                {
                    // Mutual smaller hit
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(initiator.Leader, target.Leader, -5);
                    ChangeClanInfluenceAction.Apply(initiator, -10f);
                    ChangeClanInfluenceAction.Apply(target, -10f);
                }
            }

            return true;
        }
        public bool TryGetRumorsForHero(Hero hero, out string rumorSummary)
        {
            rumorSummary = "";
            if (hero?.Clan == null) return false;

            // Check if this hero might know about alliances
            var relevantIntel = _intelligence.Where(i =>
                i.ReliabilityScore > 0.3f &&
                i.DaysOld < 45 &&
                (i.GetInformer()?.Clan == hero.Clan ||
                 i.ClanAId == hero.Clan.Id ||
                 i.ClanBId == hero.Clan.Id)).ToList();

            if (!relevantIntel.Any()) return false;

            var intel = relevantIntel.First();
            rumorSummary = $"There are whispers of secret dealings... (Reliability: {intel.ReliabilityScore:F1}, Age: {intel.DaysOld} days)";

            // Select the most relevant intelligence based on reliability and recency
            var Intel = relevantIntel.OrderByDescending(i => i.ReliabilityScore * (1f - i.DaysOld / 45f)).First();

            // Generate category-specific rumor text
            string rumorText = GetRumorTextByCategory(intel);
            rumorSummary = $"{rumorText} (Reliability: {intel.ReliabilityScore:F1}, Age: {intel.DaysOld} days)";

            Debug.Print($"[Secret Alliances] Rumors shared by {hero.Name}: {rumorSummary}");
            return true;
        }

        private string GetRumorTextByCategory(AllianceIntelligence intel)
        {
            switch ((AllianceIntelType)intel.IntelCategory)
            {
                case AllianceIntelType.TradePactEvidence:
                    return "I've heard whispers of secret trade arrangements between certain clans...";
                case AllianceIntelType.MilitaryCoordination:
                    return "There are rumors of clans coordinating their military movements in suspicious ways...";
                case AllianceIntelType.SecretMeeting:
                    return "Lords have been meeting in secret, away from prying eyes...";
                case AllianceIntelType.BetrayalPlot:
                    return "Dark whispers speak of treachery brewing among the noble houses...";
                default:
                    return "There are whispers of secret dealings among the nobles...";
            }
        }

        // Helper predicates for UI/dialogue integration
        public bool ShouldShowRumorOption(Hero hero)
        {
            if (hero?.Clan == null) return false;

            // Check if hero has any relevant intelligence or is part of alliances
            var hasIntel = _intelligence.Any(i =>
                i.ReliabilityScore > 0.3f &&
                i.DaysOld < 45 &&
                (i.GetInformer()?.Clan == hero.Clan ||
                 i.ClanAId == hero.Clan.Id ||
                 i.ClanBId == hero.Clan.Id));

            var hasAlliances = _alliances.Any(a => a.IsActive &&
                (a.InitiatorClanId == hero.Clan.Id || a.TargetClanId == hero.Clan.Id));

            return hasIntel || hasAlliances;
        }

        public bool CanOfferTradePact(Clan proposer, Clan target)
        {
            if (proposer == null || target == null) return false;

            var alliance = FindAlliance(proposer, target);
            if (alliance == null || !alliance.IsActive) return false;

            // Can offer trade pact if not already active and alliance is strong enough
            return !alliance.TradePact &&
                   alliance.Strength >= 0.3f &&
                   alliance.Secrecy >= 0.2f &&
                   !alliance.IsOnCooldown();
        }

        public bool CanOfferMilitaryPact(Clan proposer, Clan target)
        {
            if (proposer == null || target == null) return false;

            var alliance = FindAlliance(proposer, target);
            if (alliance == null || !alliance.IsActive) return false;

            // Military pact requires higher strength and trust
            return !alliance.MilitaryPact &&
                   alliance.Strength >= 0.5f &&
                   alliance.TrustLevel >= 0.6f &&
                   !alliance.IsOnCooldown();
        }

        // Coalition cohesion mechanics
        private void CalculateCoalitionCohesion()
        {
            // Group alliances by GroupId
            var coalitions = _alliances.Where(a => a.IsActive && a.GroupId > 0)
                                     .GroupBy(a => a.GroupId)
                                     .Where(g => g.Count() > 1); // Only process actual coalitions

            foreach (var coalition in coalitions)
            {
                var alliances = coalition.ToList();

                // Calculate average secrecy and strength for the group
                float avgSecrecy = alliances.Average(a => a.Secrecy);
                float avgStrength = alliances.Average(a => a.Strength);

                // Calculate cohesion based on variance (lower variance = higher cohesion)
                float secrecyVariance = alliances.Sum(a => MathF.Pow(a.Secrecy - avgSecrecy, 2)) / alliances.Count;
                float strengthVariance = alliances.Sum(a => MathF.Pow(a.Strength - avgStrength, 2)) / alliances.Count;

                float cohesion = MathF.Max(0f, 1f - (secrecyVariance + strengthVariance));

                // Update group caches for all alliances in the coalition
                foreach (var alliance in alliances)
                {
                    alliance.GroupSecrecyCache = avgSecrecy * (0.8f + cohesion * 0.2f); // Cohesion slightly boosts effective secrecy
                    alliance.GroupStrengthCache = avgStrength * (0.9f + cohesion * 0.1f); // Cohesion slightly boosts effective strength
                }

                Debug.Print($"[Secret Alliances] Coalition {coalition.Key}: Cohesion={cohesion:F2}, AvgSecrecy={avgSecrecy:F2}, AvgStrength={avgStrength:F2}");
            }
        }

        private void ProcessOperationsScaffolding()
        {
            // Scaffolding for future operations system
            var currentDay = CampaignTime.Now.GetDayOfYear;

            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                // Check if any operations are ready to complete
                if (alliance.PendingOperationType > 0 &&
                    currentDay >= alliance.LastOperationDay + 7) // Operations take 7 days
                {
                    ProcessOperation(alliance);
                }

                // Randomly consider new operations for strong alliances
                if (alliance.Strength >= 0.7f &&
                    alliance.PendingOperationType == 0 &&
                    MBRandom.RandomFloat < 0.05f) // 5% chance per day
                {
                    ConsiderNewOperation(alliance);
                }
            }
        }

        private void ProcessOperation(SecretAllianceRecord alliance)
        {
            // Placeholder for operations processing
            Debug.Print($"[Secret Alliances] Operation type {alliance.PendingOperationType} completed for alliance {alliance.InitiatorClanId}-{alliance.TargetClanId}");

            // Simple operation effects for now
            alliance.SuccessfulOperations++;
            alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + 0.05f);

            // Reset operation state
            alliance.PendingOperationType = 0;
            alliance.LastOperationDay = 0;
        }

        private void ConsiderNewOperation(SecretAllianceRecord alliance)
        {
            // Placeholder for operation consideration
            if (alliance.MilitaryPact && MBRandom.RandomFloat < 0.6f)
            {
                alliance.PendingOperationType = 1; // Military operation
            }
            else if (alliance.TradePact && MBRandom.RandomFloat < 0.4f)
            {
                alliance.PendingOperationType = 2; // Economic operation
            }
            else
            {
                alliance.PendingOperationType = 3; // Intelligence operation
            }

            alliance.LastOperationDay = CampaignTime.Now.GetDayOfYear;
            Debug.Print($"[Secret Alliances] New operation type {alliance.PendingOperationType} started for alliance {alliance.InitiatorClanId}-{alliance.TargetClanId}");
        }

        private void GenerateTradePactIntelligence(SecretAllianceRecord alliance)
        {
            // Generate intelligence when trade pacts are established
            if (MBRandom.RandomFloat < 0.3f) // 30% chance to generate intelligence
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();

                // Find potential informants from both clans
                var potentialInformants = GetPotentialInformants(initiator, target, alliance);

                if (potentialInformants.Any())
                {
                    var informant = potentialInformants[MBRandom.RandomInt(potentialInformants.Count)];

                    var intel = new AllianceIntelligence
                    {
                        AllianceId = alliance.UniqueId,
                        InformerHeroId = informant.Id,
                        ReliabilityScore = CalculateInformerReliability(informant) * 0.8f, // Trade pact intel is less reliable
                        DaysOld = 0,
                        IsConfirmed = false,
                        SeverityLevel = 0.4f, // Moderate severity
                        ClanAId = alliance.InitiatorClanId,
                        ClanBId = alliance.TargetClanId,
                        IntelCategory = (int)AllianceIntelType.TradePactEvidence
                    };

                    _intelligence.Add(intel);
                    Debug.Print($"[Secret Alliances] Trade pact intelligence generated by {informant.Name}");
                }
            }
        }

        private void GenerateMilitaryPactIntelligence(SecretAllianceRecord alliance)
        {
            // Generate intelligence when military pacts are established
            if (MBRandom.RandomFloat < 0.5f) // 50% chance to generate intelligence (higher than trade)
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();

                var potentialInformants = GetPotentialInformants(initiator, target, alliance);

                if (potentialInformants.Any())
                {
                    var informant = potentialInformants[MBRandom.RandomInt(potentialInformants.Count)];

                    var intel = new AllianceIntelligence
                    {
                        AllianceId = alliance.UniqueId,
                        InformerHeroId = informant.Id,
                        ReliabilityScore = CalculateInformerReliability(informant), // Military intel is more reliable
                        DaysOld = 0,
                        IsConfirmed = false,
                        SeverityLevel = 0.7f, // High severity
                        ClanAId = alliance.InitiatorClanId,
                        ClanBId = alliance.TargetClanId,
                        IntelCategory = (int)AllianceIntelType.MilitaryCoordination
                    };

                    _intelligence.Add(intel);
                    Debug.Print($"[Secret Alliances] Military pact intelligence generated by {informant.Name}");
                }
            }
        }

        #region Missing Core Methods

        // Alliance aging and decay
        private void ProcessAllianceAging(SecretAllianceRecord alliance)
        {
            int age = CampaignTime.Now.GetDayOfYear - alliance.CreatedGameDay;

            // Ancient alliances with poor stats may dissolve
            if (age > 540 && alliance.Strength < 0.3f && alliance.TrustLevel < 0.25f)
            {
                if (MBRandom.RandomFloat < 0.02f) // 2% chance daily
                {
                    alliance.IsActive = false;
                    Debug.Print($"[SecretAlliances] Alliance dissolved due to age and weakness: " +
                              $"{alliance.GetInitiatorClan()?.Name} <-> {alliance.GetTargetClan()?.Name}");
                }
            }
        }

        // Intelligence aging with reliability decay
        private void ProcessIntelligenceAging()
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;
            var toRemove = new List<AllianceIntelligence>();

            foreach (var intel in _intelligence)
            {
                intel.DaysOld++;

                // Reliability decay
                intel.ReliabilityScore = MathF.Max(0f, intel.ReliabilityScore - 0.005f);

                // Remove old or unreliable intelligence
                if (intel.DaysOld > 180 || intel.ReliabilityScore < 0.05f)
                {
                    toRemove.Add(intel);
                }
            }

            foreach (var intel in toRemove)
            {
                _intelligence.Remove(intel);
            }

            if (toRemove.Count > 0 && Config.DebugVerbose)
            {
                Debug.Print($"[SecretAlliances] Removed {toRemove.Count} aged intelligence records");
            }
        }

        // Coalition cohesion calculations


        // Forced reveal mechanics
        private void ProcessForcedRevealMechanics()
        {
            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                if (alliance.Strength > Config.ForcedRevealStrengthThreshold &&
                    alliance.Secrecy < Config.ForcedRevealSecrecyThreshold)
                {
                    float revealChance = (alliance.Strength - Config.ForcedRevealStrengthThreshold) *
                                        (Config.ForcedRevealSecrecyThreshold - alliance.Secrecy + 0.05f);

                    if (MBRandom.RandomFloat < revealChance)
                    {
                        ForceRevealAlliance(alliance);
                    }
                }
            }
        }

        // Betrayal evaluation system
        private void ProcessBetrayalEvaluations()
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;

            // Prevent spam by checking if we already evaluated today
            if (_lastEvaluationDay == currentDay) return;
            _lastEvaluationDay = currentDay;

            foreach (var alliance in _alliances.Where(a => a.IsActive && a.BetrayalCooldownDays <= 0))
            {
                EvaluateStrategicBetrayal(alliance);
            }
        }

        private void EvaluateStrategicBetrayal(SecretAllianceRecord alliance)
        {
            float baseChance = Config.BetrayalBaseChance;

            // Calculate betrayal factors
            float strengthFactor = 1.0f - alliance.Strength; // Weak alliances more likely to betray
            float trustFactor = 1.0f - alliance.TrustLevel;  // Low trust increases betrayal
            float pressureFactor = alliance.PoliticalPressure; // High pressure increases betrayal
            float desperationFactor = CalculateDesperationLevel(alliance.GetInitiatorClan());

            float betrayalChance = baseChance * (1f + strengthFactor + trustFactor + pressureFactor + desperationFactor);

            // Apply escalation counter
            betrayalChance += alliance.BetrayalEscalationCounter;

            // Debug logging
            if (Config.DebugVerbose || betrayalChance > 0.05f)
            {
                var factorJson = $"{{\"type\":\"StrategicBetrayal\",\"base\":{baseChance:F3}," +
                               $"\"strength\":{strengthFactor:F3},\"trust\":{trustFactor:F3}," +
                               $"\"pressure\":{pressureFactor:F3},\"desperation\":{desperationFactor:F3}," +
                               $"\"escalation\":{alliance.BetrayalEscalationCounter:F3},\"final\":{betrayalChance:F3}}}";
                Debug.Print($"[SecretAlliances] Betrayal evaluation: {factorJson}");
            }

            // Escalating near-miss system
            float nearMissThreshold = betrayalChance * 1.25f;
            float roll = MBRandom.RandomFloat;

            if (roll < betrayalChance)
            {
                // Betrayal succeeds
                ExecuteBetrayal(alliance);
            }
            else if (roll < nearMissThreshold)
            {
                // Near miss - escalate future attempts
                alliance.BetrayalEscalationCounter = MathF.Min(alliance.BetrayalEscalationCounter + 0.05f, 0.15f);
                Debug.Print($"[SecretAlliances] Near-miss betrayal, escalation increased to {alliance.BetrayalEscalationCounter:F3}");
            }

            // Set cooldown to prevent immediate re-evaluation
            alliance.BetrayalCooldownDays = 7;
        }

        private void ExecuteBetrayal(SecretAllianceRecord alliance)
        {
            alliance.BetrayalRevealed = true;
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - 0.4f); // Massive secrecy loss
            alliance.Strength = MathF.Max(0.1f, alliance.Strength - 0.3f); // Significant strength loss
            alliance.TrustLevel = MathF.Max(0f, alliance.TrustLevel - 0.5f); // Major trust loss

            // Optionally disable alliance
            if (alliance.Strength < 0.2f && alliance.TrustLevel < 0.1f)
            {
                alliance.IsActive = false;
            }

            Debug.Print($"[SecretAlliances] Betrayal executed: {alliance.GetInitiatorClan()?.Name} <-> {alliance.GetTargetClan()?.Name}");
        }

        // Data cleanup
        private void CleanupOldData()
        {
            // Clean up old trade transfer records
            int currentDay = CampaignTime.Now.GetDayOfYear;
            foreach (var kvp in _recentTransfers.ToList())
            {
                var transfers = kvp.Value.Where(t => currentDay - t.Day <= 30).ToList(); // Keep last 30 days
                if (transfers.Count == 0)
                {
                    _recentTransfers.Remove(kvp.Key);
                }
                else
                {
                    _recentTransfers[kvp.Key] = transfers.Take(20).ToList(); // Cap at 20 entries
                }
            }

            // Clean up operation cooldowns
            foreach (var kvp in _operationCooldowns.ToList())
            {
                var validCooldowns = kvp.Value.Where(c => c.Value > currentDay).ToDictionary(c => c.Key, c => c.Value);
                if (validCooldowns.Count == 0)
                {
                    _operationCooldowns.Remove(kvp.Key);
                }
                else
                {
                    _operationCooldowns[kvp.Key] = validCooldowns;
                }
            }
        }

        // Advanced feature processing methods
        private void ProcessAdvancedFeatures()
        {
            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                // Process military coordination improvements
                ProcessMilitaryCoordination(alliance);

                // Process economic network growth
                ProcessEconomicNetwork(alliance);

                // Process spy network development
                ProcessSpyNetwork(alliance);

                // Update reputation naturally over time
                UpdateReputationScore(alliance, Config.ReputationDecayRate * (alliance.TrustLevel - 0.5f));
            }
        }

        private void ProcessContractManagement()
        {
            var currentDay = CampaignTime.Now.GetDayOfYear;
            var expiredContracts = _contracts.Where(c => c.IsExpired()).ToList();

            foreach (var contract in expiredContracts)
            {
                if (contract.AutoRenew && contract.ViolationCount == 0)
                {
                    // Auto-renew successful contracts
                    contract.ExpirationDay = currentDay + Config.ContractMinDuration;
                    contract.ContractValue *= 0.9f; // Slight reduction for renewal
                }
                else
                {
                    // Remove expired contracts
                    _contracts.Remove(contract);
                    var alliance = GetAllianceById(contract.AllianceId);
                    if (alliance != null)
                    {
                        alliance.ReputationScore += 0.02f; // Small reputation gain for completion
                    }
                }
            }
        }

        private void ProcessReputationDecay()
        {
            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                // Natural reputation decay over time
                alliance.ReputationScore = MathF.Max(0f, alliance.ReputationScore - Config.ReputationDecayRate);
            }
        }

        private void ProcessAllianceRankProgression()
        {
            foreach (var alliance in _alliances.Where(a => a.IsActive && a.AllianceRank < Config.MaxAllianceRank))
            {
                // Check for automatic rank progression
                if (MBRandom.RandomFloat < 0.01f) // 1% chance per day for eligible alliances
                {
                    var clanA = alliance.GetInitiatorClan();
                    var clanB = alliance.GetTargetClan();
                    if (clanA != null && clanB != null)
                    {
                        TryUpgradeAlliance(clanA, clanB);
                    }
                }
            }
        }

        private void ProcessMilitaryCoordination(SecretAllianceRecord alliance)
        {
            if (!alliance.MilitaryPact) return;

            var militaryData = EnsureMilitaryData(alliance);
            var clanA = alliance.GetInitiatorClan();
            var clanB = alliance.GetTargetClan();

            if (clanA == null || clanB == null) return;

            // Improve coordination based on joint activities
            if (alliance.JointCampaignCount > militaryData.CoordinationLevel * 2)
            {
                militaryData.CoordinationLevel = Math.Min(5, militaryData.CoordinationLevel + 1);
                militaryData.CombatEfficiencyBonus = Math.Min(Config.MilitaryCoordinationMaxBonus,
                    militaryData.CombatEfficiencyBonus + 0.02f);
            }

            // Update alliance's military coordination field
            alliance.MilitaryCoordination = militaryData.CoordinationLevel / 5f;
        }

        private void ProcessEconomicNetwork(SecretAllianceRecord alliance)
        {
            if (!alliance.TradePact) return;

            var economicData = EnsureEconomicData(alliance);
            var clanA = alliance.GetInitiatorClan();
            var clanB = alliance.GetTargetClan();

            if (clanA == null || clanB == null) return;

            // Calculate trade volume between clans
            var tradesA = _recentTransfers.GetValueOrDefault(clanA.Id, new List<TradeTransferRecord>());
            var tradesB = _recentTransfers.GetValueOrDefault(clanB.Id, new List<TradeTransferRecord>());

            int mutualTrades = tradesA.Count(t => t.ToClan == clanB.Id) + tradesB.Count(t => t.ToClan == clanA.Id);

            if (mutualTrades > 0)
            {
                alliance.EconomicIntegration += 0.005f * mutualTrades;
                alliance.EconomicIntegration = Math.Min(1f, alliance.EconomicIntegration);

                economicData.TradeVolumeMultiplier = Math.Min(Config.TradeNetworkMaxMultiplier,
                    1f + (alliance.EconomicIntegration * 0.5f));

                // Increase economic warfare capability over time
                if (alliance.EconomicIntegration > 0.5f && economicData.EconomicWarfareCapability < 5)
                {
                    economicData.EconomicWarfareCapability++;
                }
            }
        }

        private void ProcessSpyNetwork(SecretAllianceRecord alliance)
        {
            var spyData = EnsureSpyData(alliance);

            // Spy networks develop over time with successful operations
            if (alliance.SuccessfulOperations > spyData.NetworkTier * 3 && spyData.NetworkTier < Config.MaxSpyNetworkTier)
            {
                spyData.NetworkTier++;
                spyData.InformationQuality += 0.1f;
                spyData.CounterIntelDefense += 5;

                Debug.Print($"[SecretAlliances] Spy network upgraded to tier {spyData.NetworkTier} for alliance {alliance.UniqueId}");
            }

            // Natural counter-intelligence decay
            spyData.CounterIntelDefense = Math.Max(5, spyData.CounterIntelDefense - 1);

            // Information quality degrades without maintenance
            if (CampaignTime.Now.GetDayOfYear - spyData.LastSuccessfulOperation > 30)
            {
                spyData.InformationQuality = Math.Max(0.1f, spyData.InformationQuality - 0.01f);
            }
        }

        // Enhanced leak detection with logistic probability
        private void CheckForLeaks(SecretAllianceRecord alliance, float multiplier = 1.0f)
        {
            float baseLeak = Config.LeakBaseChance * multiplier;

            // Pact bonuses
            if (alliance.TradePact) baseLeak *= 1.1f;
            if (alliance.MilitaryPact) baseLeak *= 1.15f;

            // Secrecy factor using logistic function
            float secrecyFactor = 1f / (1f + ExpF(10f * (alliance.Secrecy - 0.5f)));

            // Leak attempts increase chance
            float attemptsFactor = 1f + (alliance.LeakAttempts * 0.15f);

            // Counter-intelligence buff
            float counterIntelReduction = 1.0f;
            if (CampaignTime.Now.GetDayOfYear < alliance.CounterIntelBuffExpiryDay)
            {
                counterIntelReduction = 0.5f; // 50% reduction during buff
            }

            float finalLeakChance = baseLeak * secrecyFactor * attemptsFactor * counterIntelReduction;

            if (MBRandom.RandomFloat < finalLeakChance)
            {
                GenerateLeak(alliance);
            }
        }

        private void GenerateLeak(SecretAllianceRecord alliance)
        {
            alliance.LeakAttempts++;
            alliance.DaysWithoutLeak = 0;

            // Determine intelligence category
            AllianceIntelType category = AllianceIntelType.General;
            if (alliance.TradePact && alliance.MilitaryPact)
            {
                category = MBRandom.RandomFloat < 0.5f ? AllianceIntelType.Trade : AllianceIntelType.Military;
            }
            else if (alliance.TradePact)
            {
                category = AllianceIntelType.Trade;
            }
            else if (alliance.MilitaryPact)
            {
                category = AllianceIntelType.Military;
            }

            // Find informer
            var informer = GetRandomInformer(alliance);
            if (informer == null) return;

            // Calculate reliability and severity
            float reliability = CalculateInformerReliability(informer);
            float severity = CalculateLeakSeverity(alliance);

            var intel = new AllianceIntelligence
            {
                AllianceId = alliance.UniqueId,
                InformerHeroId = informer.Id,
                ReliabilityScore = reliability,
                DaysOld = 0,
                IsConfirmed = false,
                SeverityLevel = severity,
                ClanAId = alliance.InitiatorClanId,
                ClanBId = alliance.TargetClanId,
                IntelCategory = (int)category
            };

            _intelligence.Add(intel);
            alliance.LastLeakSeverity = severity;

            Debug.Print($"[SecretAlliances] Leak generated: {alliance.GetInitiatorClan()?.Name} <-> " +
                       $"{alliance.GetTargetClan()?.Name} (Severity: {severity:F2}, Category: {category})");
        }

        private float CalculateLeakSeverity(SecretAllianceRecord alliance)
        {
            float severity = alliance.Strength * 0.4f; // Stronger alliances = more severe leaks
            severity += (1f - alliance.Secrecy) * 0.3f; // Less secret = more severe

            if (alliance.BetrayalRevealed) severity += 0.2f;
            if (alliance.SuccessfulOperations > 5) severity += 0.1f;

            return MathF.Min(severity, 1.0f);
        }

        private Hero GetRandomInformer(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            var potentialInformers = new List<Hero>();

            if (initiator?.Heroes != null) potentialInformers.AddRange(initiator.Heroes);
            if (target?.Heroes != null) potentialInformers.AddRange(target.Heroes);

            return potentialInformers.Any() ? potentialInformers[MBRandom.RandomInt(potentialInformers.Count)] : null;
        }

        #endregion

        #region Public Methods for Console Commands and Dialog Integration

        // Public interface methods for console commands and dialog system
        public List<SecretAllianceRecord> GetAlliances()
        {
            return _alliances.ToList();
        }

        public SecretAllianceRecord FindAlliance(Clan clan1, Clan clan2)
        {
            return _alliances.FirstOrDefault(a => a.IsActive &&
                ((a.InitiatorClanId == clan1.Id && a.TargetClanId == clan2.Id) ||
                 (a.InitiatorClanId == clan2.Id && a.TargetClanId == clan1.Id)));
        }

        public void ForceLeakForTesting(SecretAllianceRecord alliance)
        {
            GenerateLeak(alliance);
        }

        public void ExecuteOperationForTesting(SecretAllianceRecord alliance, int operationType)
        {
            if (operationType >= 1 && operationType <= 5)
            {
                ExecuteOperation(alliance, (OperationType)operationType);
            }
        }

        public void ForceRevealAlliance(SecretAllianceRecord alliance)
        {
            alliance.BetrayalRevealed = true;

            // Generate high-severity intelligence for all clans in same kingdom
            var initiatorKingdom = alliance.GetInitiatorClan()?.Kingdom;
            var targetKingdom = alliance.GetTargetClan()?.Kingdom;

            var affectedClans = Clan.All.Where(c =>
                (c.Kingdom == initiatorKingdom || c.Kingdom == targetKingdom) &&
                c != alliance.GetInitiatorClan() &&
                c != alliance.GetTargetClan()).ToList();

            foreach (var clan in affectedClans.Take(5)) // Limit to prevent spam
            {
                var informer = clan.Heroes?.FirstOrDefault();
                if (informer == null) continue;

                var intel = new AllianceIntelligence
                {
                    AllianceId = alliance.UniqueId,
                    InformerHeroId = informer.Id,
                    ReliabilityScore = 0.7f, // Moderate reliability for forced reveal
                    DaysOld = 0,
                    IsConfirmed = true,
                    SeverityLevel = 0.9f, // Very high severity
                    ClanAId = alliance.InitiatorClanId,
                    ClanBId = alliance.TargetClanId,
                    IntelCategory = (int)AllianceIntelType.General
                };

                _intelligence.Add(intel);
            }

            Debug.Print($"[SecretAlliances] Alliance forcibly revealed to {affectedClans.Count} clans");
        }

        public void CreateTestAlliance(Clan clan1, Clan clan2)
        {
            CreateNewAlliance(clan1, clan2);
        }

        // Rumor system integration
        public bool ShouldShowRumorOption(Hero asker, Hero target)
        {
            if (asker?.Clan == null || target?.Clan == null) return false;

            var playerClan = Clan.PlayerClan;
            if (playerClan == null) return false;

            // Check if both sides share an active alliance with player clan
            bool askerConnected = FindAlliance(asker.Clan, playerClan) != null;
            bool targetConnected = FindAlliance(target.Clan, playerClan) != null;

            if (askerConnected && targetConnected) return true;

            // Check coalition groups
            var askerAlliance = _alliances.FirstOrDefault(a => a.IsActive &&
                (a.InitiatorClanId == asker.Clan.Id || a.TargetClanId == asker.Clan.Id));
            var targetAlliance = _alliances.FirstOrDefault(a => a.IsActive &&
                (a.InitiatorClanId == target.Clan.Id || a.TargetClanId == target.Clan.Id));
            var playerAlliance = _alliances.FirstOrDefault(a => a.IsActive &&
                (a.InitiatorClanId == playerClan.Id || a.TargetClanId == playerClan.Id));

            return askerAlliance?.GroupId > 0 && targetAlliance?.GroupId > 0 && playerAlliance?.GroupId > 0 &&
                   askerAlliance.GroupId == targetAlliance.GroupId &&
                   targetAlliance.GroupId == playerAlliance.GroupId;
        }

        public string GetTopRumorString(Hero hero)
        {
            var rumors = TryGetRumorsForHero(hero, Clan.PlayerClan, 1);
            return rumors.FirstOrDefault() ?? "";
        }

        public List<string> TryGetRumorsForHero(Hero hero, Clan playerClan, int max = 3)
        {
            if (hero?.Clan == null || playerClan == null) return new List<string>();

            // Filter relevant intelligence
            var relevantIntel = _intelligence.Where(i =>
                i.ReliabilityScore >= 0.25f &&
                i.SeverityLevel > 0f &&
                (i.ClanAId == playerClan.Id || i.ClanBId == playerClan.Id ||
                 i.ClanAId == hero.Clan.Id || i.ClanBId == hero.Clan.Id) &&
                IsIntelRelevantToPlayer(i, playerClan, hero.Clan)).ToList();

            // Calculate scores and rank
            var scoredIntel = relevantIntel.Select(i => new {
                Intel = i,
                Score = CalculateIntelScore(i)
            }).OrderByDescending(x => x.Score).Take(max).ToList();

            // Format as rumor strings
            return scoredIntel.Select(x => FormatRumorString(x.Intel)).ToList();
        }

        private bool IsIntelRelevantToPlayer(AllianceIntelligence intel, Clan playerClan, Clan heroClan)
        {
            // Check if player clan is involved
            if (intel.ClanAId != playerClan.Id && intel.ClanBId != playerClan.Id) return false;

            // Check if hero clan matches one side or shares coalition
            if (intel.ClanAId == heroClan.Id || intel.ClanBId == heroClan.Id) return true;

            // Check coalition group sharing
            var heroAlliance = _alliances.FirstOrDefault(a => a.IsActive &&
                (a.InitiatorClanId == heroClan.Id || a.TargetClanId == heroClan.Id));
            var intelAlliance = _alliances.FirstOrDefault(a => a.UniqueId == intel.AllianceId);

            return heroAlliance?.GroupId > 0 && intelAlliance?.GroupId > 0 &&
                   heroAlliance.GroupId == intelAlliance.GroupId;
        }

        private float CalculateIntelScore(AllianceIntelligence intel)
        {
            float reliability = intel.ReliabilityScore;
            float recency = MathF.Max(0.5f, 1f - intel.DaysOld * 0.01f);

            float categoryWeight;
            var category = (AllianceIntelType)intel.IntelCategory;
            switch (category)
            {
                case AllianceIntelType.Coup:
                    categoryWeight = 1.25f;
                    break;
                case AllianceIntelType.Military:
                    categoryWeight = 1.15f;
                    break;
                case AllianceIntelType.Financial:
                    categoryWeight = 1.1f;
                    break;
                case AllianceIntelType.Recruitment:
                    categoryWeight = 1.0f;
                    break;
                case AllianceIntelType.Trade:
                    categoryWeight = 0.95f;
                    break;
                default:
                    categoryWeight = 0.9f;
                    break;
            }

            return reliability * recency * categoryWeight;
        }

        private string FormatRumorString(AllianceIntelligence intel)
        {
            var clanA = MBObjectManager.Instance.GetObject<Clan>(c => c.Id == intel.ClanAId);
            var clanB = MBObjectManager.Instance.GetObject<Clan>(c => c.Id == intel.ClanBId);
            var informer = intel.GetInformer();

            string clanNames = (clanA?.Name != null && clanB?.Name != null)
                ? (clanA.Name.ToString() + " and " + clanB.Name.ToString())
                : "certain clans";
            string informerName = informer?.Name?.ToString() ?? "a contact";

            var category = (AllianceIntelType)intel.IntelCategory;
            string rumor;
            switch (category)
            {
                case AllianceIntelType.Military:
                    rumor = "I heard from " + informerName + " that " + clanNames + " have been coordinating military movements.";
                    break;
                case AllianceIntelType.Trade:
                    rumor = informerName + " mentioned seeing unusual trade activity between " + clanNames + ".";
                    break;
                case AllianceIntelType.Financial:
                    rumor = "There are rumors of gold changing hands between " + clanNames + ", according to " + informerName + ".";
                    break;
                case AllianceIntelType.Coup:
                    rumor = informerName + " whispers of plots brewing between " + clanNames + ".";
                    break;
                default:
                    rumor = informerName + " speaks of secretive meetings between " + clanNames + ".";
                    break;
            }

            return rumor;
        }

        #endregion

        #region Trade and Financial Dynamics


        private float CalculateWealthDisparity(Clan clan1, Clan clan2)
        {
            int gold1 = clan1.Gold;
            int gold2 = clan2.Gold;

            if (gold1 + gold2 == 0) return 0f;

            float disparity = MathF.Abs(gold1 - gold2) / (float)(gold1 + gold2);
            return disparity;
        }

        private static void TransferClanGold(Clan giver, Clan receiver, int amount)
        {
            if (giver == null || receiver == null || amount == 0) return;

            try
            {
                var t = typeof(Campaign).Assembly.GetType("TaleWorlds.CampaignSystem.Actions.GiveGoldAction");
                if (t != null)
                {
                    var methods = t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    foreach (var m in methods)
                    {
                        var ps = m.GetParameters();
                        // Find a static method with parameters (Clan, Clan, int, ...)
                        if (ps.Length >= 3 &&
                            ps[0].ParameterType == typeof(Clan) &&
                            ps[1].ParameterType == typeof(Clan) &&
                            ps[2].ParameterType == typeof(int))
                        {
                            var args = new object[ps.Length];
                            args[0] = giver;
                            args[1] = receiver;
                            args[2] = amount;
                            for (int i = 3; i < ps.Length; i++)
                            {
                                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                            }
                            m.Invoke(null, args);
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Ignore and fall through; we won't touch Clan.Gold directly.
            }

            // Do not assign Clan.Gold (read-only). If needed, log via your debug system.
            Debug.Print("[SecretAlliances] Clan->Clan gold transfer action not found in API; skipping transfer.");
        }

        private void ExecuteTradeTransfer(SecretAllianceRecord alliance, Clan giver, Clan receiver, int amount)
        {
            // Execute the transfer
            TransferClanGold(giver, receiver, amount);

            // Track transfer for magnitude percentile calculation
            TrackTradeTransfer(alliance, giver.Id, receiver.Id, amount);

            // Calculate transfer magnitude percentile
            float percentile = CalculateTransferMagnitudePercentile(alliance, amount);

            // Generate intelligence if significant transfer
            if (percentile > 0.6f)
            {
                GenerateFinancialIntelligence(alliance, amount, giver, receiver);
            }

            // Apply alliance effects based on transfer magnitude
            float trustGain = Math.Min(0.02f, amount / 50000f); // Diminishing returns
            alliance.TrustLevel = Math.Min(1f, alliance.TrustLevel + trustGain);

            // Secrecy penalty for large transfers
            float secrecyPenalty = percentile * 0.01f;
            alliance.Secrecy = Math.Max(0f, alliance.Secrecy - secrecyPenalty);

            if (Config.DebugVerbose)
            {
                Debug.Print($"[SecretAlliances] Trade transfer: {giver.Name} -> {receiver.Name} " +
                          $"({amount} gold, percentile: {percentile:F2})");
            }
        }

        private void TrackTradeTransfer(SecretAllianceRecord alliance, MBGUID giverClan, MBGUID receiverClan, int amount)
        {
            if (!_recentTransfers.ContainsKey(alliance.UniqueId))
            {
                _recentTransfers[alliance.UniqueId] = new List<TradeTransferRecord>();
            }

            var record = new TradeTransferRecord
            {
                Day = CampaignTime.Now.GetDayOfYear,
                Amount = amount,
                FromClan = giverClan,
                ToClan = receiverClan
            };

            _recentTransfers[alliance.UniqueId].Add(record);

            // Keep only last 20 transfers
            if (_recentTransfers[alliance.UniqueId].Count > 20)
            {
                _recentTransfers[alliance.UniqueId].RemoveAt(0);
            }
        }

        private float CalculateTransferMagnitudePercentile(SecretAllianceRecord alliance, int amount)
        {
            if (!_recentTransfers.ContainsKey(alliance.UniqueId) || _recentTransfers[alliance.UniqueId].Count < 2)
            {
                return 0.5f; // Default if no history
            }

            var transfers = _recentTransfers[alliance.UniqueId];
            var amounts = transfers.Select(t => t.Amount).OrderBy(a => a).ToList();

            int rank = amounts.Count(a => a < amount);
            return (float)rank / amounts.Count;
        }

        private void GenerateFinancialIntelligence(SecretAllianceRecord alliance, int amount, Clan giver, Clan receiver)
        {
            // Find potential informant from either clan
            var informer = GetRandomInformer(alliance);
            if (informer == null) return;

            float reliability = CalculateInformerReliability(informer) * 0.9f; // Financial intel slightly less reliable
            float severity = Math.Min(0.8f, amount / 100000f); // Scale with transfer size

            var intel = new AllianceIntelligence
            {
                AllianceId = alliance.UniqueId,
                InformerHeroId = informer.Id,
                ReliabilityScore = reliability,
                DaysOld = 0,
                IsConfirmed = false,
                SeverityLevel = severity,
                ClanAId = alliance.InitiatorClanId,
                ClanBId = alliance.TargetClanId,
                IntelCategory = (int)AllianceIntelType.Financial
            };

            _intelligence.Add(intel);

            Debug.Print($"[SecretAlliances] Financial intelligence generated: {giver.Name} -> {receiver.Name} " +
                       $"({amount} gold, severity: {severity:F2})");
        }

        #endregion

        #region Intelligence Generation Helpers

        private void CreateSpyIntelligence(SecretAllianceRecord alliance, Clan targetClan)
        {
            var informer = GetRandomInformer(alliance);
            if (informer == null) return;

            var intel = new AllianceIntelligence
            {
                AllianceId = alliance.UniqueId,
                InformerHeroId = informer.Id,
                ReliabilityScore = CalculateInformerReliability(informer) * 0.8f,
                DaysOld = 0,
                IsConfirmed = false,
                SeverityLevel = 0.6f,
                ClanAId = alliance.InitiatorClanId,
                ClanBId = targetClan.Id, // Target of spy operation
                IntelCategory = (int)AllianceIntelType.Military
            };

            _intelligence.Add(intel);
            Debug.Print($"[SecretAlliances] Spy intelligence created about {targetClan.Name}");
        }

        private void CreateSabotageIntelligence(SecretAllianceRecord alliance, Clan targetClan)
        {
            var informer = GetRandomInformer(alliance);
            if (informer == null) return;

            var intel = new AllianceIntelligence
            {
                AllianceId = alliance.UniqueId,
                InformerHeroId = informer.Id,
                ReliabilityScore = CalculateInformerReliability(informer),
                DaysOld = 0,
                IsConfirmed = false,
                SeverityLevel = 0.85f, // High severity for sabotage
                ClanAId = alliance.InitiatorClanId,
                ClanBId = targetClan.Id,
                IntelCategory = (int)AllianceIntelType.Military
            };

            _intelligence.Add(intel);
            Debug.Print($"[SecretAlliances] Sabotage intelligence created about operation against {targetClan.Name}");
        }

        #endregion

        #region Advanced Alliance Management

        // Alliance rank progression system
        public bool TryUpgradeAlliance(Clan clanA, Clan clanB)
        {
            var alliance = FindAlliance(clanA, clanB);
            if (alliance == null || alliance.AllianceRank >= Config.MaxAllianceRank) return false;

            // Requirements for rank upgrade
            if (alliance.Strength >= Config.AdvancedFeatureUnlockThreshold &&
                alliance.TrustLevel >= Config.AdvancedFeatureUnlockThreshold &&
                alliance.ReputationScore >= 0.6f)
            {
                alliance.AllianceRank++;
                alliance.ReputationScore += 0.1f;

                // Unlock new capabilities based on rank
                UnlockRankBasedFeatures(alliance);

                Debug.Print($"[SecretAlliances] Alliance upgraded to rank {alliance.AllianceRank}: {clanA.Name} <-> {clanB.Name}");
                return true;
            }
            return false;
        }

        private void UnlockRankBasedFeatures(SecretAllianceRecord alliance)
        {
            switch (alliance.AllianceRank)
            {
                case 1:
                    // Advanced rank: Enable elite unit exchange and basic diplomatic immunity
                    if (alliance.AllianceRank >= Config.EliteUnitExchangeMinRank)
                    {
                        EnsureMilitaryData(alliance).EliteUnitExchange = true;
                    }
                    break;
                case 2:
                    // Strategic rank: Enable fortress network and advanced coordination
                    alliance.DiplomaticImmunity = true;
                    if (alliance.AllianceRank >= Config.FortressNetworkMinRank)
                    {
                        EnsureMilitaryData(alliance).FortressNetworkAccess = 1;
                    }
                    EnsureSpyData(alliance).NetworkTier = Math.Min(3, EnsureSpyData(alliance).NetworkTier + 1);
                    break;
            }
        }

        // Alliance contract system
        public bool CreateContract(Clan clanA, Clan clanB, int contractType, int duration, float value)
        {
            var alliance = FindAlliance(clanA, clanB);
            if (alliance == null || _contracts.Count >= Config.MaxActiveContracts) return false;

            var contract = new AllianceContract
            {
                AllianceId = alliance.UniqueId,
                ContractType = contractType,
                ExpirationDay = CampaignTime.Now.GetDayOfYear + Math.Max(Config.ContractMinDuration, Math.Min(Config.ContractMaxDuration, duration)),
                ContractValue = value,
                AutoRenew = false,
                ViolationCount = 0,
                PenaltyClause = value * 0.5f,
                WitnessClans = new List<MBGUID>()
            };

            _contracts.Add(contract);
            Debug.Print($"[SecretAlliances] Contract created: Type {contractType}, Duration {duration} days, Value {value}");
            return true;
        }

        // Economic warfare system
        public void ExecuteEconomicWarfare(SecretAllianceRecord alliance, Clan targetClan)
        {
            if (!Config.EnableEconomicWarfare || alliance.EconomicIntegration < 0.3f) return;

            var economicData = EnsureEconomicData(alliance);
            if (economicData.EconomicWarfareCapability < 3) return;

            // Economic damage calculation
            float damageMultiplier = alliance.EconomicIntegration * Config.EconomicWarfareEffectiveness;

            // Reduce target clan's income
            if (targetClan.Gold > 1000)
            {
                int goldLoss = (int)(targetClan.Gold * damageMultiplier * 0.05f);
                goldLoss = Math.Min(goldLoss, 5000); // Cap the damage

                // Apply economic damage through reduced caravan efficiency
                foreach (var wpc in targetClan.WarPartyComponents)
                {
                    var mp = wpc.MobileParty;
                    if (mp?.IsCaravan == true)
                    {
                        mp.Ai.SetDoNotMakeNewDecisions(true);
                        // Temporary disruption - will reset on next AI update
                    }
                }

                Debug.Print($"[SecretAlliances] Economic warfare: {targetClan.Name} disrupted by {alliance.GetInitiatorClan()?.Name} <-> {alliance.GetTargetClan()?.Name}");
            }

            // Cooldown for economic warfare
            economicData.EconomicWarfareCapability = Math.Max(0, economicData.EconomicWarfareCapability - 1);
        }

        // Spy network operations
        public bool ExecuteSpyOperation(SecretAllianceRecord alliance, Clan targetClan, int operationType)
        {
            if (!Config.EnableSpyNetworks) return false;

            var spyData = EnsureSpyData(alliance);
            if (spyData.NetworkTier < 2) return false;

            float successChance = Config.CounterIntelSuccessRate * (spyData.NetworkTier / (float)Config.MaxSpyNetworkTier);

            // Target's counter-intelligence affects success
            var targetSpyData = _spyData.FirstOrDefault(s =>
                s.AllianceId != alliance.UniqueId &&
                (GetAllianceById(s.AllianceId)?.InitiatorClanId == targetClan.Id ||
                 GetAllianceById(s.AllianceId)?.TargetClanId == targetClan.Id));

            if (targetSpyData != null)
            {
                successChance *= (1f - (targetSpyData.CounterIntelDefense / 100f));
            }

            if (MBRandom.RandomFloat < successChance)
            {
                // Success - execute operation based on type
                switch (operationType)
                {
                    case 1: // Information gathering
                        CreateSpyIntelligence(alliance, targetClan);
                        break;
                    case 2: // Sabotage
                        CreateSabotageIntelligence(alliance, targetClan);
                        break;
                    case 3: // Double agent recruitment
                        if (spyData.NetworkTier >= Config.DoubleAgentMinTier)
                        {
                            spyData.DoubleAgentCapability = true;
                        }
                        break;
                }

                spyData.LastSuccessfulOperation = CampaignTime.Now.GetDayOfYear;
                spyData.InformationQuality += 0.05f;
                return true;
            }
            else
            {
                // Failed operation might be detected
                if (MBRandom.RandomFloat < Config.SpyNetworkDetectionChance)
                {
                    alliance.Secrecy -= 0.1f;
                    if (targetSpyData != null)
                    {
                        targetSpyData.CounterIntelDefense += 5;
                    }
                }
                return false;
            }
        }

        // Joint military campaign system
        public void InitiateJointCampaign(SecretAllianceRecord alliance, Settlement targetSettlement)
        {
            if (alliance.AllianceRank < 1 || alliance.MilitaryCoordination < 0.3f) return;

            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.WarPartyComponents?.Any() != true || target?.WarPartyComponents?.Any() != true) return;

            // Coordinate military parties
            var initiatorParty = initiator.WarPartyComponents.FirstOrDefault()?.MobileParty;
            var targetParty = target.WarPartyComponents.FirstOrDefault()?.MobileParty;

            if (initiatorParty != null && targetParty != null && targetSettlement != null)
            {
                // Set both parties to target the same settlement
                initiatorParty.Ai.SetMoveGoToSettlement(targetSettlement);
                targetParty.Ai.SetMoveGoToSettlement(targetSettlement);

                var militaryData = EnsureMilitaryData(alliance);
                militaryData.LastJointBattleDay = CampaignTime.Now.GetDayOfYear;
                alliance.JointCampaignCount++;
                alliance.MilitaryCoordination += 0.1f;

                Debug.Print($"[SecretAlliances] Joint campaign initiated against {targetSettlement.Name} by {initiator.Name} and {target.Name}");
            }
        }

        // Data management helpers
        private MilitaryCoordinationData EnsureMilitaryData(SecretAllianceRecord alliance)
        {
            var data = _militaryData.FirstOrDefault(m => m.AllianceId == alliance.UniqueId);
            if (data == null)
            {
                data = new MilitaryCoordinationData
                {
                    AllianceId = alliance.UniqueId,
                    CoordinationLevel = 1,
                    SharedFormations = new List<MBGUID>(),
                    CombatEfficiencyBonus = 0.05f,
                    EliteUnitExchange = false,
                    FortressNetworkAccess = 0
                };
                _militaryData.Add(data);
            }
            return data;
        }

        private EconomicNetworkData EnsureEconomicData(SecretAllianceRecord alliance)
        {
            var data = _economicData.FirstOrDefault(e => e.AllianceId == alliance.UniqueId);
            if (data == null)
            {
                data = new EconomicNetworkData
                {
                    AllianceId = alliance.UniqueId,
                    TradeVolumeMultiplier = 1.1f,
                    SharedRoutes = new List<MBGUID>(),
                    CaravanProtectionLevel = 1,
                    ResourceSharingRatio = 0.05f,
                    PriceManipulationAccess = false,
                    EconomicWarfareCapability = 2
                };
                _economicData.Add(data);
            }
            return data;
        }

        private SpyNetworkData EnsureSpyData(SecretAllianceRecord alliance)
        {
            var data = _spyData.FirstOrDefault(s => s.AllianceId == alliance.UniqueId);
            if (data == null)
            {
                data = new SpyNetworkData
                {
                    AllianceId = alliance.UniqueId,
                    NetworkTier = 1,
                    EmbeddedAgents = new List<MBGUID>(),
                    CounterIntelDefense = 10,
                    InformationQuality = 0.3f,
                    DoubleAgentCapability = false
                };
                _spyData.Add(data);
            }
            return data;
        }

        private SecretAllianceRecord GetAllianceById(MBGUID allianceId)
        {
            return _alliances.FirstOrDefault(a => a.UniqueId == allianceId);
        }

        // Reputation system
        public void UpdateReputationScore(SecretAllianceRecord alliance, float delta)
        {
            alliance.ReputationScore = MathF.Max(0f, MathF.Min(1f, alliance.ReputationScore + (delta * Config.ReputationGainMultiplier)));
        }

        // Performance optimization - cache system
        private void UpdatePerformanceCaches()
        {
            int currentDay = CampaignTime.Now.GetDayOfYear;
            if (currentDay == _lastCacheUpdateDay) return;

            _lastCacheUpdateDay = currentDay;
            _allianceInfluenceCache.Clear();
            _diplomaticImmunityCache.Clear();

            // Update influence cache
            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                var clanA = alliance.GetInitiatorClan();
                var clanB = alliance.GetTargetClan();

                if (clanA != null && clanB != null)
                {
                    float influence = alliance.Strength * alliance.TrustLevel * alliance.ReputationScore;
                    _allianceInfluenceCache[clanA.Id] = _allianceInfluenceCache.GetValueOrDefault(clanA.Id, 0f) + influence;
                    _allianceInfluenceCache[clanB.Id] = _allianceInfluenceCache.GetValueOrDefault(clanB.Id, 0f) + influence;

                    if (alliance.DiplomaticImmunity)
                    {
                        _diplomaticImmunityCache[clanA.Id] = _diplomaticImmunityCache.GetValueOrDefault(clanA.Id, 0) + 1;
                        _diplomaticImmunityCache[clanB.Id] = _diplomaticImmunityCache.GetValueOrDefault(clanB.Id, 0) + 1;
                    }
                }
            }
        }

        // Public accessor methods for advanced features
        public float GetAllianceInfluence(Clan clan)
        {
            UpdatePerformanceCaches();
            return _allianceInfluenceCache.GetValueOrDefault(clan.Id, 0f);
        }

        public bool HasDiplomaticImmunity(Clan clan)
        {
            UpdatePerformanceCaches();
            return _diplomaticImmunityCache.GetValueOrDefault(clan.Id, 0) > 0;
        }

        public List<AllianceContract> GetActiveContracts()
        {
            return _contracts.Where(c => c.IsValid()).ToList();
        }

        public List<MilitaryCoordinationData> GetMilitaryCoordinationData()
        {
            return _militaryData.ToList();
        }

        public List<EconomicNetworkData> GetEconomicNetworkData()
        {
            return _economicData.ToList();
        }

        public List<SpyNetworkData> GetSpyNetworkData()
        {
            return _spyData.ToList();
        }

        #endregion
    }
}
