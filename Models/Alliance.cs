using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace SecretAlliances.Models
{
    /// <summary>
    /// Represents a secret alliance between multiple clans
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    [SaveableClass(1)]
    public class Alliance
    {
        [SaveableField(1)]
        public string Id { get; set; }

        [SaveableField(2)]
        public string Name { get; set; }

        [SaveableField(3)]
        public bool IsSecret { get; set; }

        [SaveableField(4)]
        public List<MBGUID> MemberClanIds { get; set; }

        [SaveableField(5)]
        public MBGUID LeaderClanId { get; set; }

        [SaveableField(6)]
        public string Purpose { get; set; }

        [SaveableField(7)]
        public CampaignTime StartTime { get; set; }

        [SaveableField(8)]
        public CampaignTime EndTime { get; set; }

        [SaveableField(9)]
        public float TrustLevel { get; set; }

        [SaveableField(10)]
        public float SecrecyLevel { get; set; }

        [SaveableField(11)]
        public List<string> HistoryLog { get; set; }

        [SaveableField(12)]
        public bool IsActive { get; set; }

        public Alliance()
        {
            MemberClanIds = new List<MBGUID>();
            HistoryLog = new List<string>();
            IsActive = true;
            TrustLevel = 0.5f;
            SecrecyLevel = 1.0f;
            IsSecret = true;
            StartTime = CampaignTime.Now;
        }

        public Alliance(string name, List<Clan> members, Clan leader, string purpose) : this()
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Purpose = purpose;
            LeaderClanId = leader?.Id ?? default(MBGUID);
            
            foreach (var clan in members)
            {
                if (clan != null)
                    MemberClanIds.Add(clan.Id);
            }

            AddHistoryEntry($"Alliance '{name}' created with {members.Count} members");
        }

        public void AddHistoryEntry(string entry)
        {
            HistoryLog.Add($"[{CampaignTime.Now.ToYears():F1}] {entry}");
            
            // Keep only last 50 entries for performance
            if (HistoryLog.Count > 50)
            {
                HistoryLog.RemoveAt(0);
            }
        }

        public List<Clan> GetMemberClans()
        {
            var clans = new List<Clan>();
            foreach (var clanId in MemberClanIds)
            {
                var clan = MBObjectManager.Instance.GetObject<Clan>(c => c.Id == clanId);
                if (clan != null && !clan.IsEliminated)
                    clans.Add(clan);
            }
            return clans;
        }

        public Clan GetLeaderClan()
        {
            return MBObjectManager.Instance.GetObject<Clan>(c => c.Id == LeaderClanId);
        }

        public bool HasMember(Clan clan)
        {
            return clan != null && MemberClanIds.Contains(clan.Id);
        }

        public void AddMember(Clan clan)
        {
            if (clan != null && !HasMember(clan))
            {
                MemberClanIds.Add(clan.Id);
                AddHistoryEntry($"{clan.Name} joined the alliance");
            }
        }

        public void RemoveMember(Clan clan)
        {
            if (clan != null && HasMember(clan))
            {
                MemberClanIds.Remove(clan.Id);
                AddHistoryEntry($"{clan.Name} left the alliance");
                
                // If leader left, choose new leader
                if (LeaderClanId == clan.Id)
                {
                    var remainingClans = GetMemberClans();
                    LeaderClanId = remainingClans.Count > 0 ? remainingClans[0].Id : default(MBGUID);
                }
                
                // Deactivate if no members left
                if (MemberClanIds.Count == 0)
                {
                    IsActive = false;
                    AddHistoryEntry("Alliance dissolved - no members remaining");
                }
            }
        }
    }
}