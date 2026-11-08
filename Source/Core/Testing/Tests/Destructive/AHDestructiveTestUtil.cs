namespace FactionColonies.AnimalHusbandry;

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
    /// <see cref="WorldObjectComp_AnimalRegistry"/>. With <paramref name="tileMatch"/>, only tiles it
    /// accepts are used. Fails the test if no settlement could be created or the comp (which this
    /// mod's patches attach to every settlement) is missing.
    /// </summary>
    public static WorldSettlementFC CreateSettlementWithRegistry(out WorldObjectComp_AnimalRegistry comp,
        Predicate<PlanetTile> tileMatch = null)
    {
        WorldSettlementFC s = tileMatch is null
            ? DestructiveTestUtil.RequireTransientSettlement()
            : TestAssert.NotNull(CreateSettlementOnMatchingTile(tileMatch),
                "Could not find a valid settlement tile matching the test's terrain requirement");

        comp = s.GetComponent<WorldObjectComp_AnimalRegistry>();
        if (comp is null)
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
            TestAssert.Fail("A new settlement does not carry WorldObjectComp_AnimalRegistry");
        }
        return s;
    }

    // Same recipe as DestructiveTestUtil.CreateTransientSettlement, with an extra tile filter.
    private static WorldSettlementFC CreateSettlementOnMatchingTile(Predicate<PlanetTile> tileMatch, int maxAttempts = 2000)
    {
        WorldSettlementDef def = WorldSettlementDefOf.WorldSettlementDef_Surface;
        DestructiveTestUtil.EnsureEmpireFaction();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
            if (!tile.Valid || !tileMatch(tile)) continue;
            if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def)) continue;
            if (FindFC.FactionComp.CheckSettlementCaravansList(tile)) continue;

            return ColonyUtil.CreatePlayerColonySettlement(tile, def);
        }
        return null;
    }

    /// <summary>
    /// True when the tile touches no water: not coastal, not on a lakeshore and no river. Such a
    /// tile gives a settlement no fish while fishing is restricted to water access.
    /// </summary>
    public static bool IsLandlocked(PlanetTile tile)
    {
        Tile t = tile.Tile;
        if (t is null || t.IsCoastal) return false;
        if (Find.World.LakeDirectionAt(tile) != Rot4.Invalid) return false;
        return t is not SurfaceTile st || st.Rivers is null || st.Rivers.Count == 0;
    }

    /// <summary>Generates an unspawned animal pawn of the given kind and gender.</summary>
    public static Pawn MakeAnimalPawn(PawnKindDef kind, Gender gender)
    {
        PawnGenerationRequest request = new(
            kind, Faction.OfPlayer, fixedGender: gender,
            allowAddictions: false, worldPawnFactionDoesntMatter: true);
        return PawnGenerator.GeneratePawn(request);
    }

    /// <summary>
    /// Constructs a Cloning Lab (firing its OnConstruct comp hook) in the first empty building slot
    /// of a settlement the test owns. If every slot is taken, the last one is cleared first. Returns
    /// the slot index; fails the test if the settlement has no building slots at all.
    /// </summary>
    public static int AttachCloningLab(WorldSettlementFC settlement)
    {
        WorldObjectComp_SettlementBuildings buildings = TestAssert.NotNull(settlement.BuildingsComp,
            $"Settlement '{settlement.Name}' has no buildings comp");
        int slots = buildings.NumBuildingSlots;
        TestAssert.GreaterThan(slots, 0, $"Settlement '{settlement.Name}' has no building slots");

        int slot = Enumerable.Range(0, slots).FirstOrDefault(buildings.BuildingSlotIsEmpty, slots - 1);
        buildings.ConstructBuilding(AnimalHusbandryDefOf.CloningLab, slot);
        return slot;
    }

    /// <summary>
    /// Deconstructs every Cloning Lab in the empire so no species is shared empire-wide, and returns
    /// the (settlement, slot) pairs for <see cref="RestoreCloningLabs"/>.
    /// </summary>
    public static List<(WorldSettlementFC settlement, int slot)> SuspendCloningLabs()
    {
        List<(WorldSettlementFC, int)> removed = [];
        foreach (WorldSettlementFC settlement in DestructiveTestUtil.RequireFaction().settlements)
        {
            WorldObjectComp_SettlementBuildings buildings = settlement?.BuildingsComp;
            if (buildings is null) continue;
            for (int i = 0; i < buildings.NumBuildingSlots; i++)
            {
                if (buildings.GetBuildingInSlot(i) != AnimalHusbandryDefOf.CloningLab) continue;
                buildings.DeconstructBuilding(i);
                removed.Add((settlement, i));
            }
        }
        CloningCache.Invalidate();
        return removed;
    }

    /// <summary>Rebuilds the Cloning Labs removed by <see cref="SuspendCloningLabs"/>. Swallows+logs
    /// errors so cleanup never masks the test result.</summary>
    public static void RestoreCloningLabs(List<(WorldSettlementFC settlement, int slot)> removed)
    {
        if (removed is null) return;
        foreach ((WorldSettlementFC settlement, int slot) in removed)
        {
            try
            {
                if (settlement is not null && !settlement.Destroyed)
                    settlement.BuildingsComp?.ConstructBuilding(AnimalHusbandryDefOf.CloningLab, slot);
            }
            catch (Exception ex) { LogAH.Warning($"Cloning Lab restore threw (ignored): {ex}"); }
        }
        CloningCache.Invalidate();
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
