using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Replaces ResourceFilterExtension_Animals via XML patch on RTD_Animals.
    /// Restricts animal races and products to species registered at the settlement.
    /// Falls back to base (all animals) when no settlement context is available.
    /// </summary>
    public class ResourceFilterExtension_AnimalHusbandry : ResourceFilterExtension_Animals
    {
        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            if (resource is null || resource.settlement is null)
            {
                base.SetFilter(filter, techlevel, resource);
                return;
            }

            WorldObjectComp_AnimalRegistry comp =
                resource.settlement.GetComponent<WorldObjectComp_AnimalRegistry>();
            HashSet<ThingDef> allowed = comp?.GetAllowedAnimals();

            if (allowed is null || allowed.Count == 0)
            {
                // No animals — don't add races, and remove category-allowed products
                RemoveNonAllowedProducts(filter, new HashSet<ThingDef>());
            }
            else
            {
                // Add only allowed animal races
                foreach (PawnKindDef def in FactionCache.AllAnimalKindDefs)
                {
                    if (allowed.Contains(def.race))
                        filter.SetAllow(def.race, true);
                }

                // Remove products from non-allowed animals (categories already allowed them)
                RemoveNonAllowedProducts(filter, allowed);
            }

            // Orthogonal water/biome gate: fish (a MeatRaw-derived category, kept above as "unknown
            // products") are restricted to the species this settlement's biome offers for the water
            // type(s) its tile touches, regardless of breeding pairs. Disallow the rest; an empty
            // allowed set (landlocked / no-fish biome) removes them all. Inert without Odyssey.
            if (comp is object && ModsConfig.OdysseyActive && FCAHSettings.RestrictFishToWater
                && ThingCategoryDefOf.Fish is object)
            {
                HashSet<ThingDef> allowedFish = comp.GetAllowedFish();
                foreach (ThingDef fish in ThingCategoryDefOf.Fish.DescendantThingDefs)
                {
                    if (!allowedFish.Contains(fish))
                        filter.SetAllow(fish, false);
                }
            }
        }

        private static void RemoveNonAllowedProducts(ThingFilter filter, HashSet<ThingDef> allowed)
        {
            List<ThingDef> snapshot = filter.AllowedThingDefs.ToList();
            foreach (ThingDef def in snapshot)
            {
                if (def.race is object) continue;
                if (!AnimalProductMap.IsProductAllowed(def, allowed))
                    filter.SetAllow(def, false);
            }
        }

        public override ThingSetMaker GetThingSetMaker(out TechLevel tlevel, ResourceFC resource = null)
        {
            tlevel = TechLevel.Undefined;

            HashSet<ThingDef> allowed = null;
            if (resource is object && resource.settlement is object)
            {
                WorldObjectComp_AnimalRegistry comp =
                    resource.settlement.GetComponent<WorldObjectComp_AnimalRegistry>();
                allowed = comp?.GetAllowedAnimals();
            }

            return new ThingSetMaker_AnimalHusbandry(allowed);
        }

        public override List<Thing> GenerateSpecificThings(ThingDef thingDef, int quantity,
            QualityCategory quality = QualityCategory.Normal, ThingDef stuffDef = null,
            ResourceFC resource = null)
        {
            if (thingDef.race is object && resource is object && resource.settlement is object)
            {
                WorldObjectComp_AnimalRegistry comp =
                    resource.settlement.GetComponent<WorldObjectComp_AnimalRegistry>();
                HashSet<ThingDef> allowed = comp?.GetAllowedAnimals();
                if (allowed is object && !allowed.Contains(thingDef))
                    return new List<Thing>();
            }

            return base.GenerateSpecificThings(thingDef, quantity, quality, stuffDef, resource);
        }
    }
}
