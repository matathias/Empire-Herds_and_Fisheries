namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Coverage for <see cref="FCAHSettings.ResetToDefaults"/>. The basic-animal DefOfs resolve at
/// startup, so an unresolved one is a failure. Snapshot/restore
/// pins the mutated statics because ResetToDefaults rewrites them and invalidates caches.
/// </summary>
public static class SettingsTests
{
    [EmpireTest("AH.Settings")]
    public static void ResetToDefaults_RestoresBoolsAndDefaultAnimals()
    {
        TestAssert.NotNull(AnimalHusbandryDefOf.Cow, "AnimalHusbandryDefOf.Cow did not resolve");
        TestAssert.NotNull(AnimalHusbandryDefOf.Chicken, "AnimalHusbandryDefOf.Chicken did not resolve");

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
