using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SecretAlliances
{
    public static class ConsoleCommands
    {
        public static void RegisterCommands()
        {
            // Commands would be registered here if TaleWorlds had a public command registration system
            // For now, these methods can be called through other means or mod integrations
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

        // Advanced feature console commands
        private static void UpgradeAlliance(string[] args)
        {
            if (args.Length < 2)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.upgradeAlliance clanA clanB"));
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

            if (behavior.TryUpgradeAlliance(clan1, clan2))
            {
                InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Alliance upgraded between {clan1.Name} and {clan2.Name}"));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Alliance upgrade failed - check requirements"));
            }
        }

        private static void CreateContract(string[] args)
        {
            if (args.Length < 5)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.createContract clanA clanB contractType duration value"));
                InformationManager.DisplayMessage(new InformationMessage("  ContractTypes: 0=Defense, 1=Trade, 2=Military, 3=Intelligence"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);

            if (!int.TryParse(args[2], out int contractType) || !int.TryParse(args[3], out int duration) || !float.TryParse(args[4], out float value))
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid parameters"));
                return;
            }

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            if (behavior.CreateContract(clan1, clan2, contractType, duration, value))
            {
                InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Contract created between {clan1.Name} and {clan2.Name}"));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Contract creation failed"));
            }
        }

        private static void ExecuteSpyOperation(string[] args)
        {
            if (args.Length < 4)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.spyOp allianceClanA allianceClanB targetClan opType"));
                InformationManager.DisplayMessage(new InformationMessage("  OpTypes: 1=Intel, 2=Sabotage, 3=DoubleAgent"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);
            var targetClan = FindClan(args[2]);

            if (!int.TryParse(args[3], out int opType))
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Invalid operation type"));
                return;
            }

            if (clan1 == null || clan2 == null || targetClan == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or more clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            if (behavior.ExecuteSpyOperation(alliance, targetClan, opType))
            {
                InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Spy operation successful against {targetClan.Name}"));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Spy operation failed"));
            }
        }

        private static void ExecuteEconomicWarfare(string[] args)
        {
            if (args.Length < 3)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.economicWar allianceClanA allianceClanB targetClan"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);
            var targetClan = FindClan(args[2]);

            if (clan1 == null || clan2 == null || targetClan == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or more clans not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            behavior.ExecuteEconomicWarfare(alliance, targetClan);
            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Economic warfare executed against {targetClan.Name}"));
        }

        private static void InitiateJointCampaign(string[] args)
        {
            if (args.Length < 3)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Usage: sa.jointCampaign allianceClanA allianceClanB settlementName"));
                return;
            }

            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var clan1 = FindClan(args[0]);
            var clan2 = FindClan(args[1]);
            var settlement = Settlement.All.FirstOrDefault(s =>
                s.Name?.ToString().Equals(args[2], StringComparison.OrdinalIgnoreCase) == true);

            if (clan1 == null || clan2 == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] One or both clans not found"));
                return;
            }

            if (settlement == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] Settlement not found"));
                return;
            }

            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[SecretAlliances] No alliance found between these clans"));
                return;
            }

            behavior.InitiateJointCampaign(alliance, settlement);
            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Joint campaign initiated against {settlement.Name}"));
        }

        private static void ShowAdvancedStats(string[] args)
        {
            var behavior = GetAllianceBehavior();
            if (behavior == null) return;

            var contracts = behavior.GetActiveContracts();
            var militaryData = behavior.GetMilitaryCoordinationData();
            var economicData = behavior.GetEconomicNetworkData();
            var spyData = behavior.GetSpyNetworkData();

            InformationManager.DisplayMessage(new InformationMessage($"[SecretAlliances] Advanced Statistics:"));
            InformationManager.DisplayMessage(new InformationMessage($"  Active Contracts: {contracts.Count}"));
            InformationManager.DisplayMessage(new InformationMessage($"  Military Coordination Networks: {militaryData.Count}"));
            InformationManager.DisplayMessage(new InformationMessage($"  Economic Networks: {economicData.Count}"));
            InformationManager.DisplayMessage(new InformationMessage($"  Spy Networks: {spyData.Count}"));

            if (args.Length > 0 && args[0].Equals("detailed", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var contract in contracts.Take(5))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"    Contract: Type {contract.ContractType}, Value {contract.ContractValue:F0}, Days left: {contract.ExpirationDay - CampaignTime.Now.GetDayOfYear}"));
                }

                foreach (var military in militaryData.Take(5))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"    Military: Level {military.CoordinationLevel}, Bonus {military.CombatEfficiencyBonus:F2}, Elite Exchange: {military.EliteUnitExchange}"));
                }

                foreach (var economic in economicData.Take(5))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"    Economic: Multiplier {economic.TradeVolumeMultiplier:F2}, Protection Level {economic.CaravanProtectionLevel}"));
                }

                foreach (var spy in spyData.Take(5))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"    Spy: Tier {spy.NetworkTier}, Agents {spy.EmbeddedAgents.Count}, Quality {spy.InformationQuality:F2}"));
                }
            }
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