using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using SecretAlliances.Behaviors;
using SecretAlliances.Models;

namespace SecretAlliances.ViewModels
{
    /// <summary>
    /// ViewModel for the Alliance Manager UI screen
    /// Provides data binding for Gauntlet UI integration with Clan/Kingdom screens
    /// Compatible with Bannerlord v1.2.9 and .NET Framework 4.7.2
    /// </summary>
    public class AllianceManagerVM : ViewModel
    {
        private readonly AllianceService _allianceService;
        private readonly RequestsBehavior _requestsBehavior;
        private readonly LeakBehavior _leakBehavior;

        // Observable collections for UI binding
        private MBBindingList<AllianceItemVM> _myAlliances;
        private MBBindingList<AllianceItemVM> _availableAlliances;
        private MBBindingList<RequestItemVM> _pendingRequests;
        private MBBindingList<RequestItemVM> _sentRequests;

        // Properties for UI state
        private string _clanName;
        private float _overallExposureScore;
        private int _totalAlliances;
        private int _totalRequests;
        private bool _canCreateAlliance;
        private string _statusMessage;

        public AllianceManagerVM(AllianceService allianceService, RequestsBehavior requestsBehavior, LeakBehavior leakBehavior)
        {
            _allianceService = allianceService;
            _requestsBehavior = requestsBehavior;
            _leakBehavior = leakBehavior;

            InitializeCollections();
            RefreshData();
        }

        #region Properties

        [DataSourceProperty]
        public MBBindingList<AllianceItemVM> MyAlliances
        {
            get => _myAlliances;
            set
            {
                if (value != _myAlliances)
                {
                    _myAlliances = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AllianceItemVM> AvailableAlliances
        {
            get => _availableAlliances;
            set
            {
                if (value != _availableAlliances)
                {
                    _availableAlliances = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<RequestItemVM> PendingRequests
        {
            get => _pendingRequests;
            set
            {
                if (value != _pendingRequests)
                {
                    _pendingRequests = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<RequestItemVM> SentRequests
        {
            get => _sentRequests;
            set
            {
                if (value != _sentRequests)
                {
                    _sentRequests = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string ClanName
        {
            get => _clanName;
            set
            {
                if (value != _clanName)
                {
                    _clanName = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public float OverallExposureScore
        {
            get => _overallExposureScore;
            set
            {
                if (value != _overallExposureScore)
                {
                    _overallExposureScore = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int TotalAlliances
        {
            get => _totalAlliances;
            set
            {
                if (value != _totalAlliances)
                {
                    _totalAlliances = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int TotalRequests
        {
            get => _totalRequests;
            set
            {
                if (value != _totalRequests)
                {
                    _totalRequests = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanCreateAlliance
        {
            get => _canCreateAlliance;
            set
            {
                if (value != _canCreateAlliance)
                {
                    _canCreateAlliance = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (value != _statusMessage)
                {
                    _statusMessage = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        #endregion

        #region Public Methods

        public void RefreshData()
        {
            RefreshAlliances();
            RefreshRequests();
            RefreshStatus();
        }

        public void ExecuteCreateAlliance()
        {
            // This would open alliance creation dialog or sub-screen
            InformationManager.ShowInquiry(new InquiryData(
                "Create Alliance",
                "Alliance creation feature would be implemented here with clan selection.",
                true, false,
                "Close", null,
                null, null));
        }

        public void ExecuteRefresh()
        {
            RefreshData();
            StatusMessage = "Data refreshed";
        }

        #endregion

        #region Private Methods

        private void InitializeCollections()
        {
            MyAlliances = new MBBindingList<AllianceItemVM>();
            AvailableAlliances = new MBBindingList<AllianceItemVM>();
            PendingRequests = new MBBindingList<RequestItemVM>();
            SentRequests = new MBBindingList<RequestItemVM>();
        }

        private void RefreshAlliances()
        {
            MyAlliances.Clear();
            AvailableAlliances.Clear();

            var playerClan = Hero.MainHero.Clan;
            if (playerClan == null) return;

            // My alliances
            var myAlliances = _allianceService?.GetAlliancesForClan(playerClan) ?? new List<Alliance>();
            foreach (var alliance in myAlliances)
            {
                MyAlliances.Add(new AllianceItemVM(alliance, _allianceService, _leakBehavior));
            }
            TotalAlliances = myAlliances.Count;

            // Available alliances to join (simplified - would need more complex logic)
            var allAlliances = _allianceService?.GetAllActiveAlliances() ?? new List<Alliance>();
            var availableAlliances = allAlliances.Where(a =>
                !a.HasMember(playerClan) &&
                a.GetMemberClans().Count < 5).Take(5).ToList();

            foreach (var alliance in availableAlliances)
            {
                AvailableAlliances.Add(new AllianceItemVM(alliance, _allianceService, _leakBehavior));
            }
        }

        private void RefreshRequests()
        {
            PendingRequests.Clear();
            SentRequests.Clear();

            var playerClan = Hero.MainHero.Clan;
            if (playerClan == null) return;

            // Pending requests to us
            var pendingRequests = _requestsBehavior?.GetPendingRequestsForClan(playerClan) ?? new List<Request>();
            foreach (var request in pendingRequests)
            {
                PendingRequests.Add(new RequestItemVM(request, _requestsBehavior));
            }

            // Requests we sent
            var sentRequests = _requestsBehavior?.GetRequestsSentByClan(playerClan) ?? new List<Request>();
            foreach (var request in sentRequests.Where(r => r.Status == RequestStatus.Pending || r.Status == RequestStatus.Accepted))
            {
                SentRequests.Add(new RequestItemVM(request, _requestsBehavior));
            }

            TotalRequests = pendingRequests.Count + sentRequests.Count(r => r.Status == RequestStatus.Pending);
        }

        private void RefreshStatus()
        {
            var playerClan = Hero.MainHero.Clan;
            if (playerClan == null) return;

            ClanName = playerClan.Name.ToString();
            OverallExposureScore = _leakBehavior?.GetClanExposureScore(playerClan) ?? 0f;
            CanCreateAlliance = MyAlliances.Count < 3; // Limit of 3 alliances

            // Status message based on current state
            if (OverallExposureScore > 0.7f)
            {
                StatusMessage = "HIGH EXPOSURE RISK - Your secret activities are being watched!";
            }
            else if (OverallExposureScore > 0.4f)
            {
                StatusMessage = "Moderate exposure risk - Be cautious with your alliances";
            }
            else if (TotalAlliances == 0)
            {
                StatusMessage = "No active alliances - Consider forming strategic partnerships";
            }
            else
            {
                StatusMessage = $"Managing {TotalAlliances} alliance(s) successfully";
            }
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for individual alliance items in lists
    /// </summary>
    public class AllianceItemVM : ViewModel
    {
        private readonly Alliance _alliance;
        private readonly AllianceService _allianceService;
        private readonly LeakBehavior _leakBehavior;

        private string _name;
        private string _memberNames;
        private float _trustLevel;
        private float _secrecyLevel;
        private int _memberCount;
        private bool _isLeader;
        private string _statusDescription;

        public AllianceItemVM(Alliance alliance, AllianceService allianceService, LeakBehavior leakBehavior)
        {
            _alliance = alliance;
            _allianceService = allianceService;
            _leakBehavior = leakBehavior;

            RefreshProperties();
        }

        #region Properties

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string MemberNames
        {
            get => _memberNames;
            set
            {
                if (value != _memberNames)
                {
                    _memberNames = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public float TrustLevel
        {
            get => _trustLevel;
            set
            {
                if (value != _trustLevel)
                {
                    _trustLevel = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public float SecrecyLevel
        {
            get => _secrecyLevel;
            set
            {
                if (value != _secrecyLevel)
                {
                    _secrecyLevel = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int MemberCount
        {
            get => _memberCount;
            set
            {
                if (value != _memberCount)
                {
                    _memberCount = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool IsLeader
        {
            get => _isLeader;
            set
            {
                if (value != _isLeader)
                {
                    _isLeader = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (value != _statusDescription)
                {
                    _statusDescription = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        #endregion

        #region Public Methods

        public void ExecuteLeaveAlliance()
        {
            InformationManager.ShowInquiry(new InquiryData(
                "Leave Alliance",
                $"Are you sure you want to leave {_alliance.Name}? This action cannot be undone.",
                true, true,
                "Leave", "Cancel",
                () => {
                    _allianceService?.LeaveAlliance(_alliance, Hero.MainHero.Clan);
                    InformationManager.DisplayMessage(new InformationMessage($"Left alliance: {_alliance.Name}", Colors.Yellow));
                },
                null));
        }

        public void ExecuteViewDetails()
        {
            var details = $"Alliance: {_alliance.Name}\n" +
                         $"Members: {_alliance.GetMemberClans().Count}\n" +
                         $"Trust Level: {_alliance.TrustLevel:P0}\n" +
                         $"Secrecy Level: {_alliance.SecrecyLevel:P0}\n" +
                         $"Created: {_alliance.StartTime.ToYears:F1}\n\n" 
                         ;

            InformationManager.ShowInquiry(new InquiryData(
                "Alliance Details",
                details,
                true, false,
                "Close", null,
                null, null));
        }

        #endregion

        private void RefreshProperties()
        {
            Name = _alliance.Name;
            TrustLevel = _alliance.TrustLevel;
            SecrecyLevel = _alliance.SecrecyLevel;

            var members = _alliance.GetMemberClans();
            MemberCount = members.Count;
            MemberNames = string.Join(", ", members.Select(c => c.Name.ToString()));

            IsLeader = _alliance.GetLeaderClan() == Hero.MainHero.Clan;

            StatusDescription = _alliance.IsActive ? "Active" : "Inactive";
            if (_alliance.SecrecyLevel < 0.3f)
                StatusDescription += " (Compromised)";
            else if (_alliance.TrustLevel < 0.3f)
                StatusDescription += " (Low Trust)";
        }
    }

    /// <summary>
    /// ViewModel for individual request items in lists
    /// </summary>
    public class RequestItemVM : ViewModel
    {
        private readonly Request _request;
        private readonly RequestsBehavior _requestsBehavior;

        private string _description;
        private string _requesterName;
        private string _targetName;
        private string _typeDescription;
        private string _statusDescription;
        private int _reward;
        private float _riskLevel;
        private bool _canAccept;
        private bool _canDecline;

        public RequestItemVM(Request request, RequestsBehavior requestsBehavior)
        {
            _request = request;
            _requestsBehavior = requestsBehavior;

            RefreshProperties();
        }

        #region Properties

        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (value != _description)
                {
                    _description = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string RequesterName
        {
            get => _requesterName;
            set
            {
                if (value != _requesterName)
                {
                    _requesterName = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string TypeDescription
        {
            get => _typeDescription;
            set
            {
                if (value != _typeDescription)
                {
                    _typeDescription = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (value != _statusDescription)
                {
                    _statusDescription = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int Reward
        {
            get => _reward;
            set
            {
                if (value != _reward)
                {
                    _reward = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public float RiskLevel
        {
            get => _riskLevel;
            set
            {
                if (value != _riskLevel)
                {
                    _riskLevel = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanAccept
        {
            get => _canAccept;
            set
            {
                if (value != _canAccept)
                {
                    _canAccept = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanDecline
        {
            get => _canDecline;
            set
            {
                if (value != _canDecline)
                {
                    _canDecline = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        #endregion

        #region Public Methods

        public void ExecuteAccept()
        {
            _requestsBehavior?.AcceptRequest(_request);
            InformationManager.DisplayMessage(new InformationMessage($"Accepted assistance request: {_request.Type}", Color.FromUint(0x0000FF00)));
        }

        public void ExecuteDecline()
        {
            _requestsBehavior?.DeclineRequest(_request, "Declined via UI");
            InformationManager.DisplayMessage(new InformationMessage($"Declined assistance request: {_request.Type}", Colors.Yellow));
        }

        #endregion

        private void RefreshProperties()
        {
            Description = _request.Description;
            RequesterName = _request.GetRequesterClan()?.Name?.ToString() ?? "Unknown";
            
            TypeDescription = _request.Type.ToString();
            StatusDescription = _request.GetStatusDescription();
            Reward = _request.GetEstimatedReward();
            RiskLevel = _request.RiskLevel;

            CanAccept = _request.IsPending();
            CanDecline = _request.IsPending();
        }
    }
}