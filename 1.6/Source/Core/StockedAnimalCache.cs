using System.Collections.Generic;
using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Caches the empire-wide set of animal species available to the military unit designer:
    /// the union of every settlement's <see cref="WorldObjectComp_AnimalRegistry.GetAllowedAnimals"/>
    /// (bred breeding pairs + Basic Animals (if enabled) + Cloning-Lab-shared species). Backs
    /// <see cref="MercAnimalStockFilter"/>, whose per-kind check runs each picker redraw, so the
    /// O(1) Contains lookup matters. Basic + cloned are seeded directly so the set is correct even
    /// with zero settlements. Cleared via the EmpireCacheUtil callback; invalidated on stock changes
    /// and Basic-Animal setting changes.
    /// </summary>
    public static class StockedAnimalCache
    {
        private static HashSet<ThingDef> cached;
        private static bool dirty = true;

        public static void Invalidate()
        {
            dirty = true;
            cached = null;
        }

        public static void Clear()
        {
            cached = null;
            dirty = true;
        }

        public static HashSet<ThingDef> Get()
        {
            if (!dirty && cached is object)
                return cached;

            HashSet<ThingDef> result = new HashSet<ThingDef>();

            if (FCAHSettings.BasicAnimalsEnabled)
            {
                foreach (ThingDef def in FCAHSettings.BasicAnimals)
                    if (def is object) result.Add(def);
            }

            result.UnionWith(CloningCache.GetClonedAnimals());

            FactionFC faction = FindFC.FactionComp;
            if (faction is object)
            {
                foreach (WorldSettlementFC settlement in faction.settlements)
                {
                    WorldObjectComp_AnimalRegistry comp =
                        settlement.GetComponent<WorldObjectComp_AnimalRegistry>();
                    if (comp is null) continue;
                    result.UnionWith(comp.RegisteredAnimals);
                }
            }

            cached = result;
            dirty = false;
            return cached;
        }
    }
}
