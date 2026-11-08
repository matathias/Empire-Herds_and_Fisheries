namespace FactionColonies.AnimalHusbandry;

/* DESTRUCTIVE: drives WorldObjectComp_AnimalRegistry against a live transient settlement. Each test
   creates a settlement carrying the registry, pins BasicAnimalsEnabled=false to isolate stocking
   from the basic-animal set, stocks a species not already available there, mutates stock / receives
   pawns, then restores settings, removes any
   caravans it spawned, and removes the settlement. Uncleaned residue is possible if a test throws. */
public static class AnimalRegistryDestructiveTests
{
    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void AddIndividual_CompletePair_RegistersAndEnablesProduction()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            comp.AddIndividual(species, Gender.Male);
            TestAssert.IsFalse(comp.RegisteredAnimals.Contains(species), "a lone male should not complete the pair");

            comp.AddIndividual(species, Gender.Female);
            TestAssert.IsTrue(comp.RegisteredAnimals.Contains(species), "male + female should register the species");

            ResourceFC animals = s.GetResource(ResourceTypeDefOf.RTD_Animals);
            if (animals is not null)
                TestAssert.AreEqual(1.0, comp.GetResourceMultiplierModifier(animals),
                    message: "a registered species should enable animal production (multiplier 1)");

            DestructiveTestUtil.AssertEmpireInvariants(f, "AddIndividual_CompletePair");
        }
        finally
        {
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void ResourceMultiplier_ZeroWhenNoAnimalsOrFish()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // A landlocked tile so restricted fishing yields no fish under Odyssey.
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp,
            ModsConfig.OdysseyActive ? AHDestructiveTestUtil.IsLandlocked : null);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        List<(WorldSettlementFC settlement, int slot)> suspendedLabs = null;
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            FCAHSettings.RestrictFishToWater = true;
            // Cloning Labs elsewhere would share their species here; take them down for the check.
            suspendedLabs = AHDestructiveTestUtil.SuspendCloningLabs();

            TestAssert.IsFalse(comp.HasAnyAnimals(), "a fresh settlement with no basic or cloned animals should have none");
            TestAssert.IsFalse(comp.CanProduceFish(), "a landlocked settlement with restricted fishing should have no fish");

            ResourceFC animals = TestAssert.NotNull(s.GetResource(ResourceTypeDefOf.RTD_Animals),
                "Settlement has no Animals resource");

            TestAssert.AreEqual(0.0, comp.GetResourceMultiplierModifier(animals),
                message: "no animals and no fish should zero the animal multiplier");
            TestAssert.IsNotNull(comp.GetResourceMultiplierDesc(animals),
                "the zero case should report a 'no animals' description");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceMultiplier_Zero");
        }
        finally
        {
            AHDestructiveTestUtil.RestoreCloningLabs(suspendedLabs);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void StockIndividual_ConsumesPawnAndRecordsSex()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            Pawn pawn = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            if (!comp.AcceptsPawn(pawn))
            {
                if (!pawn.Destroyed) pawn.Destroy();
                TestAssert.Fail($"Settlement did not accept a male {species.defName}, a species it does not have yet");
            }

            comp.StockIndividual(pawn);
            TestAssert.IsTrue(pawn.Destroyed, "a stocked pawn should be consumed (destroyed)");
            TestAssert.IsFalse(comp.StillNeeds(species, Gender.Male), "the male slot should now be filled");

            DestructiveTestUtil.AssertEmpireInvariants(f, "StockIndividual");
        }
        finally
        {
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void ReceivePawns_OverflowFormsCaravan()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            Pawn male = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            Pawn female = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Female);
            Pawn extraMale = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);

            int before = Find.WorldObjects.Caravans.Count(c => c.Tile == s.Tile);
            comp.ReceivePawns([male, female, extraMale]);

            TestAssert.IsTrue(comp.RegisteredAnimals.Contains(species), "the arriving pair should complete");
            int after = Find.WorldObjects.Caravans.Count(c => c.Tile == s.Tile);
            TestAssert.GreaterThan(after, before, "the overflow individual should form a caravan at the tile");

            DestructiveTestUtil.AssertEmpireInvariants(f, "ReceivePawns_Overflow");
        }
        finally
        {
            AHDestructiveTestUtil.RemoveCaravansAt(s.Tile);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void AcceptsPawn_RespectsAvailabilityAndSex()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        Pawn male = null, male2 = null, female = null;
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            TestAssert.IsFalse(comp.AcceptsPawn(null), "a null pawn should be rejected");

            male = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            TestAssert.IsTrue(comp.AcceptsPawn(male), "a needed species/sex should be accepted");

            comp.AddIndividual(species, Gender.Male);   // fill the male slot without consuming a pawn
            male2 = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            TestAssert.IsFalse(comp.AcceptsPawn(male2), "an already-stocked sex should be rejected");

            female = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Female);
            TestAssert.IsTrue(comp.AcceptsPawn(female), "the still-missing sex should be accepted");

            DestructiveTestUtil.AssertEmpireInvariants(f, "AcceptsPawn_Contract");
        }
        finally
        {
            // These probe pawns were generated but never stocked; discard them.
            if (male is not null && !male.Destroyed) male.Destroy();
            if (male2 is not null && !male2.Destroyed) male2.Destroy();
            if (female is not null && !female.Destroyed) female.Destroy();
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void GetContributableSpecies_ListsNeededCaravanSpecies()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            FCAHSettings.BasicAnimalsEnabled = false;
            // Species shared empire-wide by a Cloning Lab are already available here; stock one that is not.
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind(comp.GetAllowedAnimals());
            ThingDef species = kind.race;

            Pawn male = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
            Pawn female = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Female);
            Caravan caravan = CaravanMaker.MakeCaravan(
                new List<Pawn> { male, female }, Faction.OfPlayer, s.Tile, false);

            List<ThingDef> contributable = comp.GetContributableSpecies(caravan);
            TestAssert.Contains(contributable, species,
                "a caravan carrying a needed pair should list the species as contributable");

            DestructiveTestUtil.AssertEmpireInvariants(f, "GetContributableSpecies");
        }
        finally
        {
            AHDestructiveTestUtil.RemoveCaravansAt(s.Tile);
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("AH.Destructive.Registry")]
    public static void FishProduction_NoneWithoutOdysseyOrWater()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // Under Odyssey, a landlocked tile is the case with a fixed answer (no water -> no fish).
        WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp,
            ModsConfig.OdysseyActive ? AHDestructiveTestUtil.IsLandlocked : null);

        AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
        try
        {
            if (ModsConfig.OdysseyActive)
            {
                TestAssert.IsEmpty(comp.GetAllowedFish(), "a landlocked settlement should have no allowed fish");

                FCAHSettings.RestrictFishToWater = true;
                TestAssert.IsFalse(comp.CanProduceFish(), "restricted fishing needs water access");

                FCAHSettings.RestrictFishToWater = false;
                TestAssert.IsTrue(comp.CanProduceFish(), "unrestricted fishing enables fish at any settlement");
            }
            else
            {
                foreach (bool restrict in new[] { true, false })
                {
                    FCAHSettings.RestrictFishToWater = restrict;
                    TestAssert.IsFalse(comp.CanProduceFish(), $"without Odyssey there is no fish production (restrict={restrict})");
                }
                TestAssert.IsEmpty(comp.GetAllowedFish(), "without Odyssey the allowed-fish set is empty");
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "FishProduction_NoOdysseyOrWater");
        }
        finally
        {
            AHTestHelper.RestoreSettings(snap);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }
}
