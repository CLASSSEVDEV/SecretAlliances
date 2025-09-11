using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances
{
    /// <summary>
    /// Helper class for displaying secret alliance information and debug data
    /// Can be integrated with custom UI screens or used for logging/debugging
    /// </summary>
    public static class AllianceUIHelper
    {
        /// <summary>
        /// Get formatted string of all active alliances for debug display
        /// </summary> 

        internal static void DebugLog(string text)
        {
            try
            {
                var asmLoc = Path.GetDirectoryName(typeof(AllianceUIHelper).Assembly.Location) ?? ".";
                if (string.IsNullOrEmpty(asmLoc)) asmLoc = Path.GetTempPath();
                var path = Path.Combine(asmLoc, "SecretAlliances_debug.log");
                File.AppendAllText(
                    path,
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + text + Environment.NewLine
                );
            }
            catch { }
        }

        public static void WriteFullDebug(SecretAllianceBehavior behavior)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(GetActiveAlliancesDebugInfo(behavior));
                sb.AppendLine();
                sb.AppendLine(GetIntelligenceDebugInfo(behavior));
                sb.AppendLine();
                sb.AppendLine($"Player opportunities:\n{GetPlayerAllianceOpportunities(behavior)}");

                DebugLog(sb.ToString());
            }
            catch (Exception e)
            {
                // Ensure we never crash the game while logging
                DebugLog("WriteFullDebug exception: " + e);
            }
        }

        public static string GetActiveAlliancesDebugInfo(SecretAllianceBehavior behavior)
        {
            if (behavior == null) return "No alliance behavior found.";

            var alliances = behavior.GetActiveAlliances();
            if (!alliances.Any()) return "No active secret alliances.";

            var info = new List<string>();
            info.Add("=== ACTIVE SECRET ALLIANCES ===\n");

            foreach (var alliance in alliances)
            {
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();

                if (initiator == null || target == null) continue;

                info.Add($"Alliance: {initiator.Name} <-> {target.Name}");
                info.Add($"  Strength: {alliance.Strength:F2} | Secrecy: {alliance.Secrecy:F2} | Trust: {alliance.TrustLevel:F2}");
                info.Add($"  GroupId: {alliance.GroupId} | Bribe: {alliance.BribeAmount:F0} | Days Active: {CampaignTime.Now.GetDayOfYear - alliance.CreatedGameDay}");
                info.Add($"  Trade Pact: {alliance.TradePact} | Military Pact: {alliance.MilitaryPact}");
                info.Add($"  Political Pressure: {alliance.PoliticalPressure:F2} | Military Advantage: {alliance.MilitaryAdvantage:F2}");
                info.Add($"  Common Enemies: {alliance.HasCommonEnemies} | Coup Attempted: {alliance.CoupAttempted}");
                info.Add($"  Successful Ops: {alliance.SuccessfulOperations} | Leak Attempts: {alliance.LeakAttempts}");

                if (alliance.BetrayalRevealed)
                    info.Add("  STATUS: EXPOSED!");

                info.Add("");
                

            }

            string result = string.Join("\n", info);
            DebugLog(result);

            return result;
        }

        /// <summary>
        /// Get intelligence reports for debugging
        /// </summary>
        public static string GetIntelligenceDebugInfo(SecretAllianceBehavior behavior)
        {
            if (behavior == null) return "No alliance behavior found.";

            var intelligence = behavior.GetIntelligence();
            if (!intelligence.Any()) return "No intelligence gathered.";

            var info = new List<string>();
            info.Add("=== ALLIANCE INTELLIGENCE ===\n");

            foreach (var intel in intelligence.OrderByDescending(i => i.ReliabilityScore))
            {
                var informer = intel.GetInformer();

                info.Add($"Intelligence Report:");
                info.Add($"  Informer: {informer?.Name?.ToString() ?? "Unknown"} ({informer?.Clan?.Name?.ToString() ?? "No Clan"})");
                info.Add($"  Reliability: {intel.ReliabilityScore:F2} | Age: {intel.DaysOld} days");
                info.Add($"  Severity: {intel.SeverityLevel:F2} | Confirmed: {intel.IsConfirmed}");
                info.Add("");
            }

            string result = string.Join("\n", info);
            DebugLog(result);
            return result;

        }

        /// <summary>
        /// Get alliance assessment for a specific clan
        /// </summary>
        public static string GetClanAllianceStatus(Clan clan, SecretAllianceBehavior behavior)
        {
            if (clan == null || behavior == null) return "Invalid clan or behavior.";

            var alliances = behavior.GetAlliancesForClan(clan);

            var info = new List<string>();
            info.Add($"=== {clan.Name} ALLIANCE STATUS ===\n");

            if (!alliances.Any())
            {
                info.Add("No active secret alliances.");
            }
            else
            {
                foreach (var alliance in alliances)
                {
                    var otherClan = alliance.InitiatorClanId == clan.Id ?
                        alliance.GetTargetClan() : alliance.GetInitiatorClan();

                    if (otherClan == null) continue;

                    string role = alliance.InitiatorClanId == clan.Id ? "Initiator" : "Target";

                    info.Add($"Allied with: {otherClan.Name} (as {role})");
                    info.Add($"  Alliance Health: {GetAllianceHealthDescription(alliance)}");
                    info.Add($"  Secrecy Level: {GetSecrecyDescription(alliance.Secrecy)}");
                    info.Add($"  Strength Level: {GetStrengthDescription(alliance.Strength)}");

                    if (alliance.BribeAmount > 0)
                        info.Add($"  Financial Incentive: {alliance.BribeAmount:F0} denars");

                    if (alliance.CoupAttempted)
                        info.Add($"  WARNING: Coup previously attempted!");

                    if (alliance.BetrayalRevealed)
                        info.Add($"  WARNING: Alliance has been exposed!");

                    info.Add("");
                }
            }

            // Add clan vulnerability assessment
            info.Add("Vulnerability Assessment:");
            info.Add($"  Economic Status: {GetEconomicStatusDescription(clan)}");
            info.Add($"  Military Strength: {GetMilitaryStrengthDescription(clan)}");
            info.Add($"  Political Position: {GetPoliticalPositionDescription(clan)}");

            return string.Join("\n", info);
        }

        /// <summary>
        /// Get kingdom-wide alliance overview
        /// </summary>
        public static string GetKingdomAllianceOverview(Kingdom kingdom, SecretAllianceBehavior behavior)
        {
            if (kingdom == null || behavior == null) return "Invalid kingdom or behavior.";

            var alliances = behavior.GetActiveAlliances();
            var kingdomAlliances = alliances.Where(a =>
                (a.GetInitiatorClan()?.Kingdom == kingdom) ||
                (a.GetTargetClan()?.Kingdom == kingdom)).ToList();

            var info = new List<string>();
            info.Add($"=== {kingdom.Name} ALLIANCE OVERVIEW ===\n");

            if (!kingdomAlliances.Any())
            {
                info.Add("No known secret alliances involving kingdom clans.");
            }
            else
            {
                info.Add($"Total Secret Alliances: {kingdomAlliances.Count}");

                var internalAlliances = kingdomAlliances.Where(a =>
                    a.GetInitiatorClan()?.Kingdom == kingdom &&
                    a.GetTargetClan()?.Kingdom == kingdom).ToList();

                var externalAlliances = kingdomAlliances.Where(a =>
                    (a.GetInitiatorClan()?.Kingdom == kingdom && a.GetTargetClan()?.Kingdom != kingdom) ||
                    (a.GetTargetClan()?.Kingdom == kingdom && a.GetInitiatorClan()?.Kingdom != kingdom)).ToList();

                info.Add($"Internal Alliances (within kingdom): {internalAlliances.Count}");
                info.Add($"External Alliances (with other kingdoms): {externalAlliances.Count}");
                info.Add("");

                // Most dangerous alliances
                var dangerousAlliances = kingdomAlliances.Where(a =>
                    a.Strength > 0.6f && a.Secrecy < 0.4f).OrderByDescending(a => a.Strength);

                if (dangerousAlliances.Any())
                {
                    info.Add("HIGH RISK ALLIANCES:");
                    foreach (var alliance in dangerousAlliances.Take(3))
                    {
                        var initiator = alliance.GetInitiatorClan();
                        var target = alliance.GetTargetClan();
                        info.Add($"  {initiator?.Name} <-> {target?.Name} (Strength: {alliance.Strength:F2}, Secrecy: {alliance.Secrecy:F2})");
                    }
                    info.Add("");
                }

                // Kingdom stability assessment
                float stabilityThreat = CalculateKingdomStabilityThreat(kingdom, kingdomAlliances);
                info.Add($"Kingdom Stability Threat Level: {GetThreatLevelDescription(stabilityThreat)}");
            }

            string result = string.Join("\n", info);
            DebugLog(result);
            return result;

        }

        // Helper description methods
        private static string GetAllianceHealthDescription(SecretAllianceRecord alliance)
        {
            float health = (alliance.Strength + alliance.TrustLevel) / 2f;

            if (health > 0.8f) return "Excellent";
            if (health > 0.6f) return "Good";
            if (health > 0.4f) return "Fair";
            if (health > 0.2f) return "Poor";
            return "Critical";
        }

        private static string GetSecrecyDescription(float secrecy)
        {
            if (secrecy > 0.8f) return "Highly Secret";
            if (secrecy > 0.6f) return "Well Hidden";
            if (secrecy > 0.4f) return "Moderately Secret";
            if (secrecy > 0.2f) return "Poorly Hidden";
            return "Widely Known";
        }

        private static string GetStrengthDescription(float strength)
        {
            if (strength > 0.8f) return "Very Strong";
            if (strength > 0.6f) return "Strong";
            if (strength > 0.4f) return "Moderate";
            if (strength > 0.2f) return "Weak";
            return "Very Weak";
        }

        private static string GetEconomicStatusDescription(Clan clan)
        {
            if (clan.Gold > 20000) return "Wealthy";
            if (clan.Gold > 10000) return "Prosperous";
            if (clan.Gold > 5000) return "Stable";
            if (clan.Gold > 2000) return "Struggling";
            return "Impoverished";
        }

        private static string GetMilitaryStrengthDescription(Clan clan)
        {
            float strength = clan.TotalStrength;
            if (strength > 500f) return "Very Strong";
            if (strength > 200f) return "Strong";
            if (strength > 100f) return "Moderate";
            if (strength > 50f) return "Weak";
            return "Very Weak";
        }

        private static string GetPoliticalPositionDescription(Clan clan)
        {
            if (clan.Kingdom == null) return "Independent";
            if (clan.Kingdom.Leader == clan.Leader) return "Ruler";

            if (clan.Leader != null)
            {
                
                float influence = clan.Influence;
                if (influence > 100f) return "Very Influential";
                if (influence > 50f) return "Influential";
                if (influence > 20f) return "Moderate Influence";
                if (influence > 0f) return "Low Influence";
                return "No Influence";
            }

            return "Unknown";
        }

        private static float CalculateKingdomStabilityThreat(Kingdom kingdom, List<SecretAllianceRecord> alliances)
        {
            if (!alliances.Any()) return 0f;

            float threat = 0f;

            foreach (var alliance in alliances)
            {
                // Internal alliances against the kingdom are more threatening
                var initiator = alliance.GetInitiatorClan();
                var target = alliance.GetTargetClan();

                if (initiator?.Kingdom == kingdom && target?.Kingdom == kingdom)
                {
                    threat += alliance.Strength * 0.5f; // Internal plotting
                }
                else
                {
                    threat += alliance.Strength * 0.3f; // External coordination
                }

                // Low secrecy alliances are more dangerous (more people involved)
                threat += (1f - alliance.Secrecy) * 0.2f;

                // Attempted coups are major threat indicators
                if (alliance.CoupAttempted)
                {
                    threat += 0.4f;
                }
            }

           float clamped =  MathF.Min(1f, threat);
            DebugLog($"Calculated kindom stablity threat score = {clamped}");

            return clamped;

        }

        private static string GetThreatLevelDescription(float threat)
        {
            if (threat > 0.8f) return "CRITICAL - Immediate action required";
            if (threat > 0.6f) return "HIGH - Close monitoring needed";
            if (threat > 0.4f) return "MODERATE - Some concern";
            if (threat > 0.2f) return "LOW - Minor risk";
            return "MINIMAL - Stable";
        }

        /// <summary>
        /// Get player-specific alliance opportunities
        /// </summary>
        public static string GetPlayerAllianceOpportunities(SecretAllianceBehavior behavior)
        {
            var playerClan = Clan.PlayerClan;
            if (playerClan == null) return "Player not in a clan.";

            var info = new List<string>();
            info.Add("=== ALLIANCE OPPORTUNITIES ===\n");

            var potentialTargets = Clan.All.Where(c =>
                c != playerClan &&
                !c.IsEliminated &&
                c.Leader != null &&
                behavior.FindAlliance(playerClan, c) == null).ToList();

            var scoredTargets = potentialTargets.Select(c => new
            {
                Clan = c,
                Score = CalculatePlayerAllianceScore(playerClan, c)
            }).OrderByDescending(x => x.Score).Take(5);

            info.Add("Top Alliance Candidates:");
            foreach (var target in scoredTargets)
            {
                info.Add($"{target.Clan.Name}: {target.Score:F1}% compatibility");
                info.Add($"  Kingdom: {target.Clan.Kingdom?.Name?.ToString() ?? "Independent"}");
                info.Add($"  Relation: {target.Clan.Leader?.GetRelation(Hero.MainHero) ?? 0}");
                info.Add($"  Economic Status: {GetEconomicStatusDescription(target.Clan)}");
                info.Add($"  Military Strength: {GetMilitaryStrengthDescription(target.Clan)}");
                info.Add("");
            }

            string result = string.Join("\n", info);
            DebugLog(result);
            return result;

        }

        private static float CalculatePlayerAllianceScore(Clan playerClan, Clan targetClan)
        {
            float score = 50f; // Base score

            // Relationship bonus/penalty
            if (targetClan.Leader != null)
            {
                int relation = targetClan.Leader.GetRelation(Hero.MainHero);
                score += relation * 0.5f;
            }

            // Economic complementarity
            if (playerClan.Gold > targetClan.Gold * 1.5f || targetClan.Gold > playerClan.Gold * 1.5f)
            {
                score += 15f; // Economic imbalance creates opportunity
            }

            // Military complementarity
            float strengthRatio = playerClan.TotalStrength / MathF.Max(1f, targetClan.TotalStrength);
            if (strengthRatio > 2f || strengthRatio < 0.5f)
            {
                score += 10f;
            }

            // Political situation
            if (playerClan.Kingdom != null && targetClan.Kingdom != null)
            {
                if (playerClan.Kingdom == targetClan.Kingdom)
                {
                    score += 20f; // Same kingdom easier
                }
                else if (playerClan.Kingdom.IsAtWarWith(targetClan.Kingdom))
                {
                    score -= 30f; // War makes it harder
                }
            }

            // Desperation factors
            if (targetClan.Gold < 3000) score += 10f;
            if (targetClan.TotalStrength < 100f) score += 8f;
            if (targetClan.Settlements.Count <= 1) score += 12f;

            float clamped = MathF.Max(0f, MathF.Min(100f, score));

            DebugLog($"Calculated player alliance score = {clamped} \n");

            return clamped;
        }

        /// <summary>
        /// Dump ALL active alliances (player and AI) including GroupId and pact information
        /// </summary>
        public static void DumpAllAlliances(SecretAllianceBehavior behavior)
        {
            if (behavior == null)
            {
                Debug.Print("[Secret Alliances] No behavior available for dumping alliances");
                return;
            }

            var alliances = behavior.GetActiveAlliances();
            if (!alliances.Any())
            {
                Debug.Print("[Secret Alliances] No active alliances to dump");
                return;
            }

            Debug.Print("=== ALL ACTIVE SECRET ALLIANCES DUMP ===");
            
            var playerAlliances = alliances.Where(a => 
                a.InitiatorClanId == Clan.PlayerClan?.Id || a.TargetClanId == Clan.PlayerClan?.Id).ToList();
            var aiAlliances = alliances.Where(a => 
                a.InitiatorClanId != Clan.PlayerClan?.Id && a.TargetClanId != Clan.PlayerClan?.Id).ToList();

            Debug.Print($"Total Alliances: {alliances.Count} (Player: {playerAlliances.Count}, AI: {aiAlliances.Count})");

            // Group by GroupId for coalition analysis
            var coalitions = alliances.GroupBy(a => a.GroupId).Where(g => g.Count() > 1).ToList();
            if (coalitions.Any())
            {
                Debug.Print($"Active Coalitions: {coalitions.Count()}");
                foreach (var coalition in coalitions)
                {
                    Debug.Print($"  Coalition GroupId {coalition.Key}: {coalition.Count()} alliances");
                }
            }

            Debug.Print("\n--- PLAYER ALLIANCES ---");
            foreach (var alliance in playerAlliances)
            {
                DumpSingleAlliance(alliance);
            }

            Debug.Print("\n--- AI ALLIANCES ---");
            foreach (var alliance in aiAlliances)
            {
                DumpSingleAlliance(alliance);
            }

            Debug.Print("=== END ALLIANCE DUMP ===");
        }

        private static void DumpSingleAlliance(SecretAllianceRecord alliance)
        {
            var initiator = alliance.GetInitiatorClan();
            var target = alliance.GetTargetClan();

            if (initiator == null || target == null) return;

            Debug.Print($"  {initiator.Name} <-> {target.Name}");
            Debug.Print($"    GroupId: {alliance.GroupId} | Strength: {alliance.Strength:F2} | Secrecy: {alliance.Secrecy:F2}");
            Debug.Print($"    Trade Pact: {alliance.TradePact} | Military Pact: {alliance.MilitaryPact}");
            Debug.Print($"    Trust: {alliance.TrustLevel:F2} | Bribe: {alliance.BribeAmount:F0}");
            Debug.Print($"    Successful Ops: {alliance.SuccessfulOperations} | Leaks: {alliance.LeakAttempts}");
            if (alliance.BetrayalRevealed)
                Debug.Print($"    STATUS: EXPOSED!");
        }
    }
}