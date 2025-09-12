using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances
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

            // Initialize Mega Feature Expansion fields with safe defaults
            BetrayalCooldownDay = 0;
            NextEligibleOperationDay = 0;
            CounterIntelBuffExpiryDay = 0;
            EscalationCounter = 0;
            LeakAttempts = 0;
            OperationCooldownsSerialized = null;
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

        // Mega Feature Expansion fields (append at end - indices 32-37)
        [SaveableField(32)] public int BetrayalCooldownDay;
        [SaveableField(33)] public int NextEligibleOperationDay;
        [SaveableField(34)] public int CounterIntelBuffExpiryDay;
        [SaveableField(35)] public int EscalationCounter; // near-miss betrayal escalation
        [SaveableField(36)] public int LeakAttempts; // cumulative leak attempts for logistic scaling
        [SaveableField(37)] public string OperationCooldownsSerialized; // JSON packed cooldown end days per op type

        // Helper properties and methods for cooldown management
        private Dictionary<int, int> _operationCooldowns;
        
        public Dictionary<int, int> OperationCooldowns
        {
            get
            {
                if (_operationCooldowns == null)
                {
                    _operationCooldowns = DeserializeCooldowns();
                }
                return _operationCooldowns;
            }
        }

        public Clan GetInitiatorClan()
            => MBObjectManager.Instance.GetObject<Clan>(c => c.Id == InitiatorClanId);

        public Clan GetTargetClan()
            => MBObjectManager.Instance.GetObject<Clan>(c => c.Id == TargetClanId);

        public bool IsValidAlliance()
            => GetInitiatorClan() != null && GetTargetClan() != null && IsActive;

        public bool IsOnCooldown()
            => CampaignTime.Now.GetDayOfYear < LastInteractionDay + CooldownDays;

        /// <summary>
        /// Gets the cooldown end day for a specific operation type
        /// </summary>
        public int GetCooldownEnd(OperationType operationType)
        {
            return OperationCooldowns.TryGetValue((int)operationType, out int endDay) ? endDay : 0;
        }

        /// <summary>
        /// Sets the cooldown end day for a specific operation type
        /// </summary>
        public void SetCooldownEnd(OperationType operationType, int endDay)
        {
            OperationCooldowns[(int)operationType] = endDay;
            SerializeCooldowns();
        }

        /// <summary>
        /// Checks if a specific operation type is on cooldown
        /// </summary>
        public bool IsOperationOnCooldown(OperationType operationType, int currentDay)
        {
            int endDay = GetCooldownEnd(operationType);
            return currentDay < endDay;
        }

        /// <summary>
        /// Resets ephemeral data for session-only resets if needed
        /// </summary>
        public void ResetEphemeral()
        {
            PendingOperationType = 0;
            GroupSecrecyCache = 0f;
            GroupStrengthCache = 0f;
        }

        private Dictionary<int, int> DeserializeCooldowns()
        {
            if (string.IsNullOrEmpty(OperationCooldownsSerialized))
            {
                return new Dictionary<int, int>();
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<int, int>>(OperationCooldownsSerialized) 
                       ?? new Dictionary<int, int>();
            }
            catch
            {
                return new Dictionary<int, int>();
            }
        }

        private void SerializeCooldowns()
        {
            try
            {
                if (_operationCooldowns == null || _operationCooldowns.Count == 0)
                {
                    OperationCooldownsSerialized = null;
                }
                else
                {
                    OperationCooldownsSerialized = JsonConvert.SerializeObject(_operationCooldowns);
                }
            }
            catch
            {
                OperationCooldownsSerialized = null;
            }
        }
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
        GeneralRumor = 0,
        TradePactEvidence = 1,
        MilitaryCoordination = 2,
        SecretMeeting = 3,
        BetrayalPlot = 4
    }

    // Enum for operation types used in cooldown management
    public enum OperationType
    {
        CovertAid = 0,
        SpyProbe = 1,
        RecruitmentFeelers = 2,
        SabotageRaid = 3,
        CounterIntelligence = 4
    }
}
