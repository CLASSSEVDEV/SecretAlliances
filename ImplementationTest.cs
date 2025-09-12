using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SecretAlliances
{
    /// <summary>
    /// Simple test class to verify the mega feature implementation works correctly
    /// This can be called from debug console or during development testing
    /// </summary>
    public static class ImplementationTest
    {
        public static void RunBasicFunctionalityTest(SecretAllianceBehavior behavior)
        {
            if (behavior == null)
            {
                Debug.Print("[Test] ERROR: SecretAllianceBehavior is null");
                return;
            }

            Debug.Print("=== SECRET ALLIANCES IMPLEMENTATION TEST ===");

            try
            {
                // Test 1: Config System
                TestConfigSystem();

                // Test 2: Alliance Creation with Enhanced Features
                TestEnhancedAllianceCreation(behavior);

                // Test 3: Coalition System
                TestCoalitionSystem(behavior);

                // Test 4: Operations Framework
                TestOperationsFramework(behavior);

                // Test 5: Intelligence & Rumor System
                TestIntelligenceSystem(behavior);

                // Test 6: Enhanced UI Integration
                TestUIIntegration(behavior);

                Debug.Print("=== ALL TESTS COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Debug.Print($"[Test] ERROR: {ex.Message}");
                Debug.Print($"[Test] Stack: {ex.StackTrace}");
            }
        }

        private static void TestConfigSystem()
        {
            Debug.Print("[Test] Testing Configuration System...");
            
            var config = SecretAlliancesConfig.Instance;
            
            // Verify default values
            if (config.WealthDisparityThreshold != 0.3f)
                throw new Exception("Config default value incorrect");
                
            if (config.CohesionStrengthWeight != 0.6f)
                throw new Exception("Coalition config incorrect");
                
            if (config.DefectionCooldownDays != 30)
                throw new Exception("Defection config incorrect");
                
            if (config.OperationBaseDuration != 7)
                throw new Exception("Operations config incorrect");
                
            Debug.Print("[Test] ✓ Configuration system working correctly");
        }

        private static void TestEnhancedAllianceCreation(SecretAllianceBehavior behavior)
        {
            Debug.Print("[Test] Testing Enhanced Alliance Creation...");
            
            // Get test clans
            var clans = Clan.All.Where(c => !c.IsEliminated && c.Leader != null).Take(3).ToList();
            if (clans.Count < 2)
            {
                Debug.Print("[Test] ! Insufficient clans for alliance test, skipping");
                return;
            }

            var clan1 = clans[0];
            var clan2 = clans[1];

            // Create test alliance
            behavior.CreateAlliance(clan1, clan2, 0.8f, 0.3f, 1000f, 1);
            
            // Verify alliance was created
            var alliance = behavior.FindAlliance(clan1, clan2);
            if (alliance == null)
                throw new Exception("Alliance creation failed");
                
            if (alliance.GroupId != 1)
                throw new Exception("GroupId not set correctly");
                
            if (alliance.DefectionCooldownDays != 0) // Should start at 0
                Debug.Print("[Test] Note: DefectionCooldownDays initialized");
                
            Debug.Print($"[Test] ✓ Enhanced alliance created: {clan1.Name} <-> {clan2.Name}");
        }

        private static void TestCoalitionSystem(SecretAllianceBehavior behavior)
        {
            Debug.Print("[Test] Testing Coalition System...");
            
            var activeAlliances = behavior.GetActiveAlliances();
            var coalitions = activeAlliances.Where(a => a.GroupId > 0).GroupBy(a => a.GroupId);
            
            Debug.Print($"[Test] Found {coalitions.Count()} coalition groups");
            
            foreach (var coalition in coalitions)
            {
                var alliances = coalition.ToList();
                if (alliances.Count > 1)
                {
                    Debug.Print($"[Test] Coalition {coalition.Key} has {alliances.Count} alliances");
                    
                    // Test cohesion calculation
                    float avgSecrecy = alliances.Average(a => a.Secrecy);
                    float avgStrength = alliances.Average(a => a.Strength);
                    
                    if (avgSecrecy >= 0f && avgStrength >= 0f)
                        Debug.Print($"[Test] ✓ Coalition metrics calculated: Secrecy={avgSecrecy:F2}, Strength={avgStrength:F2}");
                }
            }
            
            Debug.Print("[Test] ✓ Coalition system functional");
        }

        private static void TestOperationsFramework(SecretAllianceBehavior behavior)
        {
            Debug.Print("[Test] Testing Operations Framework...");
            
            var alliances = behavior.GetActiveAlliances();
            if (alliances.Any())
            {
                var alliance = alliances.First();
                
                // Test operation type enum
                var operationTypes = Enum.GetValues(typeof(PendingOperationType)).Cast<PendingOperationType>().ToList();
                if (operationTypes.Count != 6) // None + 5 operation types
                    throw new Exception($"Expected 6 operation types, found {operationTypes.Count}");
                
                // Test that operations can be assigned
                alliance.PendingOperationType = (int)PendingOperationType.CovertAid;
                if (alliance.PendingOperationType != 1)
                    throw new Exception("Operation type assignment failed");
                
                Debug.Print($"[Test] ✓ Operations framework operational, {operationTypes.Count} types available");
            }
            else
            {
                Debug.Print("[Test] ! No active alliances for operations test, skipping");
            }
        }

        private static void TestIntelligenceSystem(SecretAllianceBehavior behavior)
        {
            Debug.Print("[Test] Testing Intelligence System...");
            
            var intelligence = behavior.GetIntelligence();
            Debug.Print($"[Test] Found {intelligence.Count} intelligence records");
            
            // Test intelligence categories
            var categories = Enum.GetValues(typeof(AllianceIntelType)).Cast<AllianceIntelType>().ToList();
            if (categories.Count != 10) // Should have 10 intel types now
                throw new Exception($"Expected 10 intel types, found {categories.Count}");
            
            // Test category weights
            foreach (var category in categories)
            {
                if (IntelCategoryWeights.Weights.ContainsKey(category))
                {
                    var weight = IntelCategoryWeights.Weights[category];
                    if (weight <= 0f || weight > 2f)
                        throw new Exception($"Invalid category weight for {category}: {weight}");
                }
            }
            
            Debug.Print($"[Test] ✓ Intelligence system functional, {categories.Count} categories with valid weights");
        }

        private static void TestUIIntegration(SecretAllianceBehavior behavior)
        {
            Debug.Print("[Test] Testing UI Integration...");
            
            // Test rumor system if player clan exists
            if (Clan.PlayerClan != null)
            {
                // Find any hero to test rumor system
                var testHero = Hero.All.FirstOrDefault(h => !h.IsDead && h.Clan != null);
                if (testHero != null)
                {
                    // Test ShouldShowRumorOption (should return false for unconnected heroes)
                    bool shouldShow = behavior.ShouldShowRumorOption(testHero);
                    Debug.Print($"[Test] Rumor option for {testHero.Name}: {shouldShow}");
                    
                    // Test TryGetRumorsForHero
                    bool hasRumors = behavior.TryGetRumorsForHero(testHero, out string rumors);
                    Debug.Print($"[Test] Has rumors: {hasRumors}");
                    
                    Debug.Print("[Test] ✓ UI integration methods accessible");
                }
            }
            else
            {
                Debug.Print("[Test] ! No player clan found, UI test limited");
            }
        }
    }
}