using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Models
{
    public enum PactType
    {
        Military,
        NonAggression,
        Trade,
        Intelligence
    }

    /// <summary>
    /// Represents a formal pact or treaty between clans
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    [SaveableClass(2)]
    public class Pact
    {
        [SaveableField(1)]
        public string Id { get; set; }

        [SaveableField(2)]
        public PactType Type { get; set; }

        [SaveableField(3)]
        public MBGUID InitiatorClanId { get; set; }

        [SaveableField(4)]
        public MBGUID TargetClanId { get; set; }

        [SaveableField(5)]
        public int DurationInDays { get; set; }

        [SaveableField(6)]
        public CampaignTime StartTime { get; set; }

        [SaveableField(7)]
        public CampaignTime EndTime { get; set; }

        [SaveableField(8)]
        public string Terms { get; set; }

        [SaveableField(9)]
        public int CooldownDays { get; set; }

        [SaveableField(10)]
        public float TrustImpact { get; set; }

        [SaveableField(11)]
        public bool IsActive { get; set; }

        [SaveableField(12)]
        public int ViolationCount { get; set; }

        public Pact()
        {
            IsActive = true;
            TrustImpact = 0.1f;
            ViolationCount = 0;
        }

        public Pact(PactType type, Clan initiator, Clan target, int duration, string terms) : this()
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            InitiatorClanId = initiator?.Id ?? default(MBGUID);
            TargetClanId = target?.Id ?? default(MBGUID);
            DurationInDays = duration;
            Terms = terms;
            StartTime = CampaignTime.Now;
            EndTime = CampaignTime.DaysFromNow(duration);
            
            // Set appropriate cooldown based on pact type
            switch (type)
            {
                case PactType.Military:
                    CooldownDays = 30;
                    TrustImpact = 0.2f;
                    break;
                case PactType.NonAggression:
                    CooldownDays = 15;
                    TrustImpact = 0.1f;
                    break;
                case PactType.Trade:
                    CooldownDays = 20;
                    TrustImpact = 0.15f;
                    break;
                case PactType.Intelligence:
                    CooldownDays = 45;
                    TrustImpact = 0.25f;
                    break;
            }
        }

        public Clan GetInitiatorClan()
        {
            return MBObjectManager.Instance.GetObject<Clan>(c => c.Id == InitiatorClanId);
        }

        public Clan GetTargetClan()
        {
            return MBObjectManager.Instance.GetObject<Clan>(c => c.Id == TargetClanId);
        }

        public bool IsExpired()
        {
            return CampaignTime.Now > EndTime;
        }

        public bool InvolvesClan(Clan clan)
        {
            return clan != null && (InitiatorClanId == clan.Id || TargetClanId == clan.Id);
        }

        public Clan GetOtherClan(Clan clan)
        {
            if (clan == null) return null;
            
            if (InitiatorClanId == clan.Id)
                return GetTargetClan();
            else if (TargetClanId == clan.Id)
                return GetInitiatorClan();
            
            return null;
        }

        public void RecordViolation()
        {
            ViolationCount++;
            TrustImpact -= 0.1f; // Violations reduce trust benefit
        }

        public string GetDisplayName()
        {
            var initiator = GetInitiatorClan();
            var target = GetTargetClan();
            return $"{Type} Pact: {initiator?.Name} ↔ {target?.Name}";
        }

        public string GetStatusDescription()
        {
            if (!IsActive) return "Inactive";
            if (IsExpired()) return "Expired";
            
            var daysRemaining = (EndTime - CampaignTime.Now).ToDays;
            return $"Active ({daysRemaining:F0} days remaining)";
        }
    }
}