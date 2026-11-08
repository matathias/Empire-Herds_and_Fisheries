namespace FactionColonies.AnimalHusbandry;

/* DESTRUCTIVE: exercises the Cloning Lab sharing path against a live transient settlement. Builds a
   Cloning Lab (firing SettlementBuildingComp_CloningLab.OnConstruct), registers a breeding pair, and
   checks CloningCache. Each test deconstructs the lab and removes the settlement afterward; residue is
   possible if a test throws partway. */
public static class CloningDestructiveTests
{
    [EmpireDestructiveTest("AH.Destructive.Cloning")]
    public static void CloningLab_SharesRegisteredSpeciesEmpireWide()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        int slot = -1;
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            slot = AHDestructiveTestUtil.AttachCloningLab(s);
            TestAssert.IsTrue(s.BuildingsComp.HasBuilding(AnimalHusbandryDefOf.CloningLab),
                "the Cloning Lab should be present after construction");

            comp.AddIndividual(species, Gender.Male);
            comp.AddIndividual(species, Gender.Female);
            CloningCache.Invalidate();

            TestAssert.Contains(CloningCache.GetClonedAnimals(), species,
                "a species registered at a Cloning-Lab settlement should be shared empire-wide");

            DestructiveTestUtil.AssertEmpireInvariants(f, "CloningLab_Shares");
        }
        finally
        {
            if (slot >= 0 && s is not null) SafeDeconstruct(s, slot);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Cloning")]
    public static void CloningLab_Deconstruct_StopsSharingSpecies()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        int slot = -1;
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            slot = AHDestructiveTestUtil.AttachCloningLab(s);

            comp.AddIndividual(species, Gender.Male);
            comp.AddIndividual(species, Gender.Female);
            CloningCache.Invalidate();
            TestAssert.Contains(CloningCache.GetClonedAnimals(), species,
                "species should be shared while the lab stands");

            // Deconstruct fires SettlementBuildingComp_CloningLab.OnDeconstruct, which invalidates the
            // cache. If it did not, the stale cached set would still contain the species below.
            s.BuildingsComp.DeconstructBuilding(slot);
            slot = -1;
            TestAssert.IsFalse(s.BuildingsComp.HasBuilding(AnimalHusbandryDefOf.CloningLab),
                "the Cloning Lab should be gone after deconstruction");
            TestAssert.IsFalse(CloningCache.GetClonedAnimals().Contains(species),
                "removing the Cloning Lab should stop sharing the species empire-wide");

            DestructiveTestUtil.AssertEmpireInvariants(f, "CloningLab_Deconstruct");
        }
        finally
        {
            if (slot >= 0 && s is not null) SafeDeconstruct(s, slot);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    private static void SafeDeconstruct(WorldSettlementFC settlement, int slot)
    {
        try { settlement.BuildingsComp.DeconstructBuilding(slot); }
        catch (Exception ex) { LogAH.Warning($"Cloning Lab cleanup deconstruct threw (ignored): {ex}"); }
    }
}
