namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Coverage for <see cref="WorldObjectComp_AnimalRegistry.CanStock"/>, the single stocking rule used by
/// pod arrival (<see cref="WorldObjectComp_AnimalRegistry.AcceptsPawn"/> and the per-pawn re-check in
/// ReceivePawns) and by StockIndividual. Runs on a parentless comp: the rule reads only the comp's own
/// stock, the Basic Animals settings and the empire-wide Cloning Lab set, so no settlement is needed.
/// Settings are snapshotted and restored.
/// </summary>
public static class CanStockTests
{
    [EmpireTest("AH.CanStock")]
    public static void CanStock_NullOrNonAnimal_False()
    {
        WorldObjectComp_AnimalRegistry comp = new();
        TestAssert.IsFalse(comp.CanStock(null, Gender.Male), "a null species should never be stockable");
        TestAssert.IsFalse(comp.CanStock(ThingDefOf.Silver, Gender.None), "a non-pawn def should never be stockable");
        TestAssert.IsFalse(comp.CanStock(ThingDefOf.Human, Gender.Male), "a humanlike race should never be stockable");
    }

    [EmpireTest("AH.CanStock")]
    public static void CanStock_AnimalOutsideProductMap_False()
    {
        // A fixture animal that is not in the DefDatabase, so the product map does not know it: the same
        // outcome as a dryad or blacklisted race. Before CanStock, ReceivePawns skipped this check.
        ThingDef unknown = AHTestHelper.MakeAnimalDef(true, "AHTest_CanStockUnknown");
        TestAssert.IsTrue(unknown.race.Animal, "the fixture race should read as an animal");
        TestAssert.IsFalse(AnimalProductMap.IsKnownAnimal(unknown), "the fixture race should be unknown to the product map");

        WorldObjectComp_AnimalRegistry comp = new();
        TestAssert.IsFalse(comp.CanStock(unknown, Gender.Male),
            "an animal the product map excludes should not be stockable");
    }

    [EmpireTest("AH.CanStock")]
    public static void CanStock_BasicAnimal_FalseOnlyWhileBasicEnabled()
    {
        WorldObjectComp_AnimalRegistry comp = new();
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            FCAHSettings.BasicAnimals = [species];
            TestAssert.IsTrue(comp.CanStock(species, Gender.Male),
                "with Basic Animals off, a species on the basic list is still stockable");

            FCAHSettings.BasicAnimalsEnabled = true;
            TestAssert.IsFalse(comp.CanStock(species, Gender.Male),
                "a species already available as a basic animal should not be stockable");
            TestAssert.IsFalse(comp.CanStock(species, Gender.Female),
                "a basic animal blocks both sexes");
        }
        finally
        {
            AHTestHelper.RestoreSettings(snap);
        }
    }

    [EmpireTest("AH.CanStock")]
    public static void CanStock_TracksTheStillNeededSex()
    {
        WorldObjectComp_AnimalRegistry comp = new();
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            TestAssert.IsTrue(comp.CanStock(species, Gender.Male), "an empty registry needs a male");
            TestAssert.IsTrue(comp.CanStock(species, Gender.Female), "an empty registry needs a female");

            comp.AddIndividual(species, Gender.Male);
            TestAssert.IsFalse(comp.CanStock(species, Gender.Male), "a stocked male should block another male");
            TestAssert.IsTrue(comp.CanStock(species, Gender.Female), "the female slot should still be open");

            comp.AddIndividual(species, Gender.Female);
            TestAssert.IsFalse(comp.CanStock(species, Gender.Female),
                "a completed pair makes the species available, so nothing more is stockable");
            TestAssert.IsFalse(comp.CanStock(species, Gender.Male),
                "a completed pair makes the species available, so nothing more is stockable");
        }
        finally
        {
            AHTestHelper.RestoreSettings(snap);
        }
    }

    [EmpireTest("AH.CanStock")]
    public static void AcceptsPawn_Null_False()
    {
        WorldObjectComp_AnimalRegistry comp = new();
        TestAssert.IsFalse(comp.AcceptsPawn(null), "a null pawn should be rejected, not throw");
    }

    /// <summary>
    /// The stock filter is registered once at startup as a persistent participant, so it must still be
    /// registered in a loaded game (every load clears the registries before re-adding persistent ones).
    /// </summary>
    [EmpireTest("AH.CanStock")]
    public static void MercAnimalStockFilter_StaysRegisteredAfterLoad()
    {
        TestAssert.Contains(AnimalPickerFilterRegistry.Filters, (IAnimalPickerFilter)MercAnimalStockFilter.Instance,
            "the animal stock filter should survive the registry reset that runs on every load");
    }
}
