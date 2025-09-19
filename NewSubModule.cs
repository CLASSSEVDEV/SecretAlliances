using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using SecretAlliances.Behaviors;

namespace SecretAlliances
{
    /// <summary>
    /// Main SubModule for Secret Alliances mod - rewritten for clean architecture
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// This version eliminates the critical kingdom-switching bug and provides clean UI integration
    /// </summary>
    public class NewSubModule : MBSubModuleBase
    {
        // New clean architecture services
        private AllianceService _allianceService;
        private RequestsBehavior _requestsBehavior;
        private PreBattleAssistBehavior _preBattleAssistBehavior;

        // Legacy behavior for compatibility during transition
        private SecretAllianceBehavior _legacyBehavior;

        // Dialog state tracking
        private int _currentAllianceEvaluationScore = 0;
        private int _currentBribeReceptivity = 0;
        private int _currentBribeAmount = 0;
        private bool _lastPactResult = false;
        private bool _allianceRejected = false;

        protected override void OnGameStart(Game game, IGameStarter starterObject)
        {
            base.OnGameStart(game, starterObject);

            if (starterObject is CampaignGameStarter campaignStarter)
            {
                InitializeBehaviors(campaignStarter);
                RegisterDialogs(campaignStarter);
                RegisterConsoleCommands();

                InformationManager.DisplayMessage(new InformationMessage(
                    "Secret Alliances v2.0 loaded - Kingdom switching bug fixed!", 
                    Color.FromUint(0x0000FF00))); // Green
            }
        }

        private void InitializeBehaviors(CampaignGameStarter campaignStarter)
        {
            // Initialize new clean architecture
            _allianceService = new AllianceService();
            _requestsBehavior = new RequestsBehavior(_allianceService);
            _preBattleAssistBehavior = new PreBattleAssistBehavior(_allianceService, _requestsBehavior);

            // Add new behaviors
            campaignStarter.AddBehavior(_allianceService);
            campaignStarter.AddBehavior(_requestsBehavior);
            campaignStarter.AddBehavior(_preBattleAssistBehavior);

            // Keep legacy behavior for compatibility (but with fixes applied)
            _legacyBehavior = new SecretAllianceBehavior();
            campaignStarter.AddBehavior(_legacyBehavior);

            Debug.Print("[SecretAlliances] New architecture behaviors initialized");
        }

        private void RegisterConsoleCommands()
        {
            // Register console commands for debugging
            ConsoleCommands.RegisterCommands();
        }

        private void RegisterDialogs(CampaignGameStarter starter)
        {
            // Register essential dialog lines for secret alliance system
            AddCoreDialogs(starter);
            AddAllianceProposalDialogs(starter);
            AddAssistanceRequestDialogs(starter);
        }

        private void AddCoreDialogs(CampaignGameStarter starter)
        {
            // Main entry point for secret alliance conversations
            starter.AddPlayerLine(
                "sa_main_propose",
                "lord_talk_speak_diplomacy_2",
                "sa_alliance_proposal",
                "{=SA_ProposeAlliance}I'd like to discuss a... private arrangement between our clans.",
                CanProposeSecretAlliance,
                null);

            // Alliance proposal response
            starter.AddDialogLine(
                "sa_alliance_proposal_response",
                "sa_alliance_proposal",
                "sa_alliance_decision",
                "{=SA_AllianceProposalResponse}A secret alliance? That's a dangerous proposition. What do you have in mind?",
                null,
                EvaluateAllianceProposal);

            // Acceptance path
            starter.AddDialogLine(
                "sa_alliance_accept",
                "sa_alliance_decision",
                "lord_pretalk",
                "{=SA_AllianceAccept}Very well. Our clans shall coordinate in secret. I trust this arrangement will benefit us both.",
                ShouldAcceptAlliance,
                AcceptAllianceProposal);

            // Rejection path
            starter.AddDialogLine(
                "sa_alliance_reject",
                "sa_alliance_decision",
                "lord_pretalk",
                "{=SA_AllianceReject}The risks are too great. I must decline your proposal at this time.",
                () => !ShouldAcceptAlliance(),
                RejectAllianceProposal);
        }

        private void AddAllianceProposalDialogs(CampaignGameStarter starter)
        {
            // View existing alliance status
            starter.AddPlayerLine(
                "sa_view_alliance",
                "lord_talk_speak_diplomacy_2",
                "sa_alliance_status",
                "{=SA_ViewAlliance}How stands our secret arrangement?",
                CanViewAllianceStatus,
                DisplayAllianceStatus);

            // Leave alliance option
            starter.AddPlayerLine(
                "sa_leave_alliance",
                "sa_alliance_status",
                "sa_confirm_leave",
                "{=SA_LeaveAlliance}I think it's time we ended our arrangement.",
                CanLeaveAlliance,
                null);

            // Confirm leaving
            starter.AddPlayerLine(
                "sa_confirm_leave_yes",
                "sa_confirm_leave",
                "lord_pretalk",
                "{=SA_ConfirmLeaveYes}Yes, our alliance has served its purpose.",
                null,
                LeaveAlliance);

            starter.AddPlayerLine(
                "sa_confirm_leave_no",
                "sa_confirm_leave",
                "sa_alliance_status",
                "{=SA_ConfirmLeaveNo}On second thought, let's continue our arrangement.",
                null,
                null);
        }

        private void AddAssistanceRequestDialogs(CampaignGameStarter starter)
        {
            // Request battle assistance
            starter.AddPlayerLine(
                "sa_request_battle_aid",
                "lord_talk_speak_diplomacy_2",
                "sa_battle_aid_response",
                "{=SA_RequestBattleAid}I may need your assistance in an upcoming battle.",
                CanRequestBattleAssistance,
                null);

            // Response to battle aid request
            starter.AddDialogLine(
                "sa_battle_aid_consider",
                "sa_battle_aid_response",
                "sa_battle_aid_decision",
                "{=SA_BattleAidConsider}That depends on the circumstances. Tell me more.",
                null,
                EvaluateBattleAidRequest);

            // Accept aid request
            starter.AddDialogLine(
                "sa_battle_aid_accept",
                "sa_battle_aid_decision",
                "lord_pretalk",
                "{=SA_BattleAidAccept}I'll come to your aid if I can manage it discretely.",
                ShouldAcceptBattleAid,
                AcceptBattleAidRequest);

            // Decline aid request
            starter.AddDialogLine(
                "sa_battle_aid_decline",
                "sa_battle_aid_decision",
                "lord_pretalk",
                "{=SA_BattleAidDecline}I'm afraid I cannot risk exposure at this time.",
                () => !ShouldAcceptBattleAid(),
                DeclineBattleAidRequest);
        }

        #region Dialog Condition Methods

        private bool CanProposeSecretAlliance()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            return _allianceService?.CanProposeAlliance(Hero.MainHero.Clan, targetClan) == true;
        }

        private bool CanViewAllianceStatus()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            return _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan) != null;
        }

        private bool CanLeaveAlliance()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            return alliance != null && alliance.IsActive;
        }

        private bool CanRequestBattleAssistance()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            return alliance != null && alliance.IsActive && alliance.TrustLevel > 0.3f;
        }

        private bool ShouldAcceptAlliance()
        {
            // Use the evaluation score calculated during proposal
            return _currentAllianceEvaluationScore > 50; // Threshold for acceptance
        }

        private bool ShouldAcceptBattleAid()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            
            if (alliance == null) return false;
            
            // Higher trust = more likely to help
            var acceptanceChance = alliance.TrustLevel * 0.8f + 0.2f;
            return MBRandom.RandomFloat < acceptanceChance;
        }

        #endregion

        #region Dialog Consequence Methods

        private void EvaluateAllianceProposal()
        {
            var playerClan = Hero.MainHero.Clan;
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            
            if (playerClan != null && targetClan != null)
            {
                _currentAllianceEvaluationScore = CalculateAllianceAcceptanceScore(playerClan, targetClan);
                ResetConversationState();
            }
        }

        private void AcceptAllianceProposal()
        {
            var playerClan = Hero.MainHero.Clan;
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            
            if (playerClan != null && targetClan != null && _allianceService != null)
            {
                var allianceName = $"Alliance of {playerClan.Name} and {targetClan.Name}";
                var alliance = _allianceService.CreateAlliance(
                    allianceName, 
                    new List<Clan> { playerClan, targetClan }, 
                    playerClan);
                
                if (alliance != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Secret alliance formed with {targetClan.Name}!",
                        Color.FromUint(0x0000FF00))); // Green
                }
            }
            
            ResetConversationState();
        }

        private void RejectAllianceProposal()
        {
            _allianceRejected = true;
            
            InformationManager.DisplayMessage(new InformationMessage(
                $"{Hero.OneToOneConversationHero?.Name} has declined your alliance proposal",
                Colors.Yellow));
            
            ResetConversationState();
        }

        private void DisplayAllianceStatus()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            
            if (alliance != null)
            {
                var statusText = $"Alliance Status:\n" +
                               $"Trust Level: {alliance.TrustLevel:P0}\n" +
                               $"Secrecy Level: {alliance.SecrecyLevel:P0}\n" +
                               $"Members: {alliance.GetMemberClans().Count}";
                
                InformationManager.ShowInquiry(
                    new InquiryData(
                        alliance.Name,
                        statusText,
                        true, false,
                        "Close", null,
                        null, null));
            }
        }

        private void LeaveAlliance()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            
            if (alliance != null)
            {
                _allianceService.LeaveAlliance(alliance, Hero.MainHero.Clan);
            }
        }

        private void EvaluateBattleAidRequest()
        {
            // Prepare for battle aid decision
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            var alliance = _allianceService?.GetAlliance(Hero.MainHero.Clan, targetClan);
            
            if (alliance != null && _requestsBehavior != null)
            {
                // This would typically create a formal request, but for dialog we'll handle it directly
            }
        }

        private void AcceptBattleAidRequest()
        {
            var targetClan = Hero.OneToOneConversationHero?.Clan;
            
            if (targetClan != null && _requestsBehavior != null)
            {
                var request = _requestsBehavior.CreateAssistanceRequest(
                    Hero.MainHero.Clan, 
                    targetClan, 
                    Models.RequestType.BattleAssistance,
                    "Battle assistance request from alliance partner");
                
                if (request != null)
                {
                    _requestsBehavior.AcceptRequest(request);
                }
            }
        }

        private void DeclineBattleAidRequest()
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "Battle assistance request declined",
                Colors.Yellow));
        }

        #endregion

        #region Helper Methods

        private int CalculateAllianceAcceptanceScore(Clan playerClan, Clan targetClan)
        {
            int score = 50; // Base score

            // Relationship bonus/penalty
            if (playerClan.Leader != null && targetClan.Leader != null)
            {
                var relationship = playerClan.Leader.GetRelation(targetClan.Leader);
                score += relationship / 2; // Convert relation (-100 to +100) to score modifier
            }

            // Power balance considerations
            var powerRatio = (float)playerClan.TotalStrength / (playerClan.TotalStrength + targetClan.TotalStrength);
            if (powerRatio > 0.3f && powerRatio < 0.7f) // Balanced power
            {
                score += 20;
            }
            else if (powerRatio < 0.3f) // Player much weaker
            {
                score -= 15;
            }

            // Kingdom considerations
            if (playerClan.Kingdom == targetClan.Kingdom)
            {
                score += 10; // Same kingdom = easier
            }
            else if (playerClan.Kingdom != null && targetClan.Kingdom != null &&
                     playerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
            {
                score -= 30; // At war = much harder
            }

            // Trait considerations for target clan leader
            if (targetClan.Leader != null)
            {
                var leader = targetClan.Leader;
                if (leader.GetTraitLevel(DefaultTraits.Honor) > 0)
                    score += 15;
                if (leader.GetTraitLevel(DefaultTraits.Calculating) > 0)
                    score += 20;
                if (leader.GetTraitLevel(DefaultTraits.Generosity) > 0)
                    score += 10;
                if (leader.GetTraitLevel(DefaultTraits.Mercy) < 0)
                    score -= 10;
            }

            return MathF.Max(0, MathF.Min(100, score));
        }

        private void ResetConversationState()
        {
            _currentAllianceEvaluationScore = 0;
            _currentBribeReceptivity = 0;
            _currentBribeAmount = 0;
            _lastPactResult = false;
            _allianceRejected = false;
        }

        #endregion
    }
}