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
        // Products already warned about (no associated animal), so the block is logged once each.
        private static readonly HashSet<ThingDef> warnedUnmappedProducts = new HashSet<ThingDef>();
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
            warnedUnmappedProducts.Clear();
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

                // Insect jelly has no producing comp (vanilla Hives spawn it), so associate it with any
                // insect race so insect-stocked settlements produce it and non-insect settlements don't.
                if (race.race is object && race.race.Insect)
                    AddMapping(race, ThingDefOf.InsectJelly, products);
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
        /// True if any animal producing this product is in the allowed set. A product with no
        /// associated animal is blocked (and logged once) rather than let through — the exception
        /// being fish, which are gated orthogonally by water access in the filter's water gate.
        /// </summary>
        public static bool IsProductAllowed(ThingDef product, HashSet<ThingDef> allowedAnimals)
        {
            EnsureBuilt();
            HashSet<ThingDef> producers;
            if (!productToAnimals.TryGetValue(product, out producers))
            {
                // Fish (Odyssey) are gated orthogonally by water access in ResourceFilterExtension_
                // AnimalHusbandry's water gate, not by breeding pairs. Leave that decision to the
                // filter — don't block them here on the basis of having no producing animal.
                if (IsWaterGatedProduct(product)) return true;

                // Reversed default: an animal-category product with no associated animal is BLOCKED,
                // not let through (was the leak that put insect jelly into every settlement's animal
                // production).
                WarnUnmappedProductOnce(product);
                return false;
            }
            foreach (ThingDef producer in producers)
            {
                if (allowedAnimals.Contains(producer))
                    return true;
            }
            return false;
        }

        private static bool IsWaterGatedProduct(ThingDef def)
        {
            return ModsConfig.OdysseyActive
                && ThingCategoryDefOf.Fish is object
                && def.IsWithinCategory(ThingCategoryDefOf.Fish);
        }

        private static void WarnUnmappedProductOnce(ThingDef product)
        {
            if (warnedUnmappedProducts.Add(product))
                LogAH.Warning($"Animal-category product '{product.defName}' has no associated animal; "
                    + "blocking it from animal production.");
        }

        public static bool IsKnownAnimal(ThingDef race)
        {
            EnsureBuilt();
            return animalToProducts.ContainsKey(race);
        }
    }
}
