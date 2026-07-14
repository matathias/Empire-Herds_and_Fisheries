using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /* DESTRUCTIVE: drives WorldObjectComp_AnimalRegistry against a live transient settlement. Each test
       creates a settlement carrying the registry, pins BasicAnimalsEnabled=false to isolate stocking
       from the basic-animal set, mutates stock / receives pawns, then restores settings, removes any
       caravans it spawned, and removes the settlement. Uncleaned residue is possible if a test throws. */
    public static class AnimalRegistryDestructiveTests
    {
        [EmpireDestructiveTest("AH.Destructive.Registry")]
        public static void AddIndividual_CompletePair_RegistersAndEnablesProduction()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind();
            ThingDef species = kind.race;

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.GetAllowedAnimals().Contains(species))
                    TestAssert.Skip("Test species is already available here (cloned elsewhere?)");

                comp.AddIndividual(species, Gender.Male);
                TestAssert.IsFalse(comp.RegisteredAnimals.Contains(species), "a lone male should not complete the pair");

                comp.AddIndividual(species, Gender.Female);
                TestAssert.IsTrue(comp.RegisteredAnimals.Contains(species), "male + female should register the species");

                ResourceFC animals = s.GetResource(ResourceTypeDefOf.RTD_Animals);
                if (animals is object)
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

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.HasAnyAnimals() || comp.CanProduceFish())
                    TestAssert.Skip("Settlement already produces animals/fish; cannot isolate the zero case");

                ResourceFC animals = s.GetResource(ResourceTypeDefOf.RTD_Animals);
                if (animals is null) TestAssert.Skip("Settlement has no Animals resource");

                TestAssert.AreEqual(0.0, comp.GetResourceMultiplierModifier(animals),
                    message: "no animals and no fish should zero the animal multiplier");
                TestAssert.IsNotNull(comp.GetResourceMultiplierDesc(animals),
                    "the zero case should report a 'no animals' description");

                DestructiveTestUtil.AssertEmpireInvariants(f, "ResourceMultiplier_Zero");
            }
            finally
            {
                AHTestHelper.RestoreSettings(snap);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("AH.Destructive.Registry")]
        public static void StockIndividual_ConsumesPawnAndRecordsSex()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind();
            ThingDef species = kind.race;

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.GetAllowedAnimals().Contains(species))
                    TestAssert.Skip("Test species is already available here (cloned elsewhere?)");

                Pawn pawn = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
                if (!comp.AcceptsPawn(pawn))
                {
                    if (!pawn.Destroyed) pawn.Destroy();
                    TestAssert.Skip("Settlement did not accept the generated test pawn");
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
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind();
            ThingDef species = kind.race;

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.GetAllowedAnimals().Contains(species))
                    TestAssert.Skip("Test species is already available here (cloned elsewhere?)");

                Pawn male = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);
                Pawn female = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Female);
                Pawn extraMale = AHDestructiveTestUtil.MakeAnimalPawn(kind, Gender.Male);

                int before = Find.WorldObjects.Caravans.Count(c => c.Tile == s.Tile);
                comp.ReceivePawns(new List<Pawn> { male, female, extraMale });

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
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind();
            ThingDef species = kind.race;

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            Pawn male = null, male2 = null, female = null;
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.GetAllowedAnimals().Contains(species))
                    TestAssert.Skip("Test species is already available here (cloned elsewhere?)");

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
                if (male is object && !male.Destroyed) male.Destroy();
                if (male2 is object && !male2.Destroyed) male2.Destroy();
                if (female is object && !female.Destroyed) female.Destroy();
                AHTestHelper.RestoreSettings(snap);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("AH.Destructive.Registry")]
        public static void GetContributableSpecies_ListsNeededCaravanSpecies()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            PawnKindDef kind = AHTestHelper.RequireGenderedAnimalKind();
            ThingDef species = kind.race;

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            AHTestHelper.SettingsSnapshot snap = AHTestHelper.SnapshotSettings();
            try
            {
                FCAHSettings.BasicAnimalsEnabled = false;
                if (comp.GetAllowedAnimals().Contains(species))
                    TestAssert.Skip("Test species is already available here (cloned elsewhere?)");

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
        public static void FishProduction_NoneWithoutOdyssey()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC s = AHDestructiveTestUtil.CreateSettlementWithRegistry(out var comp);
            if (s is null) TestAssert.Skip("Could not create a settlement carrying the animal registry");

            try
            {
                if (ModsConfig.OdysseyActive)
                    TestAssert.Skip("Odyssey active; fish availability is tile-dependent and covered by play-testing");

                TestAssert.IsFalse(comp.CanProduceFish(), "without Odyssey there is no fish production");
                TestAssert.IsEmpty(comp.GetAllowedFish(), "without Odyssey the allowed-fish set is empty");

                DestructiveTestUtil.AssertEmpireInvariants(f, "FishProduction_NoOdyssey");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
