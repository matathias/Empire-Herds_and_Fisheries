namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Caches the set of animal species shared empire-wide via Cloning Labs.
/// An animal is "cloned" if registered at any settlement that has a CloningLab building.
/// Cleared via EmpireCacheUtil callback.
/// </summary>
public static class CloningCache
{
    private static HashSet<ThingDef> cachedClonedAnimals;
    private static bool dirty = true;

    public static void Invalidate()
    {
        dirty = true;
        cachedClonedAnimals = null;
    }

    public static HashSet<ThingDef> GetClonedAnimals()
    {
        if (!dirty && cachedClonedAnimals is not null)
            return cachedClonedAnimals;

        cachedClonedAnimals = [];
        FactionFC faction = FindFC.FactionComp;
        if (faction is null) return cachedClonedAnimals;

        BuildingFCDef cloningLabDef = AnimalHusbandryDefOf.CloningLab;

        foreach (WorldSettlementFC settlement in faction.settlements)
        {
            if (settlement.BuildingsComp is null) continue;
            if (!settlement.BuildingsComp.HasBuilding(cloningLabDef)) continue;

            WorldObjectComp_AnimalRegistry comp =
                settlement.GetComponent<WorldObjectComp_AnimalRegistry>();
            if (comp is null) continue;

            cachedClonedAnimals.UnionWith(comp.RegisteredAnimals);
        }

        dirty = false;
        return cachedClonedAnimals;
    }
}
