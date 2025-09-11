using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SecretAlliances
{
    public class SubModule : MBSubModuleBase
    {
        private SecretAllianceBehavior _allianceBehavior;

        private int _currentAllianceEvaluationScore = 0;
        private int _currentBribeReceptivity = 0;
        private int _currentBribeAmount = 0;
        private bool _lastPactResult = false;


        // Add a flag to track rejection
        private bool _allianceRejected = false;

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);

            if (starterObject is CampaignGameStarter campaignStarter)
            {
                // Add the campaign behavior that manages the alliances
                _allianceBehavior = new SecretAllianceBehavior();
                campaignStarter.AddBehavior(_allianceBehavior);

                

                // Register dialog lines for secret alliance system
                AddDialogs(campaignStarter);

                AllianceUIHelper.DebugLog("SubModule.OnGameStart called - dialogs registered");

                InformationManager.DisplayMessage(new InformationMessage("Secret Alliances loaded!", Colors.Cyan));

                // Debug: Dump all alliances on startup for testing
                // This will show any existing alliances when loading a save
                if (_allianceBehavior != null)
                {
                    AllianceUIHelper.DumpAllAlliances(_allianceBehavior);
                }
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // --- MAIN OFFER ---
            starter.AddPlayerLine(
                "sa_main_offer",
                "hero_main_options",
                "sa_response_consider",
                "{=SA_PlayerOffer}I have a discreet proposal that could benefit both our clans...",
                CanOfferSecretAlliance,
                null,
                100,
                SecretAllianceClickableCondition,
                null);

            starter.AddDialogLine(
                "sa_response_consider",
                "sa_response_consider",   // matches output of sa_main_offer
                "sa_player_options",
                "{=SA_LordConsider}Speak carefully. If your proposal has merit, I will listen.",
                () => true,
                null);

            // --- OFFER OPTIONS ---
            starter.AddPlayerLine(
                "sa_offer_alliance",
                "sa_player_options",
                "sa_evaluate_offer",
                "{=SA_OfferAlliance}Our clans could coordinate secretly...",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_offer_bribe",
                "sa_player_options",
                "sa_bribe_response",
                "{=SA_OfferBribe}I'm prepared to offer compensation...",
                () => _allianceRejected && CanOfferBribe(),
                null);

            starter.AddPlayerLine(
                "sa_nevermind",
                "sa_player_options",
                "lord_pretalk",
                "{=SA_Nevermind}Perhaps another time.",
                () => true,
                () => ResetConversationState());

            // --- ALLIANCE BRANCH ---
            starter.AddDialogLine(
                "sa_evaluate_offer",
                "sa_evaluate_offer",    // comes from sa_offer_alliance
                "sa_alliance_decision",
                "{=SA_EvaluateOffer}An interesting proposition...",
                () => true,
                EvaluateAllianceOffer);

            starter.AddDialogLine(
                "sa_alliance_accept",
                "sa_alliance_decision",
                "lord_pretalk",
                "{=SA_AllianceAccept}Very well. Our clans shall coordinate in secret.",
                ShouldAcceptAlliance,
                AcceptAlliance);

            starter.AddDialogLine(
                "sa_alliance_reject",
                "sa_alliance_decision",
                "lord_pretalk",
                "{=SA_AllianceReject}The risks are too great. I must decline",
                () => !ShouldAcceptAlliance(),
                () => ResetConversationState());


            // --- BRIBE BRANCH ---
            starter.AddDialogLine(
                "sa_bribe_response",
                "sa_bribe_response",    // comes from sa_offer_bribe
                "sa_bribe_decision",
                "{=SA_BribeResponse}Gold speaks louder than words...",
                () => true,
                EvaluateBribeOffer);

            starter.AddPlayerLine(
                "sa_bribe_small",
                "sa_bribe_decision",
                "sa_bribe_result",
                "{=SA_BribeSmall}1000 denars for your discretion.",
                () => Hero.MainHero.Gold >= 1000,
                () => SetBribeAmount(1000));

            starter.AddPlayerLine(
                "sa_bribe_medium",
                "sa_bribe_decision",
                "sa_bribe_result",
                "{=SA_BribeMedium}3000 denars, and future considerations.",
                () => Hero.MainHero.Gold >= 3000,
                () => SetBribeAmount(3000));

            starter.AddPlayerLine(
                "sa_bribe_large",
                "sa_bribe_decision",
                "sa_bribe_result",
                "{=SA_BribeLarge}5000 denars, proof of my commitment.",
                () => Hero.MainHero.Gold >= 5000,
                () => SetBribeAmount(5000));

            starter.AddDialogLine(
                "sa_bribe_accept",
                "sa_bribe_result",
                "lord_pretalk",
                "{=SA_BribeAccept}Your generosity is noted...",
                ShouldAcceptBribe,
                AcceptBribe);

            starter.AddDialogLine(
                "sa_bribe_reject",
                "sa_bribe_result",
                "lord_pretalk",
                "{=SA_BribeReject}My loyalty is worth more than gold...",
                () => !ShouldAcceptBribe(),
                () => ResetConversationState());


            // --- INTELLIGENCE ---
            starter.AddPlayerLine(
                "sa_gather_info",
                "hero_main_options",
                "sa_info_response",
                "{=SA_GatherInfo}Have you heard any rumors?",
                CanGatherIntelligence,
                null,
                100,
                IntelligenceClickableCondition,
                null);

            starter.AddDialogLine(
                "sa_info_response_has_rumors",
                "sa_info_response",   // comes from sa_gather_info
                "lord_pretalk",
                "{=SA_InfoResponseRumors}Indeed, there are whispers of secret dealings...",
                () => HasRumorsToShare(),
                ShareIntelligence);

            starter.AddDialogLine(
                "sa_info_response_no_rumors",
                "sa_info_response",   // comes from sa_gather_info
                "lord_pretalk",
                "{=SA_InfoResponseNoRumors}I know nothing of such matters.",
                () => !HasRumorsToShare(),
                null);

            // --- ALLIANCE MANAGEMENT (for existing allies) ---
            starter.AddPlayerLine(
                "sa_deepen_pact",
                "hero_main_options",
                "sa_pact_options",
                "{=SA_DeepenPact}Perhaps we should deepen our arrangement...",
                CanDeepenPact,
                null,
                100);

            starter.AddPlayerLine(
                "sa_dissolve_alliance",
                "hero_main_options",
                "sa_dissolve_confirm",
                "{=SA_DissolveAlliance}I believe it's time to end our arrangement.",
                CanDissolveAlliance,
                null,
                100);

            // --- PACT OPTIONS ---
            starter.AddPlayerLine(
                "sa_trade_pact",
                "sa_pact_options",
                "sa_pact_result",
                "{=SA_TradePact}We should coordinate our trade efforts.",
                () => true,
                () => TrySetTradePact());

            starter.AddPlayerLine(
                "sa_military_pact",
                "sa_pact_options",
                "sa_pact_result",
                "{=SA_MilitaryPact}Our military forces should work together.",
                () => true,
                () => TrySetMilitaryPact());

            starter.AddPlayerLine(
                "sa_pact_nevermind",
                "sa_pact_options",
                "hero_main_options",
                "{=SA_PactNevermind}On second thought, our current arrangement is sufficient.",
                () => true,
                null);

            // --- PACT RESULTS ---
            starter.AddDialogLine(
                "sa_pact_success",
                "sa_pact_result",
                "hero_main_options",
                "{=SA_PactSuccess}Agreed. This coordination will benefit us both.",
                () => _lastPactResult,
                () => ResetConversationState());

            starter.AddDialogLine(
                "sa_pact_failure",
                "sa_pact_result",
                "hero_main_options",
                "{=SA_PactFailure}Perhaps we should wait before making such arrangements.",
                () => !_lastPactResult,
                () => ResetConversationState());

            // --- DISSOLUTION ---
            starter.AddDialogLine(
                "sa_dissolve_confirm",
                "sa_dissolve_confirm",
                "sa_dissolve_final",
                "{=SA_DissolveConfirm}If that is your wish...",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_dissolve_yes",
                "sa_dissolve_final",
                "hero_main_options",
                "{=SA_DissolveYes}Yes, it's for the best.",
                () => true,
                () => DissolveAlliance());

            starter.AddPlayerLine(
                "sa_dissolve_no",
                "sa_dissolve_final",
                "hero_main_options",
                "{=SA_DissolveNo}Perhaps I spoke hastily.",
                () => true,
                () => ResetConversationState());
        }


        // Conversation condition methods
        private bool CanOfferSecretAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null || targetHero.Clan == Clan.PlayerClan) return false;

            var playerClan = Clan.PlayerClan;
            if (playerClan == null) return false;

            // Can't offer to eliminated clans or those already allied
            if (targetHero.Clan.IsEliminated) return false;
            if (_allianceBehavior?.FindAlliance(playerClan, targetHero.Clan) != null) return false;

            // Basic requirements
            if (targetHero.Clan.Leader != targetHero) return false; // Only to clan leaders
            if (playerClan.Kingdom == null) return false; // Player must be in a kingdom

            return true;
        }

        private bool CanOfferBribe()
        {
            return Hero.MainHero?.Gold >= 1000;
        }

        private bool CanGatherIntelligence()
        {
            // Can gather intel if this hero might know about secret alliances
            return true; // Always show option, but may not have rumors
        }

        private bool HasRumorsToShare()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero == null) return false;

            return _allianceBehavior?.TryGetRumorsForHero(targetHero, out _) ?? false;
        }

        private bool SecretAllianceClickableCondition(out TextObject explanation)
        {
            explanation = new TextObject("{=SA_ClickCondition}Propose secret coordination");
            return true;
        }

        private bool IntelligenceClickableCondition(out TextObject explanation)
        {
            explanation = new TextObject("{=SA_IntelClickCondition}Gather intelligence");
            return true;
        }

        // Conversation consequence and evaluation methods
        private void EvaluateAllianceOffer()
        {
            var targetHero = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;

            if (targetHero?.Clan == null || playerClan == null) return;

            // Use the local method instead of the behavior's method
            _currentAllianceEvaluationScore = CalculateAllianceAcceptanceScore(playerClan, targetHero.Clan);
            _allianceRejected = _currentAllianceEvaluationScore < 60;
        }

        private void EvaluateBribeOffer()
        {
            var targetHero = Hero.OneToOneConversationHero;

            if (targetHero?.Clan == null) return;

            // Store bribe evaluation for later decision
            // AFTER
            // AFTER
            _currentBribeReceptivity = CalculateBribeReceptivity(targetHero);
        }

        private void SetBribeAmount(int amount)
        {
            // AFTER
            _currentBribeAmount = amount;
        }

        private bool ShouldAcceptAlliance()
        {
            // Check stored evaluation score

            var targetHero = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;

            // Check for existing alliance first
            if (_allianceBehavior?.FindAlliance(playerClan, targetHero?.Clan) != null)
            {
                return false;
            }

            // Check stored evaluation score - require ~70% for acceptance
            return _currentAllianceEvaluationScore >= 70;
        }

        private bool ShouldAcceptBribe()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero == null) return false;

            // AFTER: Use our private fields directly
            int amount = _currentBribeAmount;
            int receptivity = _currentBribeReceptivity;



            // Calculate acceptance based on amount and receptivity
            int acceptanceThreshold = 100 - receptivity;
            int amountScore = (amount / 100); // Every 100 denars = 1 point

            return amountScore >= acceptanceThreshold;
        }

        private void AcceptAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;

            if (targetHero?.Clan == null || playerClan == null) return;
            if (_allianceBehavior?.FindAlliance(playerClan, targetHero.Clan) != null) return;

            float initialSecrecy = 0.75f + (MBRandom.RandomFloat * 0.2f);
            float initialStrength = 0.08f + (MBRandom.RandomFloat * 0.12f);

            _allianceBehavior?.CreateAlliance(playerClan, targetHero.Clan, initialSecrecy, initialStrength);

            AllianceUIHelper.DebugLog($"Alliance created: {playerClan.Name} <-> {targetHero.Clan.Name} | Secrecy: {initialSecrecy:F2}, Strength: {initialStrength:F2}");

            ChangeRelationAction.ApplyPlayerRelation(targetHero, 5, true, false);
            ChangeClanInfluenceAction.Apply(playerClan, -10f);

            InformationManager.DisplayMessage(new InformationMessage($"Secret alliance formed with {targetHero.Clan.Name}!", Colors.Green));

            
            // Reset conversation state
            ResetConversationState();
        }

        private void ResetConversationState()
        {
            _currentAllianceEvaluationScore = 0;
            _currentBribeReceptivity = 0;
            _currentBribeAmount = 0;
            _lastPactResult = false;
        }

        private void AcceptBribe()
        {
            var targetHero = Hero.OneToOneConversationHero;
            var playerClan = Clan.PlayerClan;

            if (targetHero?.Clan == null || playerClan == null) return;

            
            int amount = _currentBribeAmount;

            // Transfer money
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, targetHero, amount, false);

            // Create a "bribed" alliance with different characteristics
            float initialSecrecy = 0.85f + (MBRandom.RandomFloat * 0.1f); // Higher secrecy (0.85-0.95)
            float initialStrength = 0.05f + (MBRandom.RandomFloat * 0.1f); // Lower strength (0.05-0.15)

            _allianceBehavior?.CreateAlliance(playerClan, targetHero.Clan, initialSecrecy, initialStrength, amount);

            // Relations improve more with bribe
            ChangeRelationAction.ApplyPlayerRelation(targetHero, 8, true, false);

            // But target hero gains negative traits over time (corruption)
            if (MBRandom.RandomFloat < 0.3f) // 30% chance
            {
                targetHero.AddSkillXp(DefaultSkills.Roguery, 100); // Becomes more roguish
            }
            InformationManager.DisplayMessage(new InformationMessage($"Bribed alliance formed with {targetHero.Clan.Name} for {amount} denars!", Colors.Yellow));



            // Reset conversation state
            ResetConversationState();
        }

        private void ShareIntelligence()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero == null) return;

            var intelligence = _allianceBehavior?.GetIntelligence();
            if (intelligence == null || !intelligence.Any()) return;

            // Share some intelligence about secret alliances
            var relevantIntel = intelligence.Where(i =>
                i.ReliabilityScore > 0.4f &&
                i.DaysOld < 30).Take(2);

            foreach (var intel in relevantIntel)
            {
                // Player gains knowledge about secret alliances
                var informer = intel.GetInformer();
                if (informer?.Clan != null)
                {
                    // Improve relation with informant slightly
                    ChangeRelationAction.ApplyPlayerRelation(targetHero, 2, false, false);
                }
            }

            // Player gains roguery skill for intelligence gathering
            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, 50);
        }

        // Helper calculation methods
        private int CalculateAllianceAcceptanceScore(Clan playerClan, Clan targetClan)
        {
            int score = 30; // Base 30% chance

            // Relationship factor (most important)
            if (targetClan.Leader != null)
            {
                int relation = targetClan.Leader.GetRelation(Hero.MainHero);
                score += relation / 3; // Each relation point = 0.33% acceptance
            }

            // Economic factors
            if (playerClan.Gold > targetClan.Gold * 1.5f)
            {
                score += 8; // Player is wealthy
            }
            else if (playerClan.Gold < targetClan.Gold * 0.5f)
            {
                score -= 10; // Player is poor
            }

            // Military strength comparison
            float strengthRatio = playerClan.TotalStrength / System.Math.Max(1f, targetClan.TotalStrength);
            if (strengthRatio > 1.5f)
            {
                score += 12; // Player is much stronger
            }
            else if (strengthRatio < 0.5f)
            {
                score -= 10; // Player is much weaker
            }

            // Political situation
            if (playerClan.Kingdom != null && targetClan.Kingdom != null)
            {
                if (playerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
                {
                    score -= 30; // Much harder if at war
                }
                else if (playerClan.Kingdom == targetClan.Kingdom)
                {
                    score += 10; // Easier if same kingdom
                }
            }

            // Target clan's current situation
            if (targetClan.Gold < 2000)
            {
                score += 15; // Desperate for resources
            }

            if (targetClan.TotalStrength < 100f)
            {
                score += 10; // Militarily weak, needs allies
            }

            // Leader personality traits
            if (targetClan.Leader != null)
            {
                var traits = targetClan.Leader.GetHeroTraits();
                score += traits.Calculating * 5; // Calculating leaders more likely to accept
                score -= traits.Honor * 3; // Honorable leaders less likely
                score += traits.Valor * 2; // Brave leaders more willing to take risks
            }

            // Player's reputation and skills
            int leadership = Hero.MainHero.GetSkillValue(DefaultSkills.Leadership);
            int charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
            int roguery = Hero.MainHero.GetSkillValue(DefaultSkills.Roguery);

            score += (leadership + charm + roguery) / 10; // Skills help persuasion

            // Random factor for unpredictability
            score += MBRandom.RandomInt(-15, 15);

            AllianceUIHelper.DebugLog($"Alliance acceptance score for {playerClan.Name} -> {targetClan.Name}: {score}");
            return System.Math.Max(0, System.Math.Min(100, score));
        }

        private int CalculateBribeReceptivity(Hero targetHero)
        {
            int receptivity = 30; // Base 30% receptivity

            // Personality traits heavily influence bribe acceptance
            if (targetHero != null)
            {
                var traits = targetHero.GetHeroTraits();
                receptivity -= traits.Honor * 15; // Honorable heroes very resistant
                receptivity += traits.Calculating * 8; // Calculating heroes more receptive
                receptivity -= traits.Generosity * 5; // Generous heroes less likely to take bribes
                receptivity += System.Math.Max(0, -traits.Mercy * 3); // Cruel heroes more receptive
            }

            // Economic situation
            if (targetHero?.Clan != null)
            {
                if (targetHero.Clan.Gold < 3000)
                {
                    receptivity += 20; // Poor clans more receptive
                }
                else if (targetHero.Clan.Gold > 15000)
                {
                    receptivity -= 10; // Rich clans less receptive
                }
            }

            // Current relations
            int relation = targetHero.GetRelation(Hero.MainHero);
            if (relation > 20)
            {
                receptivity += 15; // Friends more willing to accept "gifts"
            }
            else if (relation < -10)
            {
                receptivity -= 20; // Enemies suspicious of bribes
            }

            // Skills
            int roguery = targetHero.GetSkillValue(DefaultSkills.Roguery);
            receptivity += roguery / 5; // Roguish heroes more corrupt

            // Random factor
            receptivity += MBRandom.RandomInt(-10, 10);

            return System.Math.Max(0, System.Math.Min(80, receptivity));
        }

        // New dialog conditions and consequences
        private bool CanDeepenPact()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive && !alliance.IsOnCooldown();
        }

        private bool CanDissolveAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive;
        }

        private void TrySetTradePact()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            _lastPactResult = _allianceBehavior?.TrySetTradePact(Clan.PlayerClan, targetHero.Clan) ?? false;
        }

        private void TrySetMilitaryPact()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            _lastPactResult = _allianceBehavior?.TrySetMilitaryPact(Clan.PlayerClan, targetHero.Clan) ?? false;
        }

        private void DissolveAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            _allianceBehavior?.TryDissolveAlliance(Clan.PlayerClan, targetHero.Clan, true);
            ResetConversationState();
        }
    }
}