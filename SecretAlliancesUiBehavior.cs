using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace SecretAlliances.Behaviors
{
    // Derive from CampaignBehaviorBase so AddBehavior accepts it (fixes CS1503).
    // Allows Shift + A to toggle the overlay anywhere on the campaign map (optional quality-of-life).
    public class SecretAlliancesUiBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnTick(float dt)
        {
            if (!IsOnCampaignMap()) return;

            bool shiftDown = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
            if (shiftDown && Input.IsKeyPressed(InputKey.A))
            {
                UI.SecretAlliancesUI.Toggle();
            }
        }

        private static bool IsOnCampaignMap()
        {
            var screen = ScreenManager.TopScreen;
            if (screen == null) return false;
            var name = screen.GetType().Name;
            return name.Contains("MapScreen");
        }
    }
}