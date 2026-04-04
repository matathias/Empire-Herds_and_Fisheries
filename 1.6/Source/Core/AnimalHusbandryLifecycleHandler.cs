using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Lifecycle handler that invalidates the CloningCache when a Cloning Lab
    /// is built or demolished at any settlement.
    /// </summary>
    public class AnimalHusbandryLifecycleHandler : LifecycleParticipantBase
    {
        private static BuildingFCDef cachedCloningLabDef;

        private static BuildingFCDef CloningLabDef
        {
            get
            {
                if (cachedCloningLabDef == null)
                    cachedCloningLabDef = DefDatabase<BuildingFCDef>.GetNamedSilentFail("CloningLab");
                return cachedCloningLabDef;
            }
        }

        public override void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            if (building == CloningLabDef)
            {
                CloningCache.Invalidate();
                FactionCache.FactionComp?.InvalidateAllSettlementStatCaches();
            }
        }

        public override void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            if (building == CloningLabDef)
            {
                CloningCache.Invalidate();
                FactionCache.FactionComp?.InvalidateAllSettlementStatCaches();
            }
        }
    }
}
