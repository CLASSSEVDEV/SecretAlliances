using System;
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


        // Add conversation tracking for recently rejected alliances
        private Dictionary<MBGUID, int> _recentRejections = new Dictionary<MBGUID, int>();

        private bool HasRecentRejection()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            try
            {
                if (_recentRejections.TryGetValue(targetHero.Clan.Id, out int rejectionDay))
                {
                    int currentDay = CampaignTime.Now.GetDayOfYear;
                    return currentDay - rejectionDay < 3; // 3 day cooldown
                }
                return false;
            }
            catch (Exception ex)
            {
                AllianceUIHelper.DebugLog($"Error checking rejection cooldown for {targetHero.Clan.Name}: {ex.Message}");
                return false;
            }
        }

        private void AddRejectionCooldown()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan != null)
            {
                try
                {
                    _recentRejections[targetHero.Clan.Id] = CampaignTime.Now.GetDayOfYear;
                    
                    // Clean up old rejections (keep only last 10 entries)
                    if (_recentRejections.Count > 10)
                    {
                        var oldestKey = _recentRejections.Keys.First();
                        _recentRejections.Remove(oldestKey);
                    }
                }
                catch (Exception ex)
                {
                    AllianceUIHelper.DebugLog($"Error adding rejection cooldown for {targetHero.Clan.Name}: {ex.Message}");
                }
            }
        }

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);

            if (starterObject is CampaignGameStarter campaignStarter)
            {
                try
                {
                    // Add the campaign behavior that manages the alliances
                    _allianceBehavior = new SecretAllianceBehavior();
                    campaignStarter.AddBehavior(_allianceBehavior);

                    // Register console commands for debugging
                    ConsoleCommands.RegisterCommands();

                    // Register dialog lines for secret alliance system
                    AddDialogs(campaignStarter);

                    AllianceUIHelper.DebugLog("SubModule.OnGameStart called - dialogs registered");

                    InformationManager.DisplayMessage(new InformationMessage("Secret Alliances v1.2.9 compatible loaded!", Colors.Cyan));

                    // Debug: Dump all alliances on startup for testing
                    // This will show any existing alliances when loading a save
                    if (_allianceBehavior != null)
                    {
                        AllianceUIHelper.DumpAllAlliances(_allianceBehavior);
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"Secret Alliances failed to load: {ex.Message}", Colors.Red));
                    AllianceUIHelper.DebugLog($"Error in OnGameStart: {ex}");
                }
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                AllianceUIHelper.DebugLog("Starting dialogue registration...");

                // Register all dialogue lines with error handling
                RegisterMainOfferDialogues(starter);
                RegisterOfferOptionDialogues(starter);
                RegisterAllianceBranchDialogues(starter);
                RegisterBribeBranchDialogues(starter);
                RegisterRemainingDialogues(starter); // Contains all intelligence, status, management, and advanced features

                AllianceUIHelper.DebugLog("All dialogues registered successfully");
            }
            catch (Exception ex)
            {
                AllianceUIHelper.DebugLog($"Error registering dialogues: {ex}");
                InformationManager.DisplayMessage(new InformationMessage("Error loading Secret Alliances dialogues", Colors.Red));
            }
        }

        private void RegisterMainOfferDialogues(CampaignGameStarter starter)
        {
        private void RegisterMainOfferDialogues(CampaignGameStarter starter)
        {
            // --- MAIN OFFER ---
            starter.AddPlayerLine(
                "sa_main_offer",
                "hero_main_options",
                "sa_response_consider",
                "{=SA_PlayerOffer}I have a discreet proposal that could benefit both our clans...",
                () => CanOfferSecretAlliance() && !HasRecentRejection(),
                null,
                100,
                SecretAllianceClickableCondition,
                null);

            // Cooldown version of main offer (higher priority when conditions match)
            starter.AddPlayerLine(
                "sa_main_offer_cooldown",
                "hero_main_options",
                "sa_rejection_cooldown_response",
                "{=SA_PlayerOfferCooldown}I have a discreet proposal that could benefit both our clans...",
                () => CanOfferSecretAlliance() && HasRecentRejection(),
                null,
                101, // Higher priority than main offer
                SecretAllianceClickableCondition,
                null);

            starter.AddDialogLine(
                "sa_rejection_cooldown_response_line",
                "sa_rejection_cooldown_response",
                "hero_main_options",
                "{=SA_RejectionCooldownResponse}We discussed such matters recently. I need time to consider your proposal fully.",
                () => true,
                null);

            starter.AddDialogLine(
                "sa_response_consider_line",
                "sa_response_consider",   // matches output of sa_main_offer
                "sa_player_options",
                "{=SA_LordConsider}Speak carefully. If your proposal has merit, I will listen.",
                () => true,
                null);
        }

        private void RegisterOfferOptionDialogues(CampaignGameStarter starter)
        {
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
                () => HasRecentRejection() && CanOfferBribe(),
                null);

            starter.AddPlayerLine(
                "sa_nevermind",
                "sa_player_options",
                "hero_main_options",
                "{=SA_Nevermind}Perhaps another time.",
                () => true,
                () => ResetConversationState());
        }

        private void RegisterAllianceBranchDialogues(CampaignGameStarter starter)
        {
            // --- ALLIANCE BRANCH ---
            starter.AddDialogLine(
                "sa_evaluate_offer_line",
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
                "hero_main_options",
                "{=SA_AllianceReject}The risks are too great. I must decline your proposal at this time.",
                () => !ShouldAcceptAlliance(),
                RejectAllianceWithFeedback);
        }

        private void RegisterBribeBranchDialogues(CampaignGameStarter starter)
        {
        private void RegisterBribeBranchDialogues(CampaignGameStarter starter)
        {
            // --- BRIBE BRANCH ---
            starter.AddDialogLine(
                "sa_bribe_response_line",
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
                "hero_main_options",
                "{=SA_BribeReject}My loyalty is worth more than gold...",
                () => !ShouldAcceptBribe(),
                () => ResetConversationState());
        }

        private void RegisterIntelligenceDialogues(CampaignGameStarter starter)
        {
            // Comprehensive intelligence and rumor dialogues implemented in full method below
            // This includes all the myth, legend, and political conversation trees
        }

        private void RegisterAllianceStatusDialogues(CampaignGameStarter starter)
        {
            // Alliance status and management dialogues
        }

        private void RegisterAllianceManagementDialogues(CampaignGameStarter starter)
        {
            // Advanced alliance management features
        }

        private void RegisterAdvancedFeaturesDialogues(CampaignGameStarter starter)
        {
            // Economic warfare, spy operations, joint campaigns
        }

        // Temporary method to complete registration - all dialogue content preserved
        private void RegisterRemainingDialogues(CampaignGameStarter starter)
        {
            // === ALL INTELLIGENCE AND ADVANCED DIALOGUES ===
            
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
                "sa_info_response",
                "sa_rumor_topics",
                "{=SA_InfoResponseRumors}Indeed, there are whispers of secret dealings...",
                () => HasRumorsToShare(),
                null);

            starter.AddDialogLine(
                "sa_info_response_no_rumors",
                "sa_info_response",
                "hero_main_options",
                "{=SA_InfoResponseNoRumors}I know nothing of such matters.",
                () => !HasRumorsToShare(),
                null);

            // --- RUMOR TOPICS ---
            starter.AddPlayerLine(
                "sa_ask_alliances",
                "sa_rumor_topics",
                "sa_alliance_rumors",
                "{=SA_AskAlliances}Tell me about secret alliances.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_ask_myths",
                "sa_rumor_topics",
                "sa_myth_topics",
                "{=SA_AskMyths}Do you know any myths or legends?",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_rumor_nevermind",
                "sa_rumor_topics",
                "hero_main_options",
                "{=SA_RumorNevermind}Perhaps another time.",
                () => true,
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
                "sa_status_display_line",
                "sa_status_display",
                "hero_main_options",
                "{=SA_StatusDisplay}Let me update you on our current understanding...",
                () => true,
                DisplayAllianceStatus);

            // --- ALLIANCE MANAGEMENT ---
            starter.AddPlayerLine(
                "sa_deepen_pact",
                "hero_main_options",
                "sa_pact_options",
                "{=SA_DeepenPact}Perhaps we should deepen our arrangement...",
                () => CanDeepenPact() && !IsAllianceOnCooldown(),
                null,
                100);

            starter.AddPlayerLine(
                "sa_deepen_pact_cooldown",
                "hero_main_options",
                "sa_cooldown_response",
                "{=SA_DeepenPactCooldown}Perhaps we should deepen our arrangement...",
                () => CanDeepenPact() && IsAllianceOnCooldown(),
                null,
                101);

            starter.AddDialogLine(
                "sa_cooldown_response_line",
                "sa_cooldown_response",
                "hero_main_options",
                "{=SA_CooldownResponse}We have spoken of such matters recently. Give it time...",
                () => true,
                null);

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
        }
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
                "sa_rumor_topics",
                "{=SA_InfoResponseRumors}Indeed, there are whispers of secret dealings...",
                () => HasRumorsToShare(),
                null);

            starter.AddDialogLine(
                "sa_info_response_no_rumors",
                "sa_info_response",   // comes from sa_gather_info
                "hero_main_options",
                "{=SA_InfoResponseNoRumors}I know nothing of such matters.",
                () => !HasRumorsToShare(),
                null);

            // --- RUMOR TOPICS ---
            starter.AddPlayerLine(
                "sa_ask_alliances",
                "sa_rumor_topics",
                "sa_alliance_rumors",
                "{=SA_AskAlliances}Tell me about secret alliances.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_ask_myths",
                "sa_rumor_topics",
                "sa_myth_topics",
                "{=SA_AskMyths}Do you know any myths or legends?",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_ask_politics",
                "sa_rumor_topics",
                "sa_political_rumors",
                "{=SA_AskPolitics}What's happening in the political sphere?",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_rumor_nevermind",
                "sa_rumor_topics",
                "hero_main_options",
                "{=SA_RumorNevermind}Perhaps another time.",
                () => true,
                null);

            // --- ALLIANCE RUMORS ---
            starter.AddDialogLine(
                "sa_alliance_rumors_line",
                "sa_alliance_rumors",
                "sa_alliance_detail",
                "{=SA_AllianceRumors}There are whispers of clans working together in shadows...",
                () => true,
                ShareIntelligence);

            starter.AddPlayerLine(
                "sa_alliance_tell_more",
                "sa_alliance_detail",
                "sa_alliance_more_detail",
                "{=SA_AllianceTellMore}Tell me more about these arrangements.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_alliance_enough",
                "sa_alliance_detail",
                "hero_main_options",
                "{=SA_AllianceEnough}That's enough for now.",
                () => true,
                null);

            starter.AddDialogLine(
                "sa_alliance_more_detail_line",
                "sa_alliance_more_detail",
                "hero_main_options",
                "{=SA_AllianceMoreDetail}Some say gold changes hands, others speak of military coordination. Trust is a rare commodity in these times.",
                () => true,
                null);

            // --- MYTH TOPICS ---
            starter.AddPlayerLine(
                "sa_myth_dragons",
                "sa_myth_topics",
                "sa_dragon_tale",
                "{=SA_MythDragons}Tell me about dragons.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_myth_heroes",
                "sa_myth_topics",
                "sa_hero_tale",
                "{=SA_MythHeroes}What tales of ancient heroes do you know?",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_myth_back",
                "sa_myth_topics",
                "sa_rumor_topics",
                "{=SA_MythBack}Let's talk about something else.",
                () => true,
                null);

            // --- DRAGON TALES ---
            starter.AddDialogLine(
                "sa_dragon_tale_line",
                "sa_dragon_tale",
                "sa_dragon_detail",
                "{=SA_DragonTale}Ah, the great wyrms of old... they say they slumber in the deepest mountains.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_dragon_tell_more",
                "sa_dragon_detail",
                "sa_dragon_more_detail",
                "{=SA_DragonTellMore}Tell me more about these dragons.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_dragon_enough",
                "sa_dragon_detail",
                "sa_myth_topics",
                "{=SA_DragonEnough}Interesting. What else do you know?",
                () => true,
                null);

            starter.AddDialogLine(
                "sa_dragon_more_detail_line",
                "sa_dragon_more_detail",
                "sa_myth_topics",
                "{=SA_DragonMoreDetail}Legend speaks of vast hoards and ancient magic. Some believe they still watch over the realm, waiting for the time of greatest need.",
                () => true,
                null);

            // --- HERO TALES ---
            starter.AddDialogLine(
                "sa_hero_tale_line",
                "sa_hero_tale",
                "sa_hero_detail",
                "{=SA_HeroTale}The chronicles speak of warriors who shaped the very foundations of our kingdoms...",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_hero_tell_more",
                "sa_hero_detail",
                "sa_hero_more_detail",
                "{=SA_HeroTellMore}Tell me more about these ancient warriors.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_hero_enough",
                "sa_hero_detail",
                "sa_myth_topics",
                "{=SA_HeroEnough}Fascinating. Any other tales?",
                () => true,
                null);

            starter.AddDialogLine(
                "sa_hero_more_detail_line",
                "sa_hero_more_detail",
                "sa_myth_topics",
                "{=SA_HeroMoreDetail}They fought with valor beyond measure, united kingdoms through both sword and diplomacy. Perhaps their spirit lives on in those who seek to build alliances today.",
                () => true,
                null);

            // --- POLITICAL RUMORS ---
            starter.AddDialogLine(
                "sa_political_rumors_line",
                "sa_political_rumors",
                "sa_political_detail",
                "{=SA_PoliticalRumors}The courts are full of intrigue. Marriages, treaties, and betrayals shape the balance of power.",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_political_tell_more",
                "sa_political_detail",
                "sa_political_more_detail",
                "{=SA_PoliticalTellMore}What specific intrigues have you heard about?",
                () => true,
                null);

            starter.AddPlayerLine(
                "sa_political_enough",
                "sa_political_detail",
                "sa_rumor_topics",
                "{=SA_PoliticalEnough}That's all I need to know for now.",
                () => true,
                null);

            starter.AddDialogLine(
                "sa_political_more_detail_line",
                "sa_political_more_detail",
                "sa_rumor_topics",
                "{=SA_PoliticalMoreDetail}Some lords grow restless with their liege's decisions. Others seek new alliances to secure their position. The wise ruler keeps both ears open and tongue careful.",
                () => true,
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
                "sa_status_display_line",
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
                () => CanDeepenPact() && !IsAllianceOnCooldown(),
                null,
                100);

            // Cooldown version of deepen pact (higher priority when conditions match)
            starter.AddPlayerLine(
                "sa_deepen_pact_cooldown",
                "hero_main_options",
                "sa_cooldown_response",
                "{=SA_DeepenPactCooldown}Perhaps we should deepen our arrangement...",
                () => CanDeepenPact() && IsAllianceOnCooldown(),
                null,
                101); // Higher priority than main deepen pact

            starter.AddDialogLine(
                "sa_cooldown_response_line",
                "sa_cooldown_response",
                "hero_main_options",
                "{=SA_CooldownResponse}We have spoken of such matters recently. Give it time...",
                () => true,
                null);

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
                "sa_dissolve_confirm_line",
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
                "sa_economic_target_line",
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
                "sa_spy_target_line",
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
                "sa_campaign_target_line",
                "sa_campaign_target",
                "hero_main_options",
                "{=SA_CampaignTarget}Our combined forces shall be unstoppable.",
                () => true,
                LaunchJointCampaign);
        }


        // Add conversation tracking to prevent duplicates
        private HashSet<string> _activeDialogueIds = new HashSet<string>();

        private bool IsDialogueActive(string dialogueId)
        {
            return _activeDialogueIds.Contains(dialogueId);
        }

        private void ActivateDialogue(string dialogueId)
        {
            _activeDialogueIds.Add(dialogueId);
        }

        private void DeactivateDialogue(string dialogueId)
        {
            _activeDialogueIds.Remove(dialogueId);
        }

        private void ClearActiveDialogues()
        {
            _activeDialogueIds.Clear();
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

            try
            {
                return _allianceBehavior?.TryGetRumorsForHero(targetHero, out _) ?? false;
            }
            catch (Exception ex)
            {
                AllianceUIHelper.DebugLog($"Error checking rumors for {targetHero.Name}: {ex.Message}");
                return false;
            }
        }

        private bool SecretAllianceClickableCondition(out TextObject explanation)
        {
            var targetHero = Hero.OneToOneConversationHero;
            
            if (targetHero?.Clan == null)
            {
                explanation = new TextObject("{=SA_ClickConditionInvalid}Cannot propose alliance");
                return false;
            }

            if (HasRecentRejection())
            {
                explanation = new TextObject("{=SA_ClickConditionCooldown}We discussed this recently");
                return true;
            }

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

            // Debug logging to help players understand rejections
            if (_currentAllianceEvaluationScore < 65)
            {
                AllianceUIHelper.DebugLog($"Alliance proposal rejected by {targetHero.Clan.Name} (Score: {_currentAllianceEvaluationScore}/100)");
            }
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
                // Add rejection cooldown
                AddRejectionCooldown();

                // Provide helpful feedback to the player about why the alliance was rejected
                string feedbackMessage = GenerateRejectionFeedback(targetHero.Clan, _currentAllianceEvaluationScore);
                InformationManager.DisplayMessage(new InformationMessage(feedbackMessage, Colors.Red));

                // Also log for debugging
                AllianceUIHelper.DebugLog($"Alliance rejected by {targetHero.Clan.Name}: {feedbackMessage}");
            }

            ResetConversationState();
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

            try
            {
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
            catch (Exception ex)
            {
                AllianceUIHelper.DebugLog($"Error sharing intelligence with {targetHero.Name}: {ex.Message}");
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
            return alliance != null && alliance.IsActive; // Always show if alliance exists
        }

        private bool IsAllianceOnCooldown()
        {
            var targetHero = Hero.OneToOneConversationHero;
            if (targetHero?.Clan == null) return false;

            var alliance = _allianceBehavior?.FindAlliance(Clan.PlayerClan, targetHero.Clan);
            return alliance != null && alliance.IsOnCooldown();
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