using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Drops a removed settlement's species from the empire-wide stocked-animal cache. StockedAnimalCache
    /// aggregates every settlement's RegisteredAnimals (consumed by MercAnimalStockFilter), so without this
    /// the merc unit-designer keeps offering a destroyed settlement's species until the next reload. The
    /// cloning half self-heals separately (buildings deconstruct first; SettlementBuildingComp_CloningLab
    /// .OnDeconstruct invalidates CloningCache) - CloningCache is re-invalidated here only defensively.
    /// </summary>
    public class AnimalHusbandryLifecycleHandler : ISettlementListener
    {
        public void OnSettlementRemoved(WorldSettlementFC settlement)
        {
            StockedAnimalCache.Invalidate();
            CloningCache.Invalidate();
        }

        public void OnSettlementCreated(WorldSettlementFC settlement) { }
        public void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel) { }
        public void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }
        public void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot) { }
        public void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot) { }
    }
}
