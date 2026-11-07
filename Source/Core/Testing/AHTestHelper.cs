namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Shared helpers for the Herds &amp; Fisheries tests: a snapshot/restore for the mutable
/// <see cref="FCAHSettings"/> statics (so tests can pin a known configuration and still leave
/// settings as they found them), plus small fixture builders for the pure tests and real-def
/// lookups for the tests that need the game's def database (Core defs, so a missing one fails).
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
        s.basicAnimals = FCAHSettings.BasicAnimals is not null
            ? new List<ThingDef>(FCAHSettings.BasicAnimals)
            : null;
        return s;
    }

    public static void RestoreSettings(SettingsSnapshot s)
    {
        FCAHSettings.BasicAnimalsEnabled = s.basicAnimalsEnabled;
        FCAHSettings.RestrictFishToWater = s.restrictFishToWater;
        FCAHSettings.RestrictMercAnimalsToStocked = s.restrictMercAnimalsToStocked;
        FCAHSettings.BasicAnimals = s.basicAnimals is not null
            ? new List<ThingDef>(s.basicAnimals)
            : [];
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
    /// A real gendered animal kind whose race is NOT in <paramref name="excluded"/>, so a test can
    /// stock it from a clean slate. Every kind in <see cref="FactionCache.AllAnimalKindDefs"/> is a
    /// known animal, so its race passes <see cref="AnimalProductMap.IsKnownAnimal"/>. Fails the test
    /// if every gendered animal species is excluded.
    /// </summary>
    public static PawnKindDef RequireGenderedAnimalKind(ICollection<ThingDef> excluded = null)
    {
        PawnKindDef kind = FactionCache.AllAnimalKindDefs?
            .FirstOrDefault(k => k.race?.race is not null && k.race.race.Animal && k.race.race.hasGenders
                && (excluded is null || !excluded.Contains(k.race)));
        return TestAssert.NotNull(kind, "No gendered animal kind available outside the excluded species");
    }

    /// <summary>A Core animal race resolved by defName; fails the test if absent.</summary>
    public static ThingDef RequireAnimalRace(string defName)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        TestAssert.IsTrue(def?.race is not null && def.race.Animal, $"Animal race '{defName}' not present in this load");
        return def;
    }

    /// <summary>A Core ThingDef resolved by defName; fails the test if absent.</summary>
    public static ThingDef RequireThingDef(string defName) =>
        TestAssert.NotNull(DefDatabase<ThingDef>.GetNamedSilentFail(defName), $"ThingDef '{defName}' not present in this load");
}
