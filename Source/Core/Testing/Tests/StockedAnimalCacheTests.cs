using System.Collections.Generic;
using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Coverage for the basic-animals branch and dirty-flag lifecycle of <see cref="StockedAnimalCache"/>.
    /// The cloned/registered branches need live settlements and are exercised in the destructive tier.
    /// Uses a uniquely-named fixture def so assertions hold regardless of any real faction state.
    /// </summary>
    public static class StockedAnimalCacheTests
    {
        [EmpireTest("AH.Cache")]
        public static void Get_IncludesBasicAnimals_WhenEnabled()
        {
            ThingDef fixtureAnimal = AHTestHelper.MakeAnimalDef(true, "AHTest_StockedCacheAnimal");
            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = true;
                FCAHSettings.BasicAnimals = new List<ThingDef> { fixtureAnimal };
                StockedAnimalCache.Invalidate();
                TestAssert.Contains(StockedAnimalCache.Get(), fixtureAnimal,
                    "an enabled basic animal should be in the stocked set");

                FCAHSettings.BasicAnimalsEnabled = false;
                StockedAnimalCache.Invalidate();
                TestAssert.IsFalse(StockedAnimalCache.Get().Contains(fixtureAnimal),
                    "a disabled basic animal should be excluded from the stocked set");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }

        [EmpireTest("AH.Cache")]
        public static void Get_CachesUntilInvalidated()
        {
            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                StockedAnimalCache.Invalidate();
                HashSet<ThingDef> first = StockedAnimalCache.Get();
                HashSet<ThingDef> second = StockedAnimalCache.Get();
                TestAssert.IsTrue(ReferenceEquals(first, second),
                    "repeated Get() without invalidation should return the cached set");

                StockedAnimalCache.Invalidate();
                HashSet<ThingDef> third = StockedAnimalCache.Get();
                TestAssert.IsFalse(ReferenceEquals(second, third),
                    "Get() after Invalidate() should rebuild a fresh set");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
            }
        }
    }
}
