using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Gates the base mod's military unit-designer animal &amp; mount pickers to animals at least one
    /// Empire settlement has stocked (the empire-wide allowed set in <see cref="StockedAnimalCache"/>).
    /// Registered with the base mod via <see cref="EmpireRegistry"/>; honors the
    /// <see cref="FCAHSettings.RestrictMercAnimalsToStocked"/> toggle. Bridges the picker's PawnKindDef
    /// to the registry's race ThingDef via <see cref="PawnKindDef.race"/>.
    /// </summary>
    public class MercAnimalStockFilter : IAnimalPickerFilter
    {
        public static readonly MercAnimalStockFilter Instance = new MercAnimalStockFilter();

        public bool IsAnimalAllowed(PawnKindDef animal)
        {
            if (!FCAHSettings.RestrictMercAnimalsToStocked) return true;   // toggle off => vanilla list
            if (animal?.race is null) return true;
            return StockedAnimalCache.Get().Contains(animal.race);
        }
    }
}
