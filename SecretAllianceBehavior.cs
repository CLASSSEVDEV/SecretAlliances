using System;
using System.Collections.Generic;
using System.Linq;
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
            }

            // Check for new alliance opportunities
            if (MBRandom.RandomFloat < 0.1f) // 10% chance daily to consider new alliances
            {
                ConsiderNewAlliances(clan);
            }

            // Process intelligence aging
            ProcessIntelligence();
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
        }

        private void ProcessTradePactEffects(SecretAllianceRecord alliance)
        {
            if (!alliance.TradePact || !alliance.IsActive) return;

            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator?.Leader == null || target?.Leader == null) return;

            // Calculate daily gold transfer based on EconomicIncentive and TrustLevel
            int baseAmount = MBRandom.RandomInt(25, 150);
            float multiplier = alliance.EconomicIncentive * alliance.TrustLevel;
            int transferAmount = (int)(baseAmount * multiplier);

            if (transferAmount < 5) return; // Skip very small amounts

            // Transfer from richer to poorer clan
            Hero richer, poorer;
            if (initiator.Gold > target.Gold)
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
                
                // Secrecy decays slightly due to trade activities
                alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - 0.001f);
                
                Debug.Print($"[Secret Alliances] Trade Pact: {richer.Name} transferred {transferAmount} denars to {poorer.Name} (Alliance GroupId: {alliance.GroupId})");
            }
        }

        private void ProcessMilitaryPactEffects(SecretAllianceRecord alliance)
        {
            if (!alliance.MilitaryPact || !alliance.IsActive) return;

            Debug.Print($"[Secret Alliances] Military Pact active for alliance GroupId {alliance.GroupId}");
            
            // Military pacts affect secrecy (slightly more decay) but are handled in UpdateAllianceStrength and UpdateAllianceSecrecy
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
            secrecyLoss += totalHeroes * 0.0005f;

            // If clans are at war with each other's allies, secrecy decreases faster
            if (AreClansInConflictingSituations(initiator, target))
            {
                secrecyLoss += 0.003f;
            }

            // Distance between clan settlements affects secrecy maintenance
            float distance = GetClanDistance(initiator, target);
            if (distance > 100f) // Long distance makes coordination harder
            {
                secrecyLoss += (distance - 100f) / 10000f;
            }

            // Leader personality affects secrecy maintenance
            if (initiator.Leader != null)
            {
                // Calculating personality-based secrecy loss
                var traits = initiator.Leader.GetHeroTraits();
                if (traits.Honor > 0) secrecyLoss += traits.Honor * 0.001f; // Honorable leaders struggle with secrecy
                if (traits.Generosity > 0) secrecyLoss += traits.Generosity * 0.0005f; // Generous leaders may talk too much
            }

            // Military pacts cause additional secrecy decay
            if (alliance.MilitaryPact)
            {
                secrecyLoss += 0.001f;
            }

            alliance.Secrecy = MathF.Max(0f, alliance.Secrecy - secrecyLoss);
        }

        private void UpdateAllianceStrength(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            // Base growth
            float strengthGain = DAILY_STRENGTH_GROWTH;

            // Factors that increase strength

            // Mutual benefit increases strength
            float mutualBenefit = CalculateMutualBenefit(initiator, target);
            strengthGain *= (1f + mutualBenefit);

            // Trust level affects strength growth
            strengthGain *= (0.5f + alliance.TrustLevel * 0.5f);

            // Economic incentives boost strength
            if (alliance.BribeAmount > 0)
            {
                float bribeEffect = MathF.Min(0.5f, alliance.BribeAmount / 10000f);
                strengthGain *= (1f + bribeEffect);
            }

            // Common enemies strengthen alliance
            if (alliance.HasCommonEnemies)
            {
                strengthGain *= 1.3f;
            }

            // Military pacts multiply growth
            if (alliance.MilitaryPact)
            {
                strengthGain *= 1.5f;
            }

            // Recent successful operations boost strength
            if (alliance.SuccessfulOperations > 0)
            {
                strengthGain *= (1f + alliance.SuccessfulOperations * 0.1f);
            }

            // Political pressure can drive clans together
            float politicalPressure = CalculatePoliticalPressure(initiator, target);
            strengthGain *= (1f + politicalPressure * 0.2f);

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
                    AllianceId = alliance.InitiatorClanId, // Using as unique identifier  
                    InformerHeroId = informant.Id,
                    ReliabilityScore = CalculateInformerReliability(informant),
                    DaysOld = 0,
                    IsConfirmed = false,
                    SeverityLevel = severity
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
            var attackerParties = mapEvent.AttackerSide?.Parties?.ToList() ?? new List<PartyBase>();
            var defenderParties = mapEvent.DefenderSide?.Parties?.ToList() ?? new List<PartyBase>();

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

        private bool EvaluateSideDefection(MapEvent mapEvent, Clan sideClan, Clan opposingClan, List<SecretAllianceRecord> relevantAlliances)
        {
            float defectionProbability = 0.05f; // Base 5% chance

            foreach (var alliance in relevantAlliances)
            {
                // Alliance strength increases defection chance
                defectionProbability += alliance.Strength * 0.3f;
                
                // Trust level affects loyalty
                defectionProbability += alliance.TrustLevel * 0.2f;
                
                // Military pact makes defection more likely
                if (alliance.MilitaryPact)
                {
                    defectionProbability += 0.15f;
                }
            }

            // Political pressure increases defection likelihood
            float pressure = CalculatePoliticalPressure(sideClan, opposingClan);
            defectionProbability += pressure * 0.2f;

            // Desperation increases defection chance
            float desperation = CalculateDesperationLevel(sideClan);
            defectionProbability += desperation * 0.25f;

            // Relative battlefield power affects decision
            try
            {
                float sideStrength = mapEvent.AttackerSide?.TotalStrength ?? 1f;
                float opposingStrength = mapEvent.DefenderSide?.TotalStrength ?? 1f;
                
                // If the side they're considering switching to is much stronger
                if (opposingStrength > sideStrength * 1.5f)
                {
                    defectionProbability += 0.1f;
                }
            }
            catch
            {
                // Fallback if battle strength calculation fails
                defectionProbability += 0.05f;
            }

            // Cap the probability
            defectionProbability = MathF.Min(0.4f, defectionProbability);

            bool shouldDefect = MBRandom.RandomFloat < defectionProbability;
            
            if (shouldDefect)
            {
                Debug.Print($"[Secret Alliances] Defection probability for {sideClan.Name}: {defectionProbability:F3} - DEFECTING");
            }

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
                Secrecy = initialSecrecy,
                Strength = initialStrength,
                BribeAmount = bribe,
                IsActive = true,
                CreatedGameDay = CampaignTime.Now.GetDayOfYear,
                TrustLevel = 0.5f,
                RiskTolerance = MBRandom.RandomFloat,
                EconomicIncentive = CalculateMutualBenefit(initiator, target),
                PoliticalPressure = CalculatePoliticalPressure(initiator, target),
                MilitaryAdvantage = (initiator.TotalStrength + target.TotalStrength) /
                                   MathF.Max(1f, initiator.Kingdom?.TotalStrength ?? 1f),
                HasCommonEnemies = HasCommonEnemies(initiator, target),
                GroupId = allianceGroupId,
                LastInteractionDay = CampaignTime.Now.GetDayOfYear,
                CooldownDays = 0
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

        public bool TryGetRumorsForHero(Hero hero, out string rumorSummary)
        {
            rumorSummary = "";
            if (hero?.Clan == null) return false;

            // Check if this hero might know about alliances
            var relevantIntel = _intelligence.Where(i =>
                i.ReliabilityScore > 0.3f &&
                i.DaysOld < 45 &&
                (i.GetInformer()?.Clan == hero.Clan || i.AllianceId == hero.Clan.Id)).ToList();

            if (!relevantIntel.Any()) return false;

            var intel = relevantIntel.First();
            rumorSummary = $"There are whispers of secret dealings... (Reliability: {intel.ReliabilityScore:F1}, Age: {intel.DaysOld} days)";
            
            Debug.Print($"[Secret Alliances] Rumors shared by {hero.Name}: {rumorSummary}");
            return true;
        }
    }
}