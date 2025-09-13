using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SecretAlliances
{
    public static class ConsoleCommands
    {
        public static void RegisterCommands()
        {
            
        }

        private static void DumpAlliances(string[] args)
        {
            var behavior = GetAllianceBehavior();
            if (behavior == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Behavior not found"));
                return;
            }

            var alliances = behavior.GetAlliances();
            if (!alliances.Any())
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No active alliances found"));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Active Alliances ({alliances.Count}):"));
            foreach (var alliance in alliances.Where(a => a.IsActive))
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();
                if (initiator != null && target != null)
                {
                    string pacts = "";
                    if (alliance.TradePact) pacts += "T";
                    if (alliance.MilitaryPact) pacts += "M";

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"  {initiator.Name} <-> {target.Name}: S={alliance.Strength:F2}, Sec={alliance.Secrecy:F2}, " +
                        $"Trust={alliance.TrustLevel:F2}, Group={alliance.GroupId}, Pacts=[{pacts}], Ops={alliance.SuccessfulOperations}"));
                }
            }
        }

        private static void ForceLeak(string[] args)
        {
            if (args.Length < 2)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.forceLeak clanA clanB"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            behavior.ForceLeakForTesting(alliance);
            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Forced leak for alliance between {clan1.Name} and {clan2.Name}"));
        }

        private static void AddTrust(string[] args)
        {
            if (args.Length < 3)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.addTrust clanA clanB amount"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (!float.TryParse(args[2], out float amount))
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid amount"));
                return;
            }

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            alliance.TrustLevel = MathF.Max(0f, MathF.Min(1f, alliance.TrustLevel + amount));
            InformationManager.DisplayMessage(new InformationMessage(
                $"[SecretAlliances] Added {amount:F2} trust. New trust level: {alliance.TrustLevel:F2}"));
        }

        private static void RunOperation(string[] args)
        {
            if (args.Length < 3)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.runOperation clanA clanB opType"));
                InformationManager.DisplayMessage(new InformationMessage("  OpTypes: 1=CovertAid, 2=SpyProbe, 3=Recruitment, 4=Sabotage, 5=CounterIntel"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (!int.TryParse(args[2], out int opType) || opType < 1 || opType > 5)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid operation type (1-5)"));
                return;
            }

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            string[] opNames = { "", "CovertAid", "SpyProbe", "Recruitment", "Sabotage", "CounterIntel" };
            behavior.ExecuteOperationForTesting(alliance, opType);
            InformationManager.DisplayMessage(new InformationMessage(
                $"[SecretAlliances] Executed {opNames[opType]} operation for {clan1.Name} <-> {clan2.Name}"));
        }

        private static void ListIntel(string[] args)
        {
            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            Clan targetClan = null;
            if (args.Length > 0)
            {
                targetClan = FindClan(args[0]);
                if (targetClan == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Clan not found"));
                    return;
                }
            }

            var intelligence = behavior.GetIntelligence();
            var filteredIntel = intelligence.Where(i =>
                targetClan == null ||
                i.ClanAId == targetClan.Id ||
                i.ClanBId == targetClan.Id).ToList();

            if (!filteredIntel.Any())
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No intelligence found"));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Intelligence ({filteredIntel.Count} entries):"));
            foreach (var intel in filteredIntel.OrderByDescending(i => i.ReliabilityScore).Take(10))
            {
                var clanA = MBObjectManager.Instance.GetObject<Clan>(c => c.Id == intel.ClanAId);
                var clanB = MBObjectManager.Instance.GetObject<Clan>(c => c.Id == intel.ClanBId);
                var informer = intel.GetInformer();

                string clanNames = (clanA != null && clanB != null) ? $"{clanA.Name} <-> {clanB.Name}" : "Unknown clans";
                string informerName = informer?.Name?.ToString() ?? "Unknown";

                InformationManager.DisplayMessage(new InformationMessage(
                    $"  {clanNames}: Rel={intel.ReliabilityScore:F2}, Sev={intel.SeverityLevel:F2}, " +
                    $"Age={intel.DaysOld}d, Cat={intel.IntelCategory}, Informer={informerName}"));
            }
        }

        private static void ConfigCommand(string[] args)
        {
            var config = AllianceConfig.Instance;

            if (args.Length == 0)
            {
                // Show all config values
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Configuration:"));
                InformationManager.DisplayMessage(new InformationMessage($"  FormationBaseChance: {config.FormationBaseChance:F3}"));
                InformationManager.DisplayMessage(new InformationMessage($"  MaxDailyFormations: {config.MaxDailyFormations}"));
                InformationManager.DisplayMessage(new InformationMessage($"  OperationIntervalDays: {config.OperationIntervalDays}"));
                InformationManager.DisplayMessage(new InformationMessage($"  LeakBaseChance: {config.LeakBaseChance:F3}"));
                InformationManager.DisplayMessage(new InformationMessage($"  TradeFlowMultiplier: {config.TradeFlowMultiplier:F2}"));
                InformationManager.DisplayMessage(new InformationMessage($"  BetrayalBaseChance: {config.BetrayalBaseChance:F3}"));
                InformationManager.DisplayMessage(new InformationMessage($"  DebugVerbose: {config.DebugVerbose}"));
                return;
            }

            if (args.Length == 1)
            {
                // Show specific property
                string property = args[0];
                var prop = typeof(AllianceConfig).GetProperty(property);
                if (prop != null)
                {
                    var value = prop.GetValue(config);
                    InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] {property}: {value}"));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Property '{property}' not found"));
                }
                return;
            }

            if (args.Length == 2)
            {
                // Set property value
                string property = args[0];
                string valueStr = args[1];

                var prop = typeof(AllianceConfig).GetProperty(property);
                if (prop == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Property '{property}' not found"));
                    return;
                }

                try
                {
                    if (prop.PropertyType == typeof(float))
                    {
                        if (float.TryParse(valueStr, out float floatVal))
                        {
                            prop.SetValue(config, floatVal);
                            config.ValidateAndClamp();
                            config.SaveConfig();
                            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Set {property} to {floatVal:F3}"));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid float value"));
                        }
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        if (int.TryParse(valueStr, out int intVal))
                        {
                            prop.SetValue(config, intVal);
                            config.ValidateAndClamp();
                            config.SaveConfig();
                            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Set {property} to {intVal}"));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid integer value"));
                        }
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        if (bool.TryParse(valueStr, out bool boolVal))
                        {
                            prop.SetValue(config, boolVal);
                            config.SaveConfig();
                            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Set {property} to {boolVal}"));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid boolean value (true/false)"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Error setting property: {ex.Message}"));
                }
            }
        }

        private static void ForceReveal(string[] args)
        {
            if (args.Length < 2)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.forceReveal clanA clanB"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            behavior.ForceRevealAlliance(alliance);
            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Forced reveal of alliance between {clan1.Name} and {clan2.Name}"));
        }

        private static void CreateAlliance(string[] args)
        {
            if (args.Length < 2)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.createAlliance clanA clanB"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            if (behavior.FindAlliance(clan1, clan2) != null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Alliance already exists between these clans"));
                return;
            }

            behavior.CreateTestAlliance(clan1, clan2);
            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Created test alliance between {clan1.Name} and {clan2.Name}"));
        }

        private static SecretAllianceBehavior GetAllianceBehavior()
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<SecretAllianceBehavior>();
            if (behavior == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Behavior not found - is the mod loaded?"));
            }
            return behavior;
        }

        private static Clan FindClan(string clanName)
        {
            // Try to find clan by name or string ID
            return Clan.All.FirstOrDefault(c =>
                c.Name?.ToString().Equals(clanName, StringComparison.OrdinalIgnoreCase) == true ||
                c.StringId?.Equals(clanName, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}