using FactionColonies;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Coverage for <see cref="FCAHSettings.ResetToDefaults"/>. Skips unless the basic-animal DefOfs
    /// have resolved (they resolve at startup, so this holds inside a running game). Snapshot/restore
    /// pins the mutated statics because ResetToDefaults rewrites them and invalidates caches.
    /// </summary>
    public static class SettingsTests
    {
        [EmpireTest("AH.Settings")]
        public static void ResetToDefaults_RestoresBoolsAndDefaultAnimals()
        {
            if (AnimalHusbandryDefOf.Cow is null || AnimalHusbandryDefOf.Chicken is null)
                TestAssert.Skip("Basic-animal DefOfs not resolved in this load");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                // Drive every field away from its default first.
                FCAHSettings.BasicAnimalsEnabled = false;
                FCAHSettings.RestrictFishToWater = false;
                FCAHSettings.RestrictMercAnimalsToStocked = false;
                FCAHSettings.BasicAnimals.Clear();

                FCAHSettings.ResetToDefaults();

                TestAssert.IsTrue(FCAHSettings.BasicAnimalsEnabled, "BasicAnimalsEnabled should reset to true");
                TestAssert.IsTrue(FCAHSettings.RestrictFishToWater, "RestrictFishToWater should reset to true");
                TestAssert.IsTrue(FCAHSettings.RestrictMercAnimalsToStocked, "RestrictMercAnimalsToStocked should reset to true");

                TestAssert.AreEqual(5, FCAHSettings.BasicAnimals.Count, "the five default basic animals should be restored");
                TestAssert.Contains(FCAHSettings.BasicAnimals, AnimalHusbandryDefOf.Cow, "defaults should include the cow");
                TestAssert.Contains(FCAHSettings.BasicAnimals, AnimalHusbandryDefOf.Chicken, "defaults should include the chicken");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }
    }
}
