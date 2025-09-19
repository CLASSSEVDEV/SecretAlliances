using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Models
{
    public enum RequestType
    {
        BattleAssistance,
        SiegeAssistance,
        RaidAssistance,
        TradeConvoyEscort,
        Sabotage,
        Intelligence,
        Tribute
    }

    public enum RequestStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired,
        Fulfilled,
        Failed
    }

    /// <summary>
    /// Represents a request for assistance between allied clans
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    [SaveableClass(3)]
    public class Request
    {
        [SaveableField(1)]
        public string Id { get; set; }

        [SaveableField(2)]
        public RequestType Type { get; set; }

        [SaveableField(3)]
        public MBGUID RequesterClanId { get; set; }

        [SaveableField(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableField(5)]
        public CampaignTime CreationTime { get; set; }

        [SaveableField(6)]
        public CampaignTime ExpiryTime { get; set; }

        [SaveableField(7)]
        public RequestStatus Status { get; set; }

        [SaveableField(8)]
        public string Description { get; set; }

        [SaveableField(9)]
        public int ProposedReward { get; set; }

        [SaveableField(10)]
        public float RiskLevel { get; set; }

        [SaveableField(11)]
        public MBGUID TargetSettlementId { get; set; }

        [SaveableField(12)]
        public MBGUID TargetPartyId { get; set; }

        [SaveableField(13)]
        public string DeclineReason { get; set; }

        [SaveableField(14)]
        public CampaignTime ResponseTime { get; set; }

        public Request()
        {
            Status = RequestStatus.Pending;
            RiskLevel = 0.3f;
        }

        public Request(RequestType type, Clan requester, Clan target, string description, int reward = 0) : this()
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            RequesterClanId = requester?.Id ?? default(MBGUID);
            TargetClanId = target?.Id ?? default(MBGUID);
            Description = description;
            ProposedReward = reward;
            CreationTime = CampaignTime.Now;
            
            // Set expiry based on request type
            switch (type)
            {
                case RequestType.BattleAssistance:
                    ExpiryTime = CampaignTime.HoursFromNow(6); // Battle requests expire quickly
                    RiskLevel = 0.7f;
                    break;
                case RequestType.SiegeAssistance:
                    ExpiryTime = CampaignTime.DaysFromNow(3);
                    RiskLevel = 0.8f;
                    break;
                case RequestType.RaidAssistance:
                    ExpiryTime = CampaignTime.HoursFromNow(12);
                    RiskLevel = 0.6f;
                    break;
                case RequestType.TradeConvoyEscort:
                    ExpiryTime = CampaignTime.DaysFromNow(7);
                    RiskLevel = 0.3f;
                    break;
                case RequestType.Sabotage:
                    ExpiryTime = CampaignTime.DaysFromNow(5);
                    RiskLevel = 0.9f;
                    break;
                case RequestType.Intelligence:
                    ExpiryTime = CampaignTime.DaysFromNow(10);
                    RiskLevel = 0.4f;
                    break;
                case RequestType.Tribute:
                    ExpiryTime = CampaignTime.DaysFromNow(14);
                    RiskLevel = 0.2f;
                    break;
            }
        }

        public Clan GetRequesterClan()
        {
            return MBObjectManager.Instance.GetObject<Clan>(c => c.Id == RequesterClanId);
        }

        public Clan GetTargetClan()
        {
            return MBObjectManager.Instance.GetObject<Clan>(c => c.Id == TargetClanId);
        }

        public Settlement GetTargetSettlement()
        {
            if (TargetSettlementId == default(MBGUID)) return null;
            return MBObjectManager.Instance.GetObject<Settlement>(s => s.Id == TargetSettlementId);
        }

        public bool IsExpired()
        {
            return CampaignTime.Now > ExpiryTime;
        }

        public bool IsPending()
        {
            return Status == RequestStatus.Pending && !IsExpired();
        }

        public void Accept()
        {
            if (IsPending())
            {
                Status = RequestStatus.Accepted;
                ResponseTime = CampaignTime.Now;
            }
        }

        public void Decline(string reason = "")
        {
            if (IsPending())
            {
                Status = RequestStatus.Declined;
                DeclineReason = reason;
                ResponseTime = CampaignTime.Now;
            }
        }

        public void MarkFulfilled()
        {
            if (Status == RequestStatus.Accepted)
            {
                Status = RequestStatus.Fulfilled;
            }
        }

        public void MarkFailed()
        {
            if (Status == RequestStatus.Accepted)
            {
                Status = RequestStatus.Failed;
            }
        }

        public void CheckExpiry()
        {
            if (Status == RequestStatus.Pending && IsExpired())
            {
                Status = RequestStatus.Expired;
            }
        }

        public string GetDisplayName()
        {
            var requester = GetRequesterClan();
            var target = GetTargetClan();
            return $"{Type}: {requester?.Name} → {target?.Name}";
        }

        public string GetStatusDescription()
        {
            switch (Status)
            {
                case RequestStatus.Pending:
                    var timeLeft = ExpiryTime - CampaignTime.Now;
                    if (timeLeft.ToDays >= 1)
                        return $"Pending ({timeLeft.ToDays:F0} days left)";
                    else
                        return $"Pending ({timeLeft.ToHours:F0} hours left)";
                case RequestStatus.Accepted:
                    return "Accepted - Awaiting completion";
                case RequestStatus.Declined:
                    return $"Declined{(string.IsNullOrEmpty(DeclineReason) ? "" : $": {DeclineReason}")}";
                case RequestStatus.Expired:
                    return "Expired";
                case RequestStatus.Fulfilled:
                    return "Successfully completed";
                case RequestStatus.Failed:
                    return "Failed to complete";
                default:
                    return Status.ToString();
            }
        }

        public int GetEstimatedReward()
        {
            var baseReward = ProposedReward;
            var riskMultiplier = 1f + RiskLevel;
            return (int)(baseReward * riskMultiplier);
        }
    }
}