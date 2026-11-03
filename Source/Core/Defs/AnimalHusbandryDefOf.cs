using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    [DefOf]
    public static class AnimalHusbandryDefOf
    {
        public static BuildingFCDef CloningLab;

        public static ThingDef Chicken;
        public static ThingDef Cow;
        public static ThingDef Pig;
        public static ThingDef Sheep;
        public static ThingDef Goat;

        static AnimalHusbandryDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(AnimalHusbandryDefOf));
    }
}
