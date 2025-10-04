using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;

namespace SecretAlliances.UIExt
{
    // Target the ClanVM by its full name without hard-referencing the assembly type.
    // BaseViewModelMixin<T> requires T : ViewModel, so we use the base type.
    [ViewModelMixin("TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanVM")]
    public class ClanVMMixin : BaseViewModelMixin<ViewModel>
    {
        public ClanVMMixin(ViewModel original) : base(original) { }

        // Command invoked by our injected tab/button.
        [DataSourceMethod]
        public void ExecuteOpenSecretAlliances()
        {
            UI.SecretAlliancesUI.Open();
        }
    }
}