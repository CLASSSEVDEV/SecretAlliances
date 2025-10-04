using System;
using System.Linq;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SecretAlliances
{
    public static class SecretAlliancesUI
    {
        private static GauntletLayer _layer;
        private static ViewModel _vm;

        public static bool IsOpen { get { return _layer != null; } }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (_layer != null) return;

            _vm = CreateAllianceManagerVM();

            _layer = new GauntletLayer(200);
            _layer.IsFocusLayer = true;
            _layer.LoadMovie("AllianceManager", _vm);

            ScreenManager.TopScreen.AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);
        }

        public static void Close()
        {
            if (_layer == null) return;
            ScreenManager.TopScreen.RemoveLayer(_layer);
            _layer = null;
            _vm = null;
        }

        private static ViewModel CreateAllianceManagerVM()
        {
            try
            {
                // Get the required behaviors from the campaign
                var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
                if (campaign == null)
                {
                    throw new InvalidOperationException("No active campaign found.");
                }

                // Find the behaviors we need
                var allianceService = campaign.GetCampaignBehavior<SecretAlliances.Behaviors.AllianceService>();
                var requestsBehavior = campaign.GetCampaignBehavior<SecretAlliances.Behaviors.RequestsBehavior>();
                var leakBehavior = campaign.GetCampaignBehavior<SecretAlliances.Behaviors.LeakBehavior>();

                if (allianceService == null || requestsBehavior == null || leakBehavior == null)
                {
                    throw new InvalidOperationException("Required behaviors not found. Make sure the mod is properly initialized.");
                }

                // Create the ViewModel with dependencies
                return new SecretAlliances.ViewModels.AllianceManagerVM(allianceService, requestsBehavior, leakBehavior);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Error opening Alliance Manager: {ex.Message}", Colors.Red));
                throw;
            }
        }
    }
}