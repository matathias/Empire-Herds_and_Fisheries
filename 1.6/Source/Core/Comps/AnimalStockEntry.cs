using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Per-species stocking progress at one settlement. Each entry owns a reference to the species
    /// it tracks, so the comp stores a flat list rather than a keyed dictionary. Gendered species
    /// need both a male and a female before they produce; the player is blocked from over-stocking a
    /// sex already present, so each sex is a binary slot (bool). Genderless species need any two
    /// individuals, tracked as a count capped at 2.
    /// </summary>
    public class AnimalStockEntry : IExposable
    {
        public ThingDef species;
        public bool hasMale;
        public bool hasFemale;
        public int genderless;

        public AnimalStockEntry()
        {
        }

        public AnimalStockEntry(ThingDef species)
        {
            this.species = species;
        }

        private bool HasGenders
        {
            get { return species is object && species.race is object && species.race.hasGenders; }
        }

        /// <summary>True once this species can produce: a male and a female (gendered), or 2+ (genderless).</summary>
        public bool IsComplete
        {
            get { return HasGenders ? (hasMale && hasFemale) : genderless >= 2; }
        }

        /// <summary>
        /// Whether the settlement still needs an individual of this gender — the single source of
        /// truth for "may the player stock this combo." Genderless ignores gender.
        /// </summary>
        public bool Needs(Gender gender)
        {
            if (HasGenders)
            {
                if (gender == Gender.Male) return !hasMale;
                if (gender == Gender.Female) return !hasFemale;
                return false;
            }
            return genderless < 2;
        }

        /// <summary>
        /// Records one received individual. Returns false (no-op) when that combo is already stocked,
        /// so callers can discard a freshly-built entry that turned out to add nothing.
        /// </summary>
        public bool Add(Gender gender)
        {
            if (!Needs(gender)) return false;
            if (HasGenders)
            {
                if (gender == Gender.Male) hasMale = true;
                else if (gender == Gender.Female) hasFemale = true;
            }
            else
            {
                genderless++;
            }
            return true;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref species, "species");
            Scribe_Values.Look(ref hasMale, "hasMale", false);
            Scribe_Values.Look(ref hasFemale, "hasFemale", false);
            Scribe_Values.Look(ref genderless, "genderless", 0);
        }
    }
}
