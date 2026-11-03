using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Shared helpers for the Herds &amp; Fisheries tests: a snapshot/restore for the mutable
    /// <see cref="FCAHSettings"/> statics (so tests can pin a known configuration and still leave
    /// settings as they found them), plus small fixture builders for the pure tests and real-def
    /// lookups (skip-guarded) for the tests that need the game's def database.
    /// </summary>
    public static class AHTestHelper
    {
        /// <summary>Captured values of the settings statics that the tests may overwrite.</summary>
        public struct SettingsSnapshot
        {
            public bool basicAnimalsEnabled;
            public bool restrictFishToWater;
            public bool restrictMercAnimalsToStocked;
            public List<ThingDef> basicAnimals;
        }

        public static SettingsSnapshot SnapshotSettings()
        {
            SettingsSnapshot s;
            s.basicAnimalsEnabled = FCAHSettings.BasicAnimalsEnabled;
            s.restrictFishToWater = FCAHSettings.RestrictFishToWater;
            s.restrictMercAnimalsToStocked = FCAHSettings.RestrictMercAnimalsToStocked;
            // Copy the list itself: tests mutate BasicAnimals in place, so keep an independent copy.
            s.basicAnimals = FCAHSettings.BasicAnimals is object
                ? new List<ThingDef>(FCAHSettings.BasicAnimals)
                : null;
            return s;
        }

        public static void RestoreSettings(SettingsSnapshot s)
        {
            FCAHSettings.BasicAnimalsEnabled = s.basicAnimalsEnabled;
            FCAHSettings.RestrictFishToWater = s.restrictFishToWater;
            FCAHSettings.RestrictMercAnimalsToStocked = s.restrictMercAnimalsToStocked;
            FCAHSettings.BasicAnimals = s.basicAnimals is object
                ? new List<ThingDef>(s.basicAnimals)
                : new List<ThingDef>();
            // The basic-animal set feeds StockedAnimalCache; drop it so the restored config takes effect.
            StockedAnimalCache.Invalidate();
        }

        /// <summary>
        /// A throwaway animal <see cref="ThingDef"/> for the pure <see cref="AnimalStockEntry"/> tests.
        /// Reference identity and <c>race.hasGenders</c> are all those tests rely on, so it never needs
        /// to be registered in the DefDatabase.
        /// </summary>
        public static ThingDef MakeAnimalDef(bool hasGenders, string defName = "AHTest_Animal")
        {
            return new ThingDef
            {
                defName = defName,
                label = defName,
                race = new RaceProperties { hasGenders = hasGenders }
            };
        }

        /// <summary>A throwaway <see cref="PawnKindDef"/> whose <c>race</c> is the given def.</summary>
        public static PawnKindDef MakePawnKind(ThingDef race, string defName = "AHTest_Kind")
        {
            return new PawnKindDef { defName = defName, label = defName, race = race };
        }

        /// <summary>
        /// A real animal kind from the game's animal list whose race has genders, or skips the test.
        /// Every kind in <see cref="FactionCache.AllAnimalKindDefs"/> is a known animal, so its race
        /// passes <see cref="AnimalProductMap.IsKnownAnimal"/>.
        /// </summary>
        public static PawnKindDef RequireGenderedAnimalKind()
        {
            PawnKindDef kind = FactionCache.AllAnimalKindDefs?
                .FirstOrDefault(k => k.race?.race is object && k.race.race.Animal && k.race.race.hasGenders);
            if (kind is null) TestAssert.Skip("No gendered animal kind available in the def database");
            return kind;
        }

        /// <summary>A real animal race resolved by defName, or skips the test if absent.</summary>
        public static ThingDef RequireAnimalRace(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def?.race is null || !def.race.Animal)
                TestAssert.Skip($"Animal race '{defName}' not present in this load");
            return def;
        }

        /// <summary>A ThingDef resolved by defName, or skips the test if absent.</summary>
        public static ThingDef RequireThingDef(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def is null) TestAssert.Skip($"ThingDef '{defName}' not present in this load");
            return def;
        }
    }
}
