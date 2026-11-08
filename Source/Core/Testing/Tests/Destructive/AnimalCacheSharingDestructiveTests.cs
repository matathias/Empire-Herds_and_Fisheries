namespace FactionColonies.AnimalHusbandry;

/* DESTRUCTIVE: checks that changes to the animals available at more than one settlement reach every
   settlement's animal tithe picker, and that the register dialog notices a caravan that is gone. Each
   test creates transient settlements (and a Cloning Lab or caravan where needed), pins Basic Animals off,
   and removes what it created. Residue is possible if a test throws partway. */
public static class AnimalCacheSharingDestructiveTests
{
    /// <summary>
    /// The tithe picker list is rebuilt only when dirty; a rebuild returns a fresh list. So an unchanged
    /// reference means the cache was not dirtied.
    /// </summary>
    private static List<ThingDef> TithePickerList(WorldSettlementFC settlement) =>
        TestAssert.NotNull(settlement.GetResource(ResourceTypeDefOf.RTD_Animals), $"{settlement.Name} has no Animals resource")
            .GenerateThingDefList();

    [EmpireDestructiveTest("AH.Destructive.Sharing")]
    public static void InvalidateAllSettlementAnimalCaches_DirtiesEveryTithePicker()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out _);
        try
        {
            List<ThingDef> first = TithePickerList(s);
            TestAssert.IsTrue(ReferenceEquals(first, TithePickerList(s)),
                "the tithe picker list should be cached between reads");

            WorldObjectComp_AnimalRegistry.InvalidateAllSettlementAnimalCaches();
            TestAssert.IsFalse(ReferenceEquals(first, TithePickerList(s)),
                "InvalidateAllSettlementAnimalCaches should dirty the settlement's animal tithe picker");

            DestructiveTestUtil.AssertEmpireInvariants(f, "InvalidateAllSettlementAnimalCaches");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /// <summary>
    /// A pair completed at a Cloning-Lab settlement becomes available everywhere, so another settlement's
    /// tithe picker must be rebuilt and its registry must list the species.
    /// </summary>
    [EmpireDestructiveTest("AH.Destructive.Sharing")]
    public static void CompletingPairAtCloningLab_RefreshesOtherSettlements()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC lab = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var labComp);
        WorldSettlementFC other = null;
        int slot = -1;
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            other = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var otherComp);
            FCAHSettings.BasicAnimalsEnabled = false;

            slot = AHDestructiveTestUtil.AttachCloningLab(lab);
            HashSet<ThingDef> excluded = labComp.GetAllowedAnimals();
            excluded.UnionWith(otherComp.GetAllowedAnimals());
            ThingDef species = AHTestHelper.RequireGenderedAnimalKind(excluded).race;

            List<ThingDef> before = TithePickerList(other);
            TestAssert.IsTrue(ReferenceEquals(before, TithePickerList(other)),
                "the other settlement's tithe picker list should be cached before the pair completes");

            labComp.AddIndividual(species, Gender.Male);
            labComp.AddIndividual(species, Gender.Female);

            TestAssert.IsFalse(ReferenceEquals(before, TithePickerList(other)),
                "completing a pair at a Cloning-Lab settlement should dirty other settlements' tithe pickers");
            TestAssert.Contains(otherComp.GetAllowedAnimals(), species,
                "the cloned species should be available at the other settlement");

            ResourceFC animals = other.GetResource(ResourceTypeDefOf.RTD_Animals);
            TestAssert.AreEqual(1.0, otherComp.GetResourceMultiplierModifier(animals),
                message: "the cloned species should enable animal production at the other settlement");

            DestructiveTestUtil.AssertEmpireInvariants(f, "CompletingPairAtCloningLab");
        }
        finally
        {
            if (slot >= 0 && !lab.Destroyed)
            {
                try { lab.BuildingsComp.DeconstructBuilding(slot); }
                catch (Exception ex) { LogAH.Warning($"Cloning Lab cleanup deconstruct threw (ignored): {ex}"); }
            }
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(other);
            DestructiveTestUtil.SafeRemoveSettlement(lab);
        }
    }

    /// <summary>
    /// The register dialog only stocks from a caravan still at the settlement: one that moved away or was
    /// destroyed must read as gone.
    /// </summary>
    [EmpireDestructiveTest("AH.Destructive.Sharing")]
    public static void RegisterDialog_CaravanStillHere_FalseOnceCaravanLeavesOrIsGone()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        Caravan caravan = null;
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());

            Pawn male = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            Pawn female = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Female);
            caravan = CaravanMaker.MakeCaravan([male, female], Faction.OfPlayer, s.Tile, false);

            Dialog_RegisterAnimals dialog = new(caravan, comp, comp.GetContributableSpecies(caravan));
            TestAssert.IsTrue(dialog.CaravanStillHere, "a caravan on the settlement tile should count as still here");

            List<PlanetTile> neighbors = [];
            Find.WorldGrid.GetTileNeighbors(s.Tile, neighbors);
            TestAssert.IsNotEmpty(neighbors, "the settlement tile should have neighbors");
            caravan.Tile = neighbors[0];
            TestAssert.IsFalse(dialog.CaravanStillHere, "a caravan that moved off the settlement tile should not count");

            caravan.Tile = s.Tile;
            caravan.Destroy();
            TestAssert.IsFalse(dialog.CaravanStillHere, "a destroyed caravan should not count");

            DestructiveTestUtil.AssertEmpireInvariants(f, "RegisterDialog_CaravanStillHere");
        }
        finally
        {
            if (caravan is not null && !caravan.Destroyed)
            {
                try { caravan.Destroy(); }
                catch (Exception ex) { LogAH.Warning($"caravan cleanup threw (ignored): {ex}"); }
            }
            AHDestructiveTestUtil.RemoveCaravansAt(s.Tile);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }
}
