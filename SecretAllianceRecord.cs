using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances
{
    // Internal enum for intelligence categories
    internal enum AllianceIntelType : int
    {
        General = 0,
        Trade = 1,
        Military = 2,
        Coup = 3,
        Financial = 4,
        Recruitment = 5
    }

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
            
            // Initialize core stabilization fields
            LastOperationDay = 0;
            PendingOperationType = 0;
            GroupSecrecyCache = 0f;
            GroupStrengthCache = 0f;
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

        // Core stabilization fields (append-only for save compatibility)
        [SaveableField(28)] public int LastOperationDay;
        [SaveableField(29)] public int PendingOperationType;
        [SaveableField(30)] public float GroupSecrecyCache;
        [SaveableField(31)] public float GroupStrengthCache;



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
        
        // Core stabilization fields (append-only for save compatibility)
        [SaveableField(7)] public MBGUID ClanAId;
        [SaveableField(8)] public MBGUID ClanBId;
        [SaveableField(9)] public int IntelCategory;

        public Hero GetInformer()
            => MBObjectManager.Instance.GetObject<Hero>(h => h.Id == InformerHeroId);
    }
}
