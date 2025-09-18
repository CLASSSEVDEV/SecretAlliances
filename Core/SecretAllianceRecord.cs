using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Core
{
    [Serializable] // give it a unique id for your mod
    public class SecretAllianceRecord
    {
        public SecretAllianceRecord()
        {
            Loyalists = new List<MBGUID>();
            Informants = new List<MBGUID>();

            // Initialize new fields with default values
            TradePact = false;
            MilitaryPact = false;
            GroupId = 0;
            LastInteractionDay = 0;
            CooldownDays = 0;

            // Initialize expanded functionality fields
            LastOperationDay = 0;
            PendingOperationType = 0;
            GroupSecrecyCache = 0f;
            GroupStrengthCache = 0f;

            // Initialize mega feature expansion fields
            NextEligibleOperationDay = 0;
            BetrayalCooldownDays = 0;
            CounterIntelBuffExpiryDay = 0;
            BetrayalEscalationCounter = 0f;

            // Initialize advanced alliance features
            AllianceRank = 0;
            ContractExpiryDay = 0;
            ReputationScore = 0.5f;
            JointCampaignCount = 0;
            DiplomaticImmunity = false;
            EconomicIntegration = 0f;
            SpyNetworkLevel = 0;
            MarriageAlliance = false;
            TerritoryAgreementType = 0;
            MilitaryCoordination = 0f;
        }

        [SaveableField(1)] public MBGUID InitiatorClanId;
        [SaveableField(2)] public MBGUID TargetClanId;

        [SaveableField(3)] public float Strength;
        [SaveableField(4)] public float Secrecy;
        [SaveableField(5)] public float BribeAmount;
        [SaveableField(6)] public bool IsActive;
        [SaveableField(7)] public int CreatedGameDay;

        [SaveableField(8)] public float TrustLevel;
        [SaveableField(9)] public float RiskTolerance;
        [SaveableField(10)] public List<MBGUID> Loyalists;
        [SaveableField(11)] public List<MBGUID> Informants;

        [SaveableField(12)] public float EconomicIncentive;
        [SaveableField(13)] public float PoliticalPressure;
        [SaveableField(14)] public float MilitaryAdvantage;
        [SaveableField(15)] public bool HasCommonEnemies;

        [SaveableField(16)] public int LeakAttempts;
        [SaveableField(17)] public int DaysWithoutLeak;
        [SaveableField(18)] public float LastLeakSeverity;

        [SaveableField(19)] public bool CoupAttempted;
        [SaveableField(20)] public bool BetrayalRevealed;
        [SaveableField(21)] public int SuccessfulOperations;

        // New fields for enhanced alliance features
        [SaveableField(22)] public MBGUID UniqueId;
        [SaveableField(23)] public int LastInteractionDay;
        [SaveableField(24)] public int CooldownDays;
        [SaveableField(25)] public bool TradePact;
        [SaveableField(26)] public bool MilitaryPact;
        [SaveableField(27)] public int GroupId;

        // New fields for expanded functionality (append only for save compatibility)
        [SaveableField(28)] public int LastOperationDay;
        [SaveableField(29)] public int PendingOperationType;
        [SaveableField(30)] public float GroupSecrecyCache;
        [SaveableField(31)] public float GroupStrengthCache;

        // Additional fields for mega feature expansion (indices 32+)
        [SaveableField(32)] public int NextEligibleOperationDay;
        [SaveableField(33)] public int BetrayalCooldownDays;
        [SaveableField(34)] public int CounterIntelBuffExpiryDay;
        [SaveableField(35)] public float BetrayalEscalationCounter;

        // Advanced alliance features (indices 36+)
        [SaveableField(36)] public int AllianceRank; // 0=Basic, 1=Advanced, 2=Strategic
        [SaveableField(37)] public int ContractExpiryDay; // Time-limited contracts
        [SaveableField(38)] public float ReputationScore; // Alliance reliability
        [SaveableField(39)] public int JointCampaignCount; // Number of joint military actions
        [SaveableField(40)] public bool DiplomaticImmunity; // Protection from hostile actions
        [SaveableField(41)] public float EconomicIntegration; // Trade network strength
        [SaveableField(42)] public int SpyNetworkLevel; // Intelligence network tier
        [SaveableField(43)] public bool MarriageAlliance; // Royal marriage connection
        [SaveableField(44)] public int TerritoryAgreementType; // Border/expansion agreements
        [SaveableField(45)] public float MilitaryCoordination; // Battle cooperation level



        public Clan GetInitiatorClan()
            => MBObjectManager.Instance.GetObject<Clan>(c => c.Id == InitiatorClanId);

        public Clan GetTargetClan()
            => MBObjectManager.Instance.GetObject<Clan>(c => c.Id == TargetClanId);

        public bool IsValidAlliance()
            => GetInitiatorClan() != null && GetTargetClan() != null && IsActive;

        public bool IsOnCooldown()
            => CampaignTime.Now.GetDayOfYear < LastInteractionDay + CooldownDays;
    }

    [Serializable]
    public class AllianceIntelligence
    {
        [SaveableField(1)] public MBGUID AllianceId;
        [SaveableField(2)] public MBGUID InformerHeroId;
        [SaveableField(3)] public float ReliabilityScore;
        [SaveableField(4)] public int DaysOld;
        [SaveableField(5)] public bool IsConfirmed;
        [SaveableField(6)] public float SeverityLevel;

        // New fields for proper clan pair tracking (append only for save compatibility)
        [SaveableField(7)] public MBGUID ClanAId;
        [SaveableField(8)] public MBGUID ClanBId;
        [SaveableField(9)] public int IntelCategory;

        public Hero GetInformer()
            => MBObjectManager.Instance.GetObject<Hero>(h => h.Id == InformerHeroId);
    }

    // Enum for intelligence categorization (mapped to int for save safety)
    public enum AllianceIntelType
    {
        General = 0,
        TradePactEvidence = 1,
        MilitaryCoordination = 2,
        SecretMeeting = 3,
        BetrayalPlot = 4,
        Coup = 5,
        Military = 6,
        Financial = 7,
        Recruitment = 8,
        Trade = 9
    }

    // Helper class for trade transfer tracking (now persisted)
    [Serializable]
    public class TradeTransferRecord
    {
        [SaveableField(1)] public int Day;
        [SaveableField(2)] public int Amount;
        [SaveableField(3)] public MBGUID FromClan;
        [SaveableField(4)] public MBGUID ToClan;
        [SaveableField(5)] public int TransferType; // 0=Gold, 1=Goods, 2=Services
        [SaveableField(6)] public bool IsCovert;

        public TradeTransferRecord()
        {
            Day = 0;
            Amount = 0;
            FromClan = default(MBGUID);
            ToClan = default(MBGUID);
            TransferType = 0;
            IsCovert = false;
        }
    }

    // Operation types enumeration
    public enum OperationType
    {
        None = 0,
        CovertAid = 1,
        SpyProbe = 2,
        RecruitmentFeelers = 3,
        SabotageRaid = 4,
        CounterIntelligence = 5,
        // Advanced operations
        DiplomaticMission = 6,
        EconomicWarfare = 7,
        InformationNetworking = 8,
        JointCampaign = 9,
        MarriageNegotiation = 10,
        TerritoryNegotiation = 11,
        TradeNetworkExpansion = 12,
        MilitaryTraining = 13,
        RoyalInfluence = 14,
        CulturalExchange = 15
    }

    // Alliance contract types
    [Serializable]
    public class AllianceContract
    {
        [SaveableField(1)] public MBGUID AllianceId;
        [SaveableField(2)] public int ContractType; // 0=Mutual Defense, 1=Trade, 2=Military Support, etc.
        [SaveableField(3)] public int ExpirationDay;
        [SaveableField(4)] public float ContractValue; // Gold or resource value
        [SaveableField(5)] public bool AutoRenew;
        [SaveableField(6)] public int ViolationCount;
        [SaveableField(7)] public float PenaltyClause;
        [SaveableField(8)] public List<MBGUID> WitnessClans;

        public AllianceContract()
        {
            WitnessClans = new List<MBGUID>();
        }

        public bool IsExpired() => CampaignTime.Now.GetDayOfYear >= ExpirationDay;
        public bool IsValid() => !IsExpired() && ViolationCount < 3;
    }

    // Military coordination data
    [Serializable]
    public class MilitaryCoordinationData
    {
        [SaveableField(1)] public MBGUID AllianceId;
        [SaveableField(2)] public int CoordinationLevel; // 0-5 coordination tiers
        [SaveableField(3)] public List<MBGUID> SharedFormations;
        [SaveableField(4)] public int LastJointBattleDay;
        [SaveableField(5)] public float CombatEfficiencyBonus;
        [SaveableField(6)] public bool EliteUnitExchange;
        [SaveableField(7)] public int FortressNetworkAccess;

        public MilitaryCoordinationData()
        {
            SharedFormations = new List<MBGUID>();
        }
    }

    // Economic network data
    [Serializable]
    public class EconomicNetworkData
    {
        [SaveableField(1)] public MBGUID AllianceId;
        [SaveableField(2)] public float TradeVolumeMultiplier;
        [SaveableField(3)] public List<MBGUID> SharedRoutes;
        [SaveableField(4)] public int CaravanProtectionLevel;
        [SaveableField(5)] public float ResourceSharingRatio;
        [SaveableField(6)] public bool PriceManipulationAccess;
        [SaveableField(7)] public int EconomicWarfareCapability;

        public EconomicNetworkData()
        {
            SharedRoutes = new List<MBGUID>();
        }
    }

    // Spy network data
    [Serializable]
    public class SpyNetworkData
    {
        [SaveableField(1)] public MBGUID AllianceId;
        [SaveableField(2)] public int NetworkTier; // 1-5 network sophistication
        [SaveableField(3)] public List<MBGUID> EmbeddedAgents;
        [SaveableField(4)] public int CounterIntelDefense;
        [SaveableField(5)] public float InformationQuality;
        [SaveableField(6)] public bool DoubleAgentCapability;
        [SaveableField(7)] public int LastSuccessfulOperation;

        public SpyNetworkData()
        {
            EmbeddedAgents = new List<MBGUID>();
        }
    }
}