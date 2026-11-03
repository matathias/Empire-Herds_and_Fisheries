using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Helpers for the DESTRUCTIVE Herds &amp; Fisheries tests. Builds on the base
    /// <see cref="DestructiveTestUtil"/> (transient settlement create/remove, invariant battery) with
    /// submod-specific fixtures: a transient settlement that carries the animal registry, animal pawn
    /// generation, a Cloning-Lab build, and caravan cleanup. All mutate live world state.
    /// </summary>
    public static class AHDestructiveTestUtil
    {
        /// <summary>
        /// Creates a transient player settlement and returns it with its
        /// <see cref="WorldObjectComp_AnimalRegistry"/>. Returns null (and removes the settlement) when
        /// the comp is absent, so the caller can skip.
        /// </summary>
        public static WorldSettlementFC CreateSettlementWithRegistry(out WorldObjectComp_AnimalRegistry comp)
        {
            comp = null;
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) return null;

            comp = s.GetComponent<WorldObjectComp_AnimalRegistry>();
            if (comp is null)
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
                return null;
            }
            return s;
        }

        /// <summary>Generates an unspawned animal pawn of the given kind and gender.</summary>
        public static Pawn MakeAnimalPawn(PawnKindDef kind, Gender gender)
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                kind, Faction.OfPlayer, fixedGender: gender,
                allowAddictions: false, worldPawnFactionDoesntMatter: true);
            return PawnGenerator.GeneratePawn(request);
        }

        /// <summary>
        /// Constructs a Cloning Lab in the first empty building slot (firing its OnConstruct comp hook).
        /// Returns the slot index, or -1 if there is no empty slot (caller skips).
        /// </summary>
        public static int AttachCloningLab(WorldSettlementFC settlement)
        {
            var buildings = settlement.BuildingsComp;
            if (buildings is null) return -1;
            int slots = buildings.NumBuildingSlots;
            for (int i = 0; i < slots; i++)
            {
                if (buildings.BuildingSlotIsEmpty(i))
                {
                    buildings.ConstructBuilding(AnimalHusbandryDefOf.CloningLab, i);
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Destroys any player caravans sitting on the given tile (overflow-arrival cleanup).</summary>
        public static void RemoveCaravansAt(PlanetTile tile)
        {
            if (Find.WorldObjects is null) return;
            List<Caravan> here = Find.WorldObjects.Caravans.Where(c => c.Tile == tile).ToList();
            foreach (Caravan c in here)
            {
                try { c.Destroy(); }
                catch (Exception ex) { LogAH.Warning($"caravan cleanup threw (ignored): {ex}"); }
            }
        }
    }
}
