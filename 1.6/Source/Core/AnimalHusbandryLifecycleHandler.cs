using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Lifecycle handler that invalidates the CloningCache when a Cloning Lab
    /// is built or demolished at any settlement.
    /// </summary>
    public class AnimalHusbandryLifecycleHandler : ISettlementListener
    {
        private static BuildingFCDef cachedCloningLabDef;

        private static BuildingFCDef CloningLabDef
        {
            get
            {
                if (cachedCloningLabDef is null)
                    cachedCloningLabDef = DefDatabase<BuildingFCDef>.GetNamedSilentFail("CloningLab");
                return cachedCloningLabDef;
            }
        }

        public void OnSettlementCreated(WorldSettlementFC settlement) { }
        public void OnSettlementRemoved(WorldSettlementFC settlement) { }
        public void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel) { }
        public void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }

        public void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            if (building == CloningLabDef)
            {
                CloningCache.Invalidate();
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
            }
        }

        public void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            if (building == CloningLabDef)
            {
                CloningCache.Invalidate();
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
            }
        }
    }
}
