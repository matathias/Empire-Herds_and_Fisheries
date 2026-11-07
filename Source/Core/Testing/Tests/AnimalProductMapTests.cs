namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Coverage for <see cref="AnimalProductMap"/>'s race/product mapping and the
/// <see cref="AnimalProductMap.IsProductAllowed"/> gate. Needs the def database (the map is built
/// from <see cref="FactionCache.AllAnimalKindDefs"/>) but no faction. The cow, chicken and steel
/// defs it uses ship with Core, so their absence fails the test.
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

        if (cow.race.meatDef is not null)
            TestAssert.Contains(products, cow.race.meatDef, "cow products should include its meat");

        CompProperties_Milkable milkable = cow.GetCompProperties<CompProperties_Milkable>();
        if (milkable?.milkDef is not null)
            TestAssert.Contains(products, milkable.milkDef, "cow products should include its milk");
    }

    [EmpireTest("AH.Products")]
    public static void IsProductAllowed_ProducerInAllowedSet_Allowed()
    {
        ThingDef cow = AHTestHelper.RequireAnimalRace("Cow");
        ThingDef meat = TestAssert.NotNull(cow.race.meatDef, "Cow has no meatDef in this load");

        HashSet<ThingDef> allowsCow = [cow];
        TestAssert.IsTrue(AnimalProductMap.IsProductAllowed(meat, allowsCow),
            "meat should be allowed when its producing animal is in the allowed set");

        HashSet<ThingDef> allowsNothing = [];
        TestAssert.IsFalse(AnimalProductMap.IsProductAllowed(meat, allowsNothing),
            "meat should be blocked when no producing animal is allowed");
    }

    [EmpireTest("AH.Products")]
    public static void IsProductAllowed_FertilizedEgg_AlwaysBlocked()
    {
        ThingDef chicken = AHTestHelper.RequireAnimalRace("Chicken");
        CompProperties_EggLayer eggLayer = chicken.GetCompProperties<CompProperties_EggLayer>();
        TestAssert.NotNull(eggLayer?.eggFertilizedDef, "Chicken has no fertilized-egg def in this load");

        // Even with the producer explicitly allowed, a fertilized egg is never a deliverable product
        // (a delivered stack would hatch into a herd).
        HashSet<ThingDef> allowsChicken = [chicken];
        TestAssert.IsFalse(AnimalProductMap.IsProductAllowed(eggLayer.eggFertilizedDef, allowsChicken),
            "fertilized eggs should always be blocked as a product");

        if (eggLayer.eggUnfertilizedDef is not null)
            TestAssert.IsTrue(AnimalProductMap.IsProductAllowed(eggLayer.eggUnfertilizedDef, allowsChicken),
                "unfertilized (food) eggs should be allowed when the producer is allowed");
    }
}
