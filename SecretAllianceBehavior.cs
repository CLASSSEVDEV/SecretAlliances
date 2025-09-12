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

namespace SecretAlliances
{
    public class SecretAllianceBehavior : CampaignBehaviorBase
    {
        private List<SecretAllianceRecord> _alliances = new List<SecretAllianceRecord>();
        private List<AllianceIntelligence> _intelligence = new List<AllianceIntelligence>();
        
        private int _nextGroupId = 1; // For coalition support

        // Constants for tuning
        private const float DAILY_SECRECY_DECAY = 0.002f;
        private const float DAILY_STRENGTH_GROWTH = 0.003f;
        private const float LEAK_BASE_CHANCE = 0.008f;
        private const float COUP_STRENGTH_THRESHOLD = 0.75f;
        private const float COUP_SECRECY_THRESHOLD = 0.35f;

        public override void RegisterEvents()
        {
            // Daily clan processing
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);

            // Battle events for alliance considerations
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnBattleStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnded);

            // Political events
            // AFTER
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);

            // Hero death affects alliances
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

            // Peace/war declarations affect alliance dynamics
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, OnPeaceDeclared);

            

        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_Alliances", ref _alliances);
            dataStore.SyncData("SecretAlliances_Intelligence", ref _intelligence);
            dataStore.SyncData("SecretAlliances_NextGroupId", ref _nextGroupId);
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan == null || clan.IsEliminated) return;

            // Process all alliances involving this clan
            var relevantAlliances = _alliances.Where(a =>
                a.IsActive &&
                (a.InitiatorClanId == clan.Id || a.TargetClanId == clan.Id)).ToList();

            foreach (var alliance in relevantAlliances)
            {
                EvaluateAlliance(alliance);

                // Process pact effects
                ProcessTradePactEffects(alliance);
                ProcessMilitaryPactEffects(alliance);

                // Age cooldown per alliance
                if (alliance.CooldownDays > 0)
                {
                    alliance.CooldownDays--;
                }
            }

            // Check for new alliance opportunities (AI consideration)
            if (MBRandom.RandomFloat < 0.1f)
            {
                ConsiderNewAlliances(clan);
            }

            // Process intelligence aging daily
            ProcessIntelligence();

            // Process coalition cohesion (once per day, triggered by first clan)
            if (clan == Clan.All.FirstOrDefault())
            {
                CalculateCoalitionCohesion();
                ProcessOperationsScaffolding();
            }
        }

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

            // Decrement cooldowns (Former PR 4 requirement)
            if (alliance.DefectionCooldownDays > 0)
            {
                alliance.DefectionCooldownDays--;
            }
        }

        private void UpdateAllianceSecrecy(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Base decay
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - DAILY_SECRECY_DECAY);

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
            float strengthGain = DAILY_STRENGTH_GROWTH;

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
            float leakChance = LEAK_BASE_CHANCE * (1f - alliance.Secrecy);

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
                    IntelCategory = (int)AllianceIntelType.GeneralRumor

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
                alliance.Strength < COUP_STRENGTH_THRESHOLD ||
                alliance.Secrecy > COUP_SECRECY_THRESHOLD)
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

            var config = SecretAlliancesConfig.Instance;
            int initiatorGold = initiator.Leader.Gold;
            int targetGold = target.Leader.Gold;

            // Adaptive Trade & Financial Intelligence (Former PR 2) - disparity-driven economic flow
            float wealthDisparity = MathF.Abs(initiatorGold - targetGold) / (float)MathF.Max(initiatorGold + targetGold, 1);
            
            // Only transfer if disparity exceeds threshold and both clans meet reserve floor
            if (wealthDisparity < config.WealthDisparityThreshold) return;

            int richerGold = MathF.Max(initiatorGold, targetGold);
            int poorerGold = MathF.Min(initiatorGold, targetGold);
            int reserveFloor = (int)(richerGold * config.ReserveFloor);
            
            if (richerGold < reserveFloor * 2) return; // Not enough to transfer safely

            // Calculate transfer with volatility band
            float baseTransferRate = wealthDisparity * alliance.EconomicIncentive * alliance.TrustLevel;
            float volatilityAdjustment = MBRandom.RandomFloat * config.VolatilityBand - (config.VolatilityBand / 2f);
            float adjustedRate = MathF.Max(0.01f, MathF.Min(0.1f, baseTransferRate + volatilityAdjustment));

            int transferAmount = (int)(richerGold * adjustedRate);
            
            // Anti-exploit throttle mechanism
            var currentDay = CampaignTime.Now.GetDayOfYear;
            bool isHighMagnitude = (transferAmount / (float)richerGold) > 0.05f; // 5% threshold
            
            if (isHighMagnitude)
            {
                // Check anti-exploit limits
                if (currentDay - alliance.LastHighTransferDay < config.AntiExploitDayWindow)
                {
                    alliance.HighMagnitudeTransferCount++;
                    if (alliance.HighMagnitudeTransferCount >= config.AntiExploitTransferLimit)
                    {
                        Debug.Print($"[Secret Alliances] Trade transfer throttled for alliance {alliance.InitiatorClanId}-{alliance.TargetClanId}");
                        return; // Throttle
                    }
                }
                else
                {
                    alliance.HighMagnitudeTransferCount = 1; // Reset counter for new window
                }
                alliance.LastHighTransferDay = currentDay;
            }

            // Transfer from richer to poorer clan
            Hero richer = initiatorGold > targetGold ? initiator.Leader : target.Leader;
            Hero poorer = initiatorGold > targetGold ? target.Leader : initiator.Leader;

            if (richer.Gold >= transferAmount)
            {
                GiveGoldAction.ApplyBetweenCharacters(richer, poorer, transferAmount, false);

                // Generate Financial intel entries with severity proportional to transfer impact
                float transferImpact = transferAmount / (float)richerGold;
                float intelSeverity = MathF.Min(1.0f, transferImpact * 10f); // Scale up for visibility

                if (intelSeverity > 0.1f) // Only generate intel for significant transfers
                {
                    GenerateFinancialIntel(alliance, richer, transferAmount, intelSeverity);
                }

                // Secrecy erosion & trust increment scaled by transaction magnitude percentile
                float magnitudePercentile = CalculateTransferMagnitudePercentile(transferAmount, alliance);
                float secrecyLoss = MathF.Min(0.01f, 0.001f + (magnitudePercentile * 0.005f));
                float trustGain = MathF.Min(0.01f, magnitudePercentile * 0.003f);

                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);
                alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + trustGain);

                Debug.Print($"[Secret Alliances] Disparity-driven transfer: {richer.Name} -> {poorer.Name}: {transferAmount} denars (Impact: {transferImpact:F3}, Intel: {intelSeverity:F2})");
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
                            // Execute defection (leave with rebellion to simulate switching sides)
                            if (sideClan.Kingdom != null)
                            {
                                ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(sideClan, false);

                                // Apply consequences
                                foreach (var relevantAlliance in relevantAlliances)
                                {
                                    relevantAlliance.Secrecy = MathF.Max(0f, relevantAlliance.Secrecy - 0.3f);
                                    relevantAlliance.TrustLevel = MathF.Max(0f, relevantAlliance.TrustLevel - 0.2f);
                                    relevantAlliance.BetrayalRevealed = true;
                                }

                                Debug.Print($"[Secret Alliances] Pre-battle defection: {sideClan.Name} switched sides due to secret alliance with {opposingClan.Name}");
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
            var config = SecretAlliancesConfig.Instance;
            
            // Check for defection cooldown (Former PR 4 requirement)
            var recentDefections = relevantAlliances.Where(a => a.DefectionCooldownDays > 0);
            if (recentDefections.Any())
            {
                Debug.Print($"[Secret Alliances] {sideClan.Name} defection blocked due to cooldown ({recentDefections.First().DefectionCooldownDays} days remaining)");
                return false;
            }

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

                // Betrayal risk escalator (Former PR 4) - repeated near-miss attempts increase probability
                if (alliance.BetrayalRiskEscalator > 0)
                {
                    float escalatorBonus = MathF.Min(config.BetrayalEscalatorCap, alliance.BetrayalRiskEscalator * config.BetrayalEscalatorIncrement);
                    defectionProbability += escalatorBonus;
                    factors.Add($"BetrayalEscalator: +{escalatorBonus:F3}");
                }
            }

            // Political pressure increases defection likelihood
            float pressure = CalculatePoliticalPressure(sideClan, opposingClan);
            float pressureBonus = MathF.Min(0.2f, pressure * 0.2f);
            defectionProbability += pressureBonus;
            factors.Add($"Pressure: +{pressureBonus:F3}");

            // Desperation increases defection chance
            float desperation = CalculateDesperationLevel(sideClan);
            float desperationBonus = MathF.Min(0.15f, desperation * 0.15f);
            defectionProbability += desperationBonus;
            factors.Add($"Desperation: +{desperationBonus:F3}");

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

            // Betrayal risk escalation logic (Former PR 4)
            if (!shouldDefect && defectionProbability > config.BetrayalNotificationThreshold)
            {
                // Near-miss betrayal - escalate risk for next time
                foreach (var alliance in relevantAlliances)
                {
                    alliance.BetrayalRiskEscalator = MathF.Min(config.BetrayalEscalatorCap / config.BetrayalEscalatorIncrement, 
                                                              alliance.BetrayalRiskEscalator + 1);
                }

                // Player notification if involved faction
                if (sideClan == Clan.PlayerClan || opposingClan == Clan.PlayerClan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Unease in the ranks of {sideClan.Name}... loyalty wavers.", Colors.Yellow));
                }
                
                Debug.Print($"[Secret Alliances] Near-miss betrayal escalated for {sideClan.Name} (probability was {defectionProbability:F3})");
            }

            // If defection occurs, apply cooldown
            if (shouldDefect)
            {
                foreach (var alliance in relevantAlliances)
                {
                    alliance.DefectionCooldownDays = config.DefectionCooldownDays;
                    alliance.BetrayalRiskEscalator = 0; // Reset escalator after successful defection
                }
            }

            // Distinguish defection types in logging (Former PR 4 requirement)
            string defectionType = mapEvent != null ? "PreBattleDefection" : "StrategicDefection";
            string factorBreakdown = string.Join(", ", factors);
            Debug.Print($"[Secret Alliances] {defectionType}: {sideClan.Name} vs {opposingClan.Name}: {defectionProbability:F3} [{factorBreakdown}] -> {(shouldDefect ? "DEFECTING" : "STAYING")}");

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

        public SecretAllianceRecord FindAlliance(Clan clan1, Clan clan2)
        {
            if (clan1 == null || clan2 == null) return null;

            return _alliances.FirstOrDefault(a => a.IsActive &&
                ((a.InitiatorClanId == clan1.Id && a.TargetClanId == clan2.Id) ||
                 (a.InitiatorClanId == clan2.Id && a.TargetClanId == clan1.Id)));
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

        // Coalition cohesion mechanics (Enhanced Former PR 3)
        private void CalculateCoalitionCohesion()
        {
            var config = SecretAlliancesConfig.Instance;
            
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

                // Daily cohesion metric: Cohesion = 0.6 * avgStrength + 0.4 * avgSecrecy
                float cohesion = config.CohesionStrengthWeight * avgStrength + config.CohesionSecrecyWeight * avgSecrecy;

                // Update group caches for all alliances in the coalition
                foreach (var alliance in alliances)
                {
                    alliance.GroupSecrecyCache = avgSecrecy * (0.8f + cohesion * 0.2f); // Cohesion slightly boosts effective secrecy
                    alliance.GroupStrengthCache = avgStrength * (0.9f + cohesion * 0.1f); // Cohesion slightly boosts effective strength

                    // Apply cohesion buffs and controlled secrecy decay for low cohesion
                    if (cohesion < config.LowCohesionThreshold)
                    {
                        // Low cohesion penalty
                        alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - config.CohesionSecrecyDecay);
                        Debug.Print($"[Secret Alliances] Low cohesion penalty applied to GroupId {alliance.GroupId}");
                    }
                    else
                    {
                        // High cohesion strength buff
                        alliance.Strength = MathF.Min(1f, alliance.Strength + config.CohesionStrengthBuff * cohesion);
                    }
                }

                Debug.Print($"[Secret Alliances] Coalition {coalition.Key}: Cohesion={cohesion:F2}, AvgSecrecy={avgSecrecy:F2}, AvgStrength={avgStrength:F2}");
            }

            // Coalition recruitment AI
            ProcessCoalitionRecruitment();
        }

        private void ProcessCoalitionRecruitment()
        {
            var config = SecretAlliancesConfig.Instance;
            
            // Find existing coalitions that might want to recruit new members
            var coalitions = _alliances.Where(a => a.IsActive && a.GroupId > 0)
                                     .GroupBy(a => a.GroupId)
                                     .Where(g => g.Count() > 1);

            foreach (var coalition in coalitions)
            {
                if (MBRandom.RandomFloat > 0.1f) continue; // 10% chance per day per coalition

                var alliances = coalition.ToList();
                var coalitionClans = alliances.SelectMany(a => new[] { a.GetInitiatorClan(), a.GetTargetClan() })
                                              .Where(c => c != null).Distinct().ToList();

                // Look for potential recruitment targets
                var potentialTargets = Clan.All.Where(c => 
                    !c.IsEliminated && 
                    c.Leader != null &&
                    !coalitionClans.Contains(c) &&
                    !_alliances.Any(a => a.IsActive && 
                        (a.InitiatorClanId == c.Id || a.TargetClanId == c.Id) &&
                        coalitionClans.Any(cc => cc.Id == a.InitiatorClanId || cc.Id == a.TargetClanId)))
                    .ToList();

                if (!potentialTargets.Any()) continue;

                // Calculate recruitment desirability based on desperation and power complementarity
                var scoredTargets = potentialTargets.Select(target => new 
                {
                    Clan = target,
                    DesperationScore = CalculateDesperationLevel(target),
                    PowerScore = CalculatePowerComplementarity(target, coalitionClans),
                    CombinedScore = CalculateDesperationLevel(target) + CalculatePowerComplementarity(target, coalitionClans)
                })
                .Where(t => t.CombinedScore > 0.3f) // Only consider if reasonably attractive
                .OrderByDescending(t => t.CombinedScore)
                .Take(3);

                foreach (var candidate in scoredTargets)
                {
                    if (MBRandom.RandomFloat < candidate.CombinedScore * 0.3f) // Success chance based on desirability
                    {
                        // Attempt to recruit by creating alliance with an existing coalition member
                        var recruiter = coalitionClans[MBRandom.RandomInt(coalitionClans.Count)];
                        
                        Debug.Print($"[Secret Alliances] Coalition recruitment: {recruiter.Name} recruiting {candidate.Clan.Name} to GroupId {coalition.Key}");
                        
                        CreateAlliance(recruiter, candidate.Clan, 0.7f, 0.2f, 0f, coalition.Key);
                        
                        // Generate recruitment intelligence
                        if (MBRandom.RandomFloat < 0.4f)
                        {
                            GenerateRecruitmentIntel(recruiter, candidate.Clan, coalition.Key);
                        }
                        break; // Only recruit one per cycle
                    }
                }
            }
        }

        private float CalculatePowerComplementarity(Clan candidate, List<Clan> coalitionClans)
        {
            if (!coalitionClans.Any()) return 0f;

            float candidatePower = candidate.TotalStrength;
            float avgCoalitionPower = coalitionClans.Average(c => c.TotalStrength);
            
            // Higher score if the candidate fills a power gap or adds significant strength
            float powerRatio = candidatePower / MathF.Max(1f, avgCoalitionPower);
            
            if (powerRatio > 0.8f && powerRatio < 1.5f) return 0.6f; // Good match
            if (powerRatio > 1.5f) return 0.8f; // Very strong addition
            if (powerRatio < 0.5f) return 0.2f; // Weak but might be desperate
            
            return 0.4f; // Moderate match
        }

        private void GenerateRecruitmentIntel(Clan recruiter, Clan target, int groupId)
        {
            // Find potential informants in both clans
            var potentialInformants = new List<Hero>();
            
            if (recruiter.Heroes != null)
                potentialInformants.AddRange(recruiter.Heroes.Where(h => !h.IsDead));
            if (target.Heroes != null)
                potentialInformants.AddRange(target.Heroes.Where(h => !h.IsDead));

            if (potentialInformants.Any())
            {
                var informant = potentialInformants[MBRandom.RandomInt(potentialInformants.Count)];

                var intel = new AllianceIntelligence
                {
                    AllianceId = new MBGUID(), // Generate new ID for recruitment intel
                    InformerHeroId = informant.Id,
                    ReliabilityScore = CalculateInformerReliability(informant) * 0.8f,
                    DaysOld = 0,
                    IsConfirmed = false,
                    SeverityLevel = 0.5f,
                    ClanAId = recruiter.Id,
                    ClanBId = target.Id,
                    IntelCategory = (int)AllianceIntelType.Recruitment
                };

                _intelligence.Add(intel);
                Debug.Print($"[Secret Alliances] Recruitment intelligence generated by {informant.Name} for GroupId {groupId}");
            }
        }

        private void ProcessOperationsScaffolding()
        {
            // Full Operations Framework (Former PR 5)
            var config = SecretAlliancesConfig.Instance;
            var currentDay = CampaignTime.Now.GetDayOfYear;

            foreach (var alliance in _alliances.Where(a => a.IsActive))
            {
                // Check if any operations are ready to complete
                if (alliance.PendingOperationType > 0 &&
                    currentDay >= alliance.LastOperationDay + config.OperationBaseDuration)
                {
                    ProcessOperation(alliance);
                }

                // Adaptive scheduler - check for new operations more frequently under pressure
                bool shouldSchedule = false;
                if (alliance.PoliticalPressure > config.OperationSchedulePoliticalThreshold ||
                    alliance.TrustLevel > config.OperationScheduleTrustThreshold)
                {
                    shouldSchedule = MBRandom.RandomFloat < 0.15f; // 15% chance when under pressure
                }
                else
                {
                    shouldSchedule = MBRandom.RandomFloat < 0.05f; // Normal 5% chance
                }

                // Consider new operations for active alliances
                if (alliance.Strength >= 0.3f &&
                    alliance.PendingOperationType == 0 &&
                    !IsOperationOnCooldown(alliance) &&
                    shouldSchedule)
                {
                    ConsiderNewOperation(alliance);
                }

                // Decrement operation cooldowns
                DecrementOperationCooldowns(alliance);
            }
        }

        private void ProcessOperation(SecretAllianceRecord alliance)
        {
            var config = SecretAlliancesConfig.Instance;
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator == null || target == null) return;

            var operationType = (PendingOperationType)alliance.PendingOperationType;
            
            // Calculate operation success based on risk model
            float successChance = CalculateOperationSuccessChance(alliance, operationType);
            bool isSuccess = MBRandom.RandomFloat < successChance;

            Debug.Print($"[Secret Alliances] Operation {operationType} for {initiator.Name}-{target.Name}: {successChance:F2} chance -> {(isSuccess ? "SUCCESS" : "FAILURE")}");

            if (isSuccess)
            {
                ExecuteSuccessfulOperation(alliance, operationType);
            }
            else
            {
                ExecuteFailedOperation(alliance, operationType);
            }

            // Apply operation-specific cooldowns (Espionage Cooldown Matrix)
            ApplyOperationCooldown(alliance, operationType);

            // Reset operation state
            alliance.PendingOperationType = 0;
            alliance.LastOperationDay = 0;

            // Player notification if involved
            if (initiator == Clan.PlayerClan || target == Clan.PlayerClan)
            {
                string resultText = isSuccess ? "completed successfully" : "ended in failure";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Secret operation {operationType} {resultText}.", 
                    isSuccess ? Colors.Green : Colors.Red));
            }
        }

        private float CalculateOperationSuccessChance(SecretAllianceRecord alliance, PendingOperationType operationType)
        {
            // Risk Model: operationDifficulty vs (Trust + Strength synergy + Leader skills)
            float baseDifficulty = GetOperationDifficulty(operationType);
            
            float trustFactor = alliance.TrustLevel * 0.4f;
            float strengthFactor = alliance.Strength * 0.3f;
            float synergy = (alliance.TrustLevel + alliance.Strength) * 0.15f; // Synergy bonus
            
            // Leader skills factor
            float leaderSkills = 0f;
            var initiator = alliance.GetInitiatorClan();
            if (initiator?.Leader != null)
            {
                leaderSkills = (initiator.Leader.GetSkillValue(DefaultSkills.Roguery) + 
                              initiator.Leader.GetSkillValue(DefaultSkills.Charm)) / 600f; // Normalize to 0-1
            }

            float totalCapability = trustFactor + strengthFactor + synergy + (leaderSkills * 0.15f);
            float successChance = totalCapability / baseDifficulty;

            return MathF.Max(0.1f, MathF.Min(0.9f, successChance)); // Clamp between 10-90%
        }

        private float GetOperationDifficulty(PendingOperationType operationType)
        {
            return operationType switch
            {
                PendingOperationType.CovertAid => 0.6f,
                PendingOperationType.SpyProbe => 0.8f,
                PendingOperationType.RecruitmentFeelers => 0.7f,
                PendingOperationType.SabotageRaid => 1.0f,
                PendingOperationType.CounterIntelligenceSweep => 0.9f,
                _ => 0.7f
            };
        }

        private void ExecuteSuccessfulOperation(SecretAllianceRecord alliance, PendingOperationType operationType)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            alliance.SuccessfulOperations++;

            switch (operationType)
            {
                case PendingOperationType.CovertAid:
                    // CovertAid: boosts target Strength & Trust; small secrecy hit; may spawn Financial intel
                    alliance.Strength = MathF.Min(1f, alliance.Strength + 0.1f);
                    alliance.TrustLevel = MathF.Min(1f, alliance.TrustLevel + 0.05f);
                    alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - 0.02f);
                    
                    // May generate financial intelligence
                    if (MBRandom.RandomFloat < 0.3f)
                    {
                        GenerateFinancialIntel(alliance, initiator.Leader, 500, 0.3f);
                    }
                    break;

                case PendingOperationType.SpyProbe:
                    // SpyProbe: chance to generate Military or General intel against enemy or neutral clans
                    GenerateSpyIntelligence(alliance);
                    break;

                case PendingOperationType.RecruitmentFeelers:
                    // RecruitmentFeelers: attempts to auto-create a linked alliance (forming coalition)
                    AttemptCoalitionExpansion(alliance);
                    break;

                case PendingOperationType.SabotageRaid:
                    // SabotageRaid: reduces rival clan's TotalStrength; creates Military intel
                    ExecuteSabotage(alliance);
                    break;

                case PendingOperationType.CounterIntelligenceSweep:
                    // CounterIntelligence: reduces existing intel reliability and lowers future leak chance
                    ExecuteCounterIntelligence(alliance);
                    break;
            }

            Debug.Print($"[Secret Alliances] Successful {operationType}: {initiator.Name}-{target.Name}");
        }

        private void ExecuteFailedOperation(SecretAllianceRecord alliance, PendingOperationType operationType)
        {
            // Failure severity influences secrecy loss and leak generation
            float failureSeverity = MBRandom.RandomFloat * 0.5f + 0.3f; // 0.3-0.8 range
            
            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - (failureSeverity * 0.1f));
            alliance.TrustLevel = MathF.Max(0f, alliance.TrustLevel - (failureSeverity * 0.05f));

            // Risk of leak generation on failure
            if (MBRandom.RandomFloat < failureSeverity * 0.4f)
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();
                var informants = GetPotentialInformants(initiator, target, alliance);
                
                if (informants.Any())
                {
                    ProcessLeak(alliance, informants);
                }
            }

            Debug.Print($"[Secret Alliances] Failed {operationType}: {alliance.InitiatorClanId}-{alliance.TargetClanId} (Severity: {failureSeverity:F2})");
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

        private float CalculateTransferMagnitudePercentile(int transferAmount, SecretAllianceRecord alliance)
        {
            // Simple percentile calculation based on typical transfer ranges
            // This could be enhanced with historical data tracking
            float typicalTransfer = 1000f; // Base amount for comparison
            float adjustedAmount = transferAmount / typicalTransfer;
            return MathF.Min(1f, MathF.Max(0f, adjustedAmount / 10f)); // Normalize to 0-1
        }

        private void GenerateFinancialIntel(SecretAllianceRecord alliance, Hero transferer, int amount, float severity)
        {
            // Find potential informants who might observe financial transactions
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();
            var potentialInformants = GetPotentialInformants(initiator, target, alliance);

            if (potentialInformants.Any() && MBRandom.RandomFloat < 0.4f) // 40% chance for financial intel
            {
                var informant = potentialInformants[MBRandom.RandomInt(potentialInformants.Count)];

                var intel = new AllianceIntelligence
                {
                    AllianceId = alliance.UniqueId,
                    InformerHeroId = informant.Id,
                    ReliabilityScore = CalculateInformerReliability(informant) * 0.9f, // Financial intel slightly less reliable
                    DaysOld = 0,
                    IsConfirmed = false,
                    SeverityLevel = severity,
                    ClanAId = alliance.InitiatorClanId,
                    ClanBId = alliance.TargetClanId,
                    IntelCategory = (int)AllianceIntelType.Financial
                };

                _intelligence.Add(intel);
                Debug.Print($"[Secret Alliances] Financial intelligence generated by {informant.Name} (Severity: {severity:F2})");
            }
        }
    }
}