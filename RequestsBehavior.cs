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
    /// Manages assistance requests between allied clans
    /// Handles the ask/offer battle assistance flow with proper UI integration
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public class RequestsBehavior : CampaignBehaviorBase
    {
        private List<Request> _requests = new List<Request>();
        private readonly AllianceService _allianceService;

        public RequestsBehavior(AllianceService allianceService)
        {
            _allianceService = allianceService;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SecretAlliances_Requests", ref _requests);
        }

        private void OnDailyTick()
        {
            ProcessRequestExpiry();
            GenerateAIRequests();
        }

        private void OnHourlyTick()
        {
            // Check for urgent battle assistance requests
            ProcessUrgentRequests();
        }

        #region Public API Methods

        /// <summary>
        /// Add a new request to the system
        /// </summary>
        public void AddRequest(Request request)
        {
            if (request == null) return;

            _requests.Add(request);

            // Notify target clan if it's the player
            if (request.GetTargetClan() == Hero.MainHero.Clan)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"New assistance request from {request.GetRequesterClan()?.Name}: {request.Type}",
                    Color.FromUint(0x00F16D26)));
            }
        }

        /// <summary>
        /// Get all pending requests for a specific clan
        /// </summary>
        public List<Request> GetPendingRequestsForClan(Clan clan)
        {
            if (clan == null) return new List<Request>();

            return _requests.Where(r => r.GetTargetClan() == clan && r.IsPending()).ToList();
        }

        /// <summary>
        /// Get all requests sent by a specific clan
        /// </summary>
        public List<Request> GetRequestsSentByClan(Clan clan)
        {
            if (clan == null) return new List<Request>();

            return _requests.Where(r => r.GetRequesterClan() == clan).ToList();
        }

        /// <summary>
        /// Get all active requests (not expired or completed)
        /// </summary>
        public List<Request> GetActiveRequests()
        {
            return _requests.Where(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Accepted).ToList();
        }

        /// <summary>
        /// Accept a request
        /// </summary>
        public bool AcceptRequest(Request request, string playerReason = "")
        {
            if (request == null || !request.IsPending())
                return false;

            request.Accept();

            // Apply immediate effects
            ApplyRequestAcceptanceEffects(request);

            // Notify requester
            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            InformationManager.DisplayMessage(new InformationMessage(
                $"{target?.Name} has agreed to assist with {request.Type}!",
                Color.FromUint(0x0000FF00))); // Green

            return true;
        }

        /// <summary>
        /// Decline a request with optional reason
        /// </summary>
        public bool DeclineRequest(Request request, string reason = "Unable to assist at this time")
        {
            if (request == null || !request.IsPending())
                return false;

            request.Decline(reason);

            // Apply relationship penalties for declining
            ApplyRequestDeclineEffects(request);

            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            InformationManager.DisplayMessage(new InformationMessage(
                $"{target?.Name} has declined to assist with {request.Type}",
                Colors.Yellow));

            return true;
        }

        /// <summary>
        /// Create a new assistance request between allied clans
        /// </summary>
        public Request CreateAssistanceRequest(Clan requester, Clan target, RequestType type, string description, int reward = 0)
        {
            if (requester == null || target == null)
                return null;

            // Check if clans are allied
            var alliance = _allianceService.GetAlliance(requester, target);
            if (alliance == null)
                return null;

            var request = new Request(type, requester, target, description, reward);
            AddRequest(request);

            return request;
        }

        /// <summary>
        /// Mark a request as fulfilled
        /// </summary>
        public void FulfillRequest(Request request)
        {
            if (request == null || request.Status != RequestStatus.Accepted)
                return;

            request.MarkFulfilled();
            ApplyRequestFulfillmentRewards(request);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Assistance request completed successfully: {request.Type}",
                Color.FromUint(0x0000FF00))); // Green
        }

        /// <summary>
        /// Mark a request as failed
        /// </summary>
        public void FailRequest(Request request)
        {
            if (request == null || request.Status != RequestStatus.Accepted)
                return;

            request.MarkFailed();
            ApplyRequestFailurePenalties(request);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Assistance request failed: {request.Type}",
                Colors.Red));
        }

        #endregion

        #region Private Methods

        private void ProcessRequestExpiry()
        {
            var expiredRequests = _requests.Where(r => r.IsExpired()).ToList();

            foreach (var request in expiredRequests)
            {
                request.CheckExpiry();

                if (request.Status == RequestStatus.Expired)
                {
                    // Apply minor relationship penalty for expired requests
                    var requester = request.GetRequesterClan();
                    var target = request.GetTargetClan();

                    if (requester?.Leader != null && target?.Leader != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(requester.Leader, target.Leader, -2);
                    }
                }
            }
        }

        private void ProcessUrgentRequests()
        {
            // Handle time-sensitive requests like battle assistance
            var urgentRequests = _requests.Where(r =>
                r.IsPending() &&
                (r.Type == RequestType.BattleAssistance || r.Type == RequestType.SiegeAssistance) &&
                (r.ExpiryTime - CampaignTime.Now).ToHours < 2).ToList();

            foreach (var request in urgentRequests)
            {
                // Send additional notifications for urgent requests
                if (request.GetTargetClan() == Hero.MainHero.Clan)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"URGENT: {request.GetRequesterClan()?.Name} needs immediate assistance!",
                        Colors.Red));
                }
            }
        }

        private void GenerateAIRequests()
        {
            // Generate AI-driven requests based on current game state
            if (MBRandom.RandomFloat > 0.1f) return; // 10% chance daily

            var alliances = _allianceService.GetAllActiveAlliances();

            foreach (var alliance in alliances)
            {
                var members = alliance.GetMemberClans();

                foreach (var member in members)
                {
                    if (member == Hero.MainHero.Clan) continue; // Don't auto-generate for player

                    // Check if this clan might need assistance
                    if (ShouldClanRequestAssistance(member))
                    {
                        var potentialTarget = GetBestAssistanceTarget(member, members);
                        if (potentialTarget != null)
                        {
                            GenerateAIRequest(member, potentialTarget, alliance);
                        }
                    }
                }
            }
        }

        private bool ShouldClanRequestAssistance(Clan clan)
        {
            if (clan?.Leader == null) return false;

            // Factors that make a clan more likely to request help:
            // - Low gold
            // - At war
            // - Weak military
            // - Under siege

            var shouldRequest = false;

            // Financial pressure
            if (clan.Gold < 10000)
                shouldRequest = true;

            // Military pressure
            if (clan.Kingdom?.IsAtWarWith(Hero.MainHero.Clan.Kingdom) == true)
                shouldRequest = true;

            // Weak position
            if (clan.TotalStrength < 500)
                shouldRequest = true;

            return shouldRequest && MBRandom.RandomFloat < 0.3f; // 30% chance if conditions met
        }

        private Clan GetBestAssistanceTarget(Clan requester, List<Clan> potentialTargets)
        {
            return potentialTargets
                .Where(c => c != requester && c != null && !c.IsEliminated)
                .OrderByDescending(c => CalculateAssistanceViability(requester, c))
                .FirstOrDefault();
        }

        private float CalculateAssistanceViability(Clan requester, Clan target)
        {
            if (requester?.Leader == null || target?.Leader == null)
                return 0f;

            float score = 0f;

            // Relationship bonus
            score += requester.Leader.GetRelation(target.Leader) / 100f;

            // Target's capability
            score += target.TotalStrength / 1000f;

            // Target's wealth (can they afford to help)
            score += target.Gold / 50000f;

            
            

            return score;
        }

        private void GenerateAIRequest(Clan requester, Clan target, Alliance alliance)
        {
            var requestTypes = new[] { RequestType.Tribute, RequestType.TradeConvoyEscort, RequestType.Intelligence };
            var type = requestTypes[MBRandom.RandomInt(requestTypes.Length)];

            var description = GenerateRequestDescription(type, requester, target);
            var reward = CalculateAIRequestReward(type, requester);

            var request = new Request(type, requester, target, description, reward);
            AddRequest(request);
        }

        private string GenerateRequestDescription(RequestType type, Clan requester, Clan target)
        {
            switch (type)
            {
                case RequestType.Tribute:
                    return $"{requester.Name} requests financial assistance to maintain their military strength.";
                case RequestType.TradeConvoyEscort:
                    return $"{requester.Name} needs protection for their trade caravans passing through dangerous territory.";
                case RequestType.Intelligence:
                    return $"{requester.Name} seeks information about enemy troop movements and plans.";
                default:
                    return $"{requester.Name} requests assistance with {type}.";
            }
        }

        private int CalculateAIRequestReward(RequestType type, Clan requester)
        {
            var baseReward = 200;

            switch (type)
            {
                case RequestType.BattleAssistance:
                    return baseReward * 3;
                case RequestType.SiegeAssistance:
                    return baseReward * 4;
                case RequestType.Tribute:
                    return 0; // Tribute is the request, not a reward
                case RequestType.Intelligence:
                    return baseReward;
                default:
                    return baseReward;
            }
        }

        private void ApplyRequestAcceptanceEffects(Request request)
        {
            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            if (requester?.Leader != null && target?.Leader != null)
            {
                // Improve relationship for accepting
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(requester.Leader, target.Leader, 5);

                // Improve alliance trust
                var alliance = _allianceService.GetAlliance(requester, target);
                if (alliance != null)
                {
                    alliance.TrustLevel += 0.02f;
                    alliance.AddHistoryEntry($"{target.Name} accepted assistance request from {requester.Name}");
                }
            }
        }

        private void ApplyRequestDeclineEffects(Request request)
        {
            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            if (requester?.Leader != null && target?.Leader != null)
            {
                // Slight relationship penalty for declining
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(requester.Leader, target.Leader, -3);

                // Slight trust reduction
                var alliance = _allianceService.GetAlliance(requester, target);
                if (alliance != null)
                {
                    alliance.TrustLevel -= 0.01f;
                    alliance.AddHistoryEntry($"{target.Name} declined assistance request from {requester.Name}");
                }
            }
        }

        private void ApplyRequestFulfillmentRewards(Request request)
        {
            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            // Pay reward
            if (request.ProposedReward > 0 && requester?.Leader != null && target?.Leader != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(requester.Leader, target.Leader, request.ProposedReward, false);
            }

            // Relationship bonus
            if (requester?.Leader != null && target?.Leader != null)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(requester.Leader, target.Leader, 10);

                var alliance = _allianceService.GetAlliance(requester, target);
                if (alliance != null)
                {
                    alliance.TrustLevel += 0.05f;
                    alliance.AddHistoryEntry($"{target.Name} successfully fulfilled assistance request");
                }
            }
        }

        private void ApplyRequestFailurePenalties(Request request)
        {
            var requester = request.GetRequesterClan();
            var target = request.GetTargetClan();

            if (requester?.Leader != null && target?.Leader != null)
            {
                // Relationship penalty for failing
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(requester.Leader, target.Leader, -8);

                var alliance = _allianceService.GetAlliance(requester, target);
                if (alliance != null)
                {
                    alliance.TrustLevel -= 0.03f;
                    alliance.AddHistoryEntry($"{target.Name} failed to fulfill assistance request");
                }
            }
        }

        #endregion
    }
}