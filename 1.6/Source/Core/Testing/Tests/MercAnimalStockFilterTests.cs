using System.Collections.Generic;
using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Coverage for <see cref="MercAnimalStockFilter"/>, the gate on the unit designer's animal picker.
    /// Toggle off falls through to the vanilla list; toggle on consults <see cref="StockedAnimalCache"/>.
    /// </summary>
    public static class MercAnimalStockFilterTests
    {
        private static readonly MercAnimalStockFilter Filter = MercAnimalStockFilter.Instance;

        [EmpireTest("AH.MercFilter")]
        public static void IsAnimalAllowed_ToggleOff_AllowsAnything()
        {
            ThingDef unstockedRace = AHTestHelper.MakeAnimalDef(true, "AHTest_MercUnstocked");
            PawnKindDef kind = AHTestHelper.MakePawnKind(unstockedRace, "AHTest_MercKind");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.RestrictMercAnimalsToStocked = false;
                TestAssert.IsTrue(Filter.IsAnimalAllowed(kind),
                    "with the gate off, any animal kind should be allowed");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }

        [EmpireTest("AH.MercFilter")]
        public static void IsAnimalAllowed_NullRace_Allowed()
        {
            PawnKindDef kind = AHTestHelper.MakePawnKind(null, "AHTest_MercNullRace");
            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.RestrictMercAnimalsToStocked = true;
                TestAssert.IsTrue(Filter.IsAnimalAllowed(kind),
                    "a kind with a null race should be allowed rather than crash");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }

        [EmpireTest("AH.MercFilter")]
        public static void IsAnimalAllowed_ToggleOn_OnlyStockedRaces()
        {
            ThingDef stockedRace = AHTestHelper.MakeAnimalDef(true, "AHTest_MercStocked");
            ThingDef unstockedRace = AHTestHelper.MakeAnimalDef(true, "AHTest_MercNotStocked");
            PawnKindDef stockedKind = AHTestHelper.MakePawnKind(stockedRace, "AHTest_MercStockedKind");
            PawnKindDef unstockedKind = AHTestHelper.MakePawnKind(unstockedRace, "AHTest_MercNotStockedKind");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.RestrictMercAnimalsToStocked = true;
                FCAHSettings.BasicAnimalsEnabled = true;
                FCAHSettings.BasicAnimals = new List<ThingDef> { stockedRace };
                StockedAnimalCache.Invalidate();

                TestAssert.IsTrue(Filter.IsAnimalAllowed(stockedKind),
                    "a kind whose race is stocked should be allowed");
                TestAssert.IsFalse(Filter.IsAnimalAllowed(unstockedKind),
                    "a kind whose race is not stocked should be denied");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }
    }
}
