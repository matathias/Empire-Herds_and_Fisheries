using System.Collections.Generic;
using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Coverage for <see cref="AnimalProductMap"/>'s race/product mapping and the
    /// <see cref="AnimalProductMap.IsProductAllowed"/> gate. Needs the def database (the map is built
    /// from <see cref="FactionCache.AllAnimalKindDefs"/>) but no faction; every test skips if the real
    /// defs it needs are absent from the current load.
    /// </summary>
    public static class AnimalProductMapTests
    {
        [EmpireTest("AH.Products")]
        public static void IsKnownAnimal_TrueForAnimalRace_FalseForNonAnimal()
        {
            ThingDef cow = AHTestHelper.RequireAnimalRace("Cow");
            TestAssert.IsTrue(AnimalProductMap.IsKnownAnimal(cow), "a real animal race should be known");

            ThingDef steel = AHTestHelper.RequireThingDef("Steel");
            TestAssert.IsFalse(AnimalProductMap.IsKnownAnimal(steel), "a non-animal thing should not be a known animal");
        }

        [EmpireTest("AH.Products")]
        public static void GetProducts_MapsMeatAndMilkForMilkableRace()
        {
            ThingDef cow = AHTestHelper.RequireAnimalRace("Cow");
            HashSet<ThingDef> products = AnimalProductMap.GetProducts(cow);
            TestAssert.IsNotNull(products, "a known animal should have a product set");

            if (cow.race.meatDef is object)
                TestAssert.Contains(products, cow.race.meatDef, "cow products should include its meat");

            CompProperties_Milkable milkable = cow.GetCompProperties<CompProperties_Milkable>();
            if (milkable?.milkDef is object)
                TestAssert.Contains(products, milkable.milkDef, "cow products should include its milk");
        }

        [EmpireTest("AH.Products")]
        public static void IsProductAllowed_ProducerInAllowedSet_Allowed()
        {
            ThingDef cow = AHTestHelper.RequireAnimalRace("Cow");
            if (cow.race.meatDef is null) TestAssert.Skip("Cow has no meatDef in this load");
            ThingDef meat = cow.race.meatDef;

            HashSet<ThingDef> allowsCow = new HashSet<ThingDef> { cow };
            TestAssert.IsTrue(AnimalProductMap.IsProductAllowed(meat, allowsCow),
                "meat should be allowed when its producing animal is in the allowed set");

            HashSet<ThingDef> allowsNothing = new HashSet<ThingDef>();
            TestAssert.IsFalse(AnimalProductMap.IsProductAllowed(meat, allowsNothing),
                "meat should be blocked when no producing animal is allowed");
        }

        [EmpireTest("AH.Products")]
        public static void IsProductAllowed_FertilizedEgg_AlwaysBlocked()
        {
            ThingDef chicken = AHTestHelper.RequireAnimalRace("Chicken");
            CompProperties_EggLayer eggLayer = chicken.GetCompProperties<CompProperties_EggLayer>();
            if (eggLayer?.eggFertilizedDef is null)
                TestAssert.Skip("Chicken has no fertilized-egg def in this load");

            // Even with the producer explicitly allowed, a fertilized egg is never a deliverable product
            // (a delivered stack would hatch into a herd).
            HashSet<ThingDef> allowsChicken = new HashSet<ThingDef> { chicken };
            TestAssert.IsFalse(AnimalProductMap.IsProductAllowed(eggLayer.eggFertilizedDef, allowsChicken),
                "fertilized eggs should always be blocked as a product");

            if (eggLayer.eggUnfertilizedDef is object)
                TestAssert.IsTrue(AnimalProductMap.IsProductAllowed(eggLayer.eggUnfertilizedDef, allowsChicken),
                    "unfertilized (food) eggs should be allowed when the producer is allowed");
        }
    }
}
