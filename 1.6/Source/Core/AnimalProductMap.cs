using System.Collections.Generic;
using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Static cache mapping animal race ThingDefs to their products and vice versa.
    /// Built once at startup via EnsureBuilt(). Cleared via EmpireCacheUtil callback.
    /// </summary>
    public static class AnimalProductMap
    {
        private static Dictionary<ThingDef, HashSet<ThingDef>> animalToProducts;
        private static Dictionary<ThingDef, HashSet<ThingDef>> productToAnimals;
        private static bool built = false;

        public static void EnsureBuilt()
        {
            if (built) return;
            Build();
        }

        public static void Clear()
        {
            animalToProducts = null;
            productToAnimals = null;
            built = false;
        }

        private static void Build()
        {
            animalToProducts = new Dictionary<ThingDef, HashSet<ThingDef>>();
            productToAnimals = new Dictionary<ThingDef, HashSet<ThingDef>>();

            foreach (PawnKindDef kind in FactionCache.AllAnimalKindDefs)
            {
                ThingDef race = kind.race;
                if (race is null) continue;
                if (!animalToProducts.ContainsKey(race))
                    animalToProducts[race] = new HashSet<ThingDef>();
                HashSet<ThingDef> products = animalToProducts[race];

                if (race.race?.meatDef is object)
                    AddMapping(race, race.race.meatDef, products);
                if (race.race?.leatherDef is object)
                    AddMapping(race, race.race.leatherDef, products);

                CompProperties_Shearable shearable = race.GetCompProperties<CompProperties_Shearable>();
                if (shearable?.woolDef is object)
                    AddMapping(race, shearable.woolDef, products);

                CompProperties_Milkable milkable = race.GetCompProperties<CompProperties_Milkable>();
                if (milkable?.milkDef is object)
                    AddMapping(race, milkable.milkDef, products);

                CompProperties_EggLayer eggLayer = race.GetCompProperties<CompProperties_EggLayer>();
                if (eggLayer?.eggUnfertilizedDef is object)
                    AddMapping(race, eggLayer.eggUnfertilizedDef, products);
                if (eggLayer?.eggFertilizedDef is object)
                    AddMapping(race, eggLayer.eggFertilizedDef, products);
            }

            built = true;
            LogAH.Message($"AnimalProductMap built: {animalToProducts.Count} animals, {productToAnimals.Count} products");
        }

        private static void AddMapping(ThingDef race, ThingDef product, HashSet<ThingDef> products)
        {
            products.Add(product);
            if (!productToAnimals.ContainsKey(product))
                productToAnimals[product] = new HashSet<ThingDef>();
            productToAnimals[product].Add(race);
        }

        public static HashSet<ThingDef> GetProducts(ThingDef animalRace)
        {
            EnsureBuilt();
            HashSet<ThingDef> result;
            if (animalToProducts.TryGetValue(animalRace, out result))
                return result;
            return null;
        }

        /// <summary>
        /// True if any animal producing this product is in the allowed set.
        /// Products with no known animal source are always allowed (safety for modded content).
        /// </summary>
        public static bool IsProductAllowed(ThingDef product, HashSet<ThingDef> allowedAnimals)
        {
            EnsureBuilt();
            HashSet<ThingDef> producers;
            if (!productToAnimals.TryGetValue(product, out producers))
                return true; // unknown product, always allowed
            foreach (ThingDef producer in producers)
            {
                if (allowedAnimals.Contains(producer))
                    return true;
            }
            return false;
        }

        public static bool IsKnownAnimal(ThingDef race)
        {
            EnsureBuilt();
            return animalToProducts.ContainsKey(race);
        }
    }
}
