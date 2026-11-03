using System.Collections.Generic;
using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Coverage for <see cref="ThingSetMaker_AnimalHusbandry"/>'s empty-criteria guard: with an empty
    /// allowed-animal set, no animal or product def can satisfy the filter, so generation short-circuits
    /// and produces nothing. Budget/count generation is intentionally not exercised here — it depends on
    /// <see cref="PawnGenerator"/> and RNG and belongs to manual play-testing.
    /// <see cref="ThingSetMaker.Generate"/> is protected, so a thin subclass surfaces it.
    /// </summary>
    public static class ThingSetMakerTests
    {
        private class ExposedMaker : ThingSetMaker_AnimalHusbandry
        {
            public ExposedMaker(HashSet<ThingDef> allowed) : base(allowed) { }
            public void CallGenerate(ThingSetMakerParams parms, List<Thing> outThings) => Generate(parms, outThings);
        }

        [EmpireTest("AH.Tithe")]
        public static void Generate_EmptyAllowedSet_ProducesNothing()
        {
            ExposedMaker maker = new ExposedMaker(new HashSet<ThingDef>());
            ThingSetMakerParams parms = new ThingSetMakerParams { filter = new ThingFilter() };

            List<Thing> outThings = new List<Thing>();
            maker.CallGenerate(parms, outThings);

            TestAssert.IsEmpty(outThings, "an empty allowed-animal set should generate no tithe things");
        }
    }
}
