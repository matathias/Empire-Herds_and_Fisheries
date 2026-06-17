using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Attached to the CloningLab building via BuildingFCExtension. Invalidates the empire-wide
    /// CloningCache when a Cloning Lab is built or demolished, replacing AnimalHusbandryLifecycleHandler.
    /// </summary>
    public class SettlementBuildingComp_CloningLab : SettlementBuildingComp
    {
        public override void OnConstruct(int buildingSlot)
        {
            base.OnConstruct(buildingSlot);
            CloningCache.Invalidate();
            FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
        }

        public override void OnDeconstruct(int buildingSlot)
        {
            CloningCache.Invalidate();
            FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
            base.OnDeconstruct(buildingSlot);
        }
    }
}
