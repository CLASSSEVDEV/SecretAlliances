using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Settlements;
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
        private bool _hasTriedBribing = false;

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

                // Register console commands for debugging
                ConsoleCommands.RegisterCommands();



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
                "sa_response_consider",   // comes from sa_main_offer
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
                () => _allianceRejected && CanOfferBribe() && !_hasTriedBribing,
                null);

            starter.AddPlayerLine(
                "sa_nevermind",
                "sa_player_options",
                "hero_main_options",
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
                "hero_main_options",
                "{=SA_AllianceAccept}Very well. Our clans shall coordinate in secret.",
                ShouldAcceptAlliance,
                AcceptAlliance);

            starter.AddDialogLine(
                "sa_alliance_reject",
                "sa_alliance_decision",
                "sa_player_options",
                "{=SA_AllianceReject}The risks are too great. I must decline your proposal at this time.",
                () => !ShouldAcceptAlliance(),
                RejectAllianceWithFeedback);

            // Add a return option after alliance rejection to allow trying bribe or leaving
            starter.AddPlayerLine(
                "sa_alliance_rejected_try_bribe",
                "sa_player_options",
                "sa_bribe_response",
                "{=SA_AllianceRejectedTryBribe}Perhaps gold could change your mind...",
                () => _allianceRejected && CanOfferBribe() && !_hasTriedBribing,
                null);

            starter.AddPlayerLine(
                "sa_alliance_rejected_accept",
                "sa_player_options",
                "hero_main_options",
                "{=SA_AllianceRejectedAccept}I understand your position.",
                () => _allianceRejected,
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
                "hero_main_options",
                "{=SA_BribeAccept}Your generosity is noted...",
                ShouldAcceptBribe,
                AcceptBribe);

            starter.AddDialogLine(
                "sa_bribe_reject",
                "sa_bribe_result",
                "sa_player_options",
                "{=SA_BribeReject}My loyalty is worth more than gold...",
                () => !ShouldAcceptBribe(),
                () => ResetConversationState());

            // Add player option to return to main menu after bribe rejection
            starter.AddPlayerLine(
                "sa_bribe_rejected_return",
                "sa_player_options",
                "hero_main_options",
                "{=SA_BribeRejectedReturn}I understand. Perhaps another time.",
                () => _hasTriedBribing,
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
                "hero_main_options",
                "{=SA_InfoResponseRumors}Indeed, there are whispers of secret dealings...",
                () => HasRumorsToShare(),
                ShareIntelligence);

            starter.AddDialogLine(
                "sa_info_response_no_rumors",
                "sa_info_response",   // comes from sa_gather_info
                "hero_main_options",
                "{=SA_InfoResponseNoRumors}I know nothing of such matters.",
                () => !HasRumorsToShare(),
                null);

            // --- ALLIANCE STATUS ---
            starter.AddPlayerLine(
                "sa_view_status",
                "hero_main_options",
                "sa_status_display",
                "{=SA_ViewStatus}What is the status of our arrangements?",
                CanViewAllianceStatus,
                null,
                100);

            starter.AddDialogLine(
                "sa_status_display",
                "sa_status_display",
                "hero_main_options",
                "{=SA_StatusDisplay}Let me update you on our current understanding...",
                () => true,
                DisplayAllianceStatus);

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

            // --- ADVANCED ALLIANCE FEATURES ---
            starter.AddPlayerLine(
                "sa_upgrade_alliance",
                "hero_main_options",
                "sa_upgrade_result",
                "{=SA_UpgradeAlliance}Our alliance could reach new heights...",
                CanUpgradeAlliance,
                null,
                100);

            starter.AddDialogLine(
                "sa_upgrade_success",
                "sa_upgrade_result",
                "hero_main_options",
                "{=SA_UpgradeSuccess}Indeed, our cooperation shall expand.",
                () => TryUpgradeCurrentAlliance(),
                () => ResetConversationState());

            starter.AddDialogLine(
                "sa_upgrade_failure",
                "sa_upgrade_result",
                "hero_main_options",
                "{=SA_UpgradeFailure}We are not yet ready for such advancement.",
                () => !TryUpgradeCurrentAlliance(),
                () => ResetConversationState());

            // --- ECONOMIC WARFARE ---
            starter.AddPlayerLine(
                "sa_economic_warfare",
                "hero_main_options",
                "sa_economic_target",
                "{=SA_EconomicWarfare}We should disrupt our enemies' trade...",
                CanLaunchEconomicWarfare,
                null,
                100);

            starter.AddDialogLine(
                "sa_economic_target",
                "sa_economic_target",
                "hero_main_options",
                "{=SA_EconomicTarget}Yes, coordinated economic pressure will serve us well.",
                () => true,
                LaunchEconomicWarfare);

            // --- SPY OPERATIONS ---
            starter.AddPlayerLine(
                "sa_spy_operations",
                "hero_main_options",
                "sa_spy_target",
                "{=SA_SpyOperations}Our intelligence network should be more active...",
                CanLaunchSpyOperation,
                null,
                100);

            starter.AddDialogLine(
                "sa_spy_target",
                "sa_spy_target",
                "hero_main_options",
                "{=SA_SpyTarget}Information is indeed the greatest weapon.",
                () => true,
                LaunchSpyOperation);

            // --- JOINT CAMPAIGNS ---
            starter.AddPlayerLine(
                "sa_joint_campaign",
                "hero_main_options",
                "sa_campaign_target",
                "{=SA_JointCampaign}We should coordinate a military campaign...",
                CanLaunchJointCampaign,
                null,
                100);

            starter.AddDialogLine(
                "sa_campaign_target",
                "sa_campaign_target",
                "hero_main_options",
                "{=SA_CampaignTarget}Our combined forces shall be unstoppable.",
                () => true,
                LaunchJointCampaign);
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

        private bool HasAlreadyTriedBribing()
        {
            return _hasTriedBribing;
        }

        private bool CanViewAllianceStatus()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            // Can view status if we have any alliance or if they might have intelligence
            var hasAlliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan) != null;
            var hasIntelligence = _allianceBehavior?.ShouldShowRumorOption(targetHero) ?? false;

            return hasAlliance || hasIntelligence;
        }

        private void DisplayAllianceStatus()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            if (alliance != null)
            {
                string statusMessage = $"Alliance with {targetHero.Clan.Name}: " +
                    $"Strength {alliance.Strength:P0}, Trust {alliance.TrustLevel:P0}, " +
                    $"Secrecy {alliance.Secrecy:P0}";

                if (alliance.TradePact) statusMessage += " [Trade Pact Active]";
                if (alliance.MilitaryPact) statusMessage += " [Military Pact Active]";
                if (alliance.IsOnCooldown()) statusMessage += " [On Cooldown]";

                InformationManager.DisplayMessage(new InformationMessage(statusMessage, Colors.Cyan));
            }
            else
            {
                // Show general intelligence about secret alliances
                var rumors = _allianceBehavior?.TryGetRumorsForHero(targetHero, Clan.PlayerClan, 2);
                if (rumors?.Any() == true)
                {
                    foreach (var rumor in rumors)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"Intelligence: {rumor}", Colors.Yellow));
                    }
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("No current intelligence about secret alliances.", Colors.Gray));
                }
            }
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

            // Use the version that returns bool and string
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
            _allianceRejected = _currentAllianceEvaluationScore < 65; // More consistent threshold

            // Debug logging to help players understand rejections
            if (_allianceRejected)
            {
                AllianceUIHelper.DebugLog($"Alliance proposal rejected by {targetHero.Clan.Name} (Score: {_currentAllianceEvaluationScore}/100)");
            }
        }

        private void EvaluateBribeOffer()
        {
            var targetHero = Hero.OneToOneConversationHero;

            if (targetHero?.Clan == null) return;

            // Mark that we've tried bribing
            _hasTriedBribing = true;
            
            // Store bribe evaluation for later decision
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

            // Check stored evaluation score - require 65% for acceptance (matches rejection threshold)
            return _currentAllianceEvaluationScore >= 65;
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

        private void RejectAllianceWithFeedback()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan != null)
            {
                // Set the rejection flag to enable bribe options
                _allianceRejected = true;
                
                // Provide helpful feedback to the player about why the alliance was rejected
                string feedbackMessage = GenerateRejectionFeedback(targetHero.Clan, _currentAllianceEvaluationScore);
                InformationManager.DisplayMessage(new InformationMessage(feedbackMessage, Colors.Red));

                // Also log for debugging
                AllianceUIHelper.DebugLog($"Alliance rejected by {targetHero.Clan.Name}: {feedbackMessage}");
            }

            // Don't reset conversation state here - allow player to try bribe option
        }

        private string GenerateRejectionFeedback(Clan targetClan, int score)
        {
            var playerClan = Clan.PlayerClan;
            if (targetClan?.Leader == null || playerClan == null) return "Alliance proposal rejected.";

            var feedback = new List<string>();

            if (score < 25)
            {
                feedback.Add($"{targetClan.Name} has very little interest in an alliance");
            }
            else if (score < 50)
            {
                feedback.Add($"{targetClan.Name} is hesitant about forming an alliance");
            }
            else
            {
                feedback.Add($"{targetClan.Name} is considering your proposal but has concerns");
            }

            // Identify specific issues
            int relation = targetClan.Leader.GetRelation(Hero.MainHero);
            if (relation < -10)
            {
                feedback.Add("your poor relationship with their leader");
            }
            else if (relation < 10)
            {
                feedback.Add("improve relations with their leader first");
            }

            if (playerClan.Kingdom != null && targetClan.Kingdom != null && playerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
            {
                feedback.Add("your kingdoms are at war");
            }

            if (playerClan.Gold < targetClan.Gold * 0.5f)
            {
                feedback.Add("your clan's economic weakness");
            }

            var existingAlliances = _allianceBehavior?.GetAlliances()?.Count(a =>
                a.IsActive && (a.InitiatorClanId == targetClan.Id || a.TargetClanId == targetClan.Id)) ?? 0;
            if (existingAlliances >= 2)
            {
                feedback.Add("they already have multiple secret alliances");
            }

            string result = feedback.Count > 1 ?
                $"{feedback[0]} due to {string.Join(", ", feedback.Skip(1))}." :
                $"{feedback[0]}.";

            return result;
        }

        private void ResetConversationState()
        {
            _currentAllianceEvaluationScore = 0;
            _currentBribeReceptivity = 0;
            _currentBribeAmount = 0;
            _lastPactResult = false;
            _allianceRejected = false;
            _hasTriedBribing = false;
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

            // Use the more appropriate rumor sharing method
            var rumors = _allianceBehavior?.TryGetRumorsForHero(targetHero, Clan.PlayerClan, 2);
            if (rumors != null && rumors.Any())
            {
                foreach (var rumor in rumors)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Intelligence: {rumor}", Colors.Yellow));
                }
                
                // Player gains roguery skill for intelligence gathering
                Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, 50);
                
                // Improve relation with informant slightly
                ChangeRelationAction.ApplyPlayerRelation(targetHero, 2, false, false);
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("No useful intelligence available.", Colors.Gray));
            }
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

            // Current alliance commitments reduce willingness
            var existingAlliances = _allianceBehavior?.GetAlliances()?.Count(a =>
                a.IsActive && (a.InitiatorClanId == targetClan.Id || a.TargetClanId == targetClan.Id)) ?? 0;
            score -= existingAlliances * 8; // Each existing alliance reduces willingness

            // Recent betrayals make them more cautious
            if (targetClan.Leader != null)
            {
                int playerRelationWithRuler = targetClan.Kingdom?.Leader?.GetRelation(Hero.MainHero) ?? 0;
                if (playerRelationWithRuler < -20)
                {
                    score -= 20; // Poor relations with their ruler makes them cautious
                }
            }

            // Player's past alliance track record (if we have intelligence about them)
            var playerAlliances = _allianceBehavior?.GetAlliances()?.Where(a =>
                a.InitiatorClanId == playerClan.Id || a.TargetClanId == playerClan.Id).ToList() ?? new List<SecretAllianceRecord>();

            int brokenAlliances = playerAlliances.Count(a => a.BetrayalRevealed || !a.IsActive);
            int successfulAlliances = playerAlliances.Count(a => a.IsActive && a.SuccessfulOperations > 3);

            score -= brokenAlliances * 12; // Past betrayals hurt reputation
            score += successfulAlliances * 8; // Successful alliances improve reputation

            // Random factor for unpredictability (reduced to make evaluation more predictable)
            score += MBRandom.RandomInt(-8, 8);

            AllianceUIHelper.DebugLog($"Alliance acceptance score for {playerClan.Name} -> {targetClan.Name}: {score} " +
                $"(Base: 30, Relations: {(targetClan.Leader?.GetRelation(Hero.MainHero) ?? 0) / 3}, " +
                $"Existing alliances: -{existingAlliances * 8}, Reputation: {successfulAlliances * 8 - brokenAlliances * 12})");
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

        // Advanced feature dialog methods
        private bool CanUpgradeAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive && alliance.AllianceRank < 2 &&
                   alliance.Strength >= 0.5f && alliance.TrustLevel >= 0.5f && alliance.ReputationScore >= 0.6f;
        }

        private bool TryUpgradeCurrentAlliance()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            return _allianceBehavior?.TryUpgradeAlliance(Clan.PlayerClan, targetHero.Clan) ?? false;
        }

        private bool CanLaunchEconomicWarfare()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive && alliance.TradePact && alliance.EconomicIntegration >= 0.3f;
        }

        private void LaunchEconomicWarfare()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            if (alliance == null) return;

            // Find a suitable target (enemy clan)
            var enemyClan = FindSuitableEconomicTarget(alliance);
            if (enemyClan != null)
            {
                _allianceBehavior.ExecuteEconomicWarfare(alliance, enemyClan);
                InformationManager.DisplayMessage(new InformationMessage($"Economic warfare launched against {enemyClan.Name}!", Colors.Yellow));
            }
        }

        private bool CanLaunchSpyOperation()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive && alliance.SpyNetworkLevel >= 2;
        }

        private void LaunchSpyOperation()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            if (alliance == null) return;

            // Find a suitable target (enemy clan)
            var enemyClan = FindSuitableSpyTarget(alliance);
            if (enemyClan != null)
            {
                bool success = _allianceBehavior.ExecuteSpyOperation(alliance, enemyClan, 1); // Information gathering
                if (success)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Spy operation successful against {enemyClan.Name}!", Colors.Green));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Spy operation failed against {enemyClan.Name}.", Colors.Red));
                }
            }
        }

        private bool CanLaunchJointCampaign()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsActive && alliance.MilitaryPact &&
                   alliance.MilitaryCoordination >= 0.3f && alliance.AllianceRank >= 1;
        }

        private void LaunchJointCampaign()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            if (alliance == null) return;

            // Find a suitable target settlement
            var targetSettlement = FindSuitableCampaignTarget(alliance);
            if (targetSettlement != null)
            {
                _allianceBehavior.InitiateJointCampaign(alliance, targetSettlement);
                InformationManager.DisplayMessage(new InformationMessage($"Joint campaign launched against {targetSettlement.Name}!", Colors.Blue));
            }
        }

        // Helper methods for finding suitable targets
        private Clan FindSuitableEconomicTarget(SecretAllianceRecord alliance)
        {
            var playerClan = Clan.PlayerClan;
            var allyClan = alliance.InitiatorClanId == playerClan.Id ? alliance.GetTargetClan() : alliance.GetInitiatorClan();

            // Find clans that are enemies of both player and ally
            return Clan.All
                .Where(c => !c.IsEliminated && c != playerClan && c != allyClan)
                .Where(c => c.Kingdom != null && (
                    (playerClan.Kingdom?.IsAtWarWith(c.Kingdom) == true) ||
                    (allyClan?.Kingdom?.IsAtWarWith(c.Kingdom) == true)))
                .Where(c => c.Gold > 5000) // Target wealthy clans
                .OrderByDescending(c => c.Gold)
                .FirstOrDefault();
        }

        private Clan FindSuitableSpyTarget(SecretAllianceRecord alliance)
        {
            var playerClan = Clan.PlayerClan;
            var allyClan = alliance.InitiatorClanId == playerClan.Id ? alliance.GetTargetClan() : alliance.GetInitiatorClan();

            // Find clans that could provide valuable intelligence
            return Clan.All
                .Where(c => !c.IsEliminated && c != playerClan && c != allyClan)
                .Where(c => c.Kingdom != null)
                .Where(c => c.TotalStrength > 50f) // Target militarily significant clans
                .OrderByDescending(c => c.TotalStrength)
                .FirstOrDefault();
        }

        private Settlement FindSuitableCampaignTarget(SecretAllianceRecord alliance)
        {
            var playerClan = Clan.PlayerClan;
            var allyClan = alliance.InitiatorClanId == playerClan.Id ? alliance.GetTargetClan() : alliance.GetInitiatorClan();

            // Find enemy settlements that are viable targets
            return Settlement.All
                .Where(s => s.IsFortification && s.OwnerClan != null)
                .Where(s => s.OwnerClan != playerClan && s.OwnerClan != allyClan)
                .Where(s => playerClan.Kingdom?.IsAtWarWith(s.OwnerClan.Kingdom) == true ||
                           allyClan?.Kingdom?.IsAtWarWith(s.OwnerClan.Kingdom) == true)
                .Where(s => s.Position2D.DistanceSquared(playerClan.FactionMidSettlement?.Position2D ?? TaleWorlds.Library.Vec2.Zero) < 10000) // Within reasonable distance
                .OrderBy(s => s.Town?.Prosperity ?? s.Village?.Hearth ?? 1000f) // Target weaker settlements first
                .FirstOrDefault();
        }
    }
}