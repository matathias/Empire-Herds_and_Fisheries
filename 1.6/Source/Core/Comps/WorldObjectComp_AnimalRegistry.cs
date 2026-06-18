using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    public class WorldObjectComp_AnimalRegistry : WorldObjectComp_SettlementPawnArrival,
        IResourceProductionModifier, ISettlementWindowOverview, ISettlementPostLoadInit
    {
        // Per-species stocking progress. A species is "available for production" only once its
        // entry IsComplete (male+female, or 2+ for genderless). Partial entries persist as in-progress.
        private List<AnimalStockEntry> animalStock = new List<AnimalStockEntry>();

        private Vector2 scrollPosition;

        // Set by ReceivePawns so the post-arrival message reports what was actually stocked,
        // not the raw pod headcount (some pod pawns can overflow back to a caravan).
        private int lastStockedCount;

        // Settlement is inherited from WorldObjectComp_SettlementPawnArrival (parent as WorldSettlementFC).

        private AnimalStockEntry FindEntry(ThingDef species)
        {
            for (int i = 0; i < animalStock.Count; i++)
            {
                if (animalStock[i].species == species)
                    return animalStock[i];
            }
            return null;
        }

        /// <summary>
        /// The species fully stocked here (complete pair / 2+ genderless). This is the seam consumed
        /// by CloningCache (so cloning shares only complete species) and the "Stocked" tab label.
        /// </summary>
        public IReadOnlyCollection<ThingDef> RegisteredAnimals
        {
            get
            {
                List<ThingDef> result = new List<ThingDef>();
                foreach (AnimalStockEntry entry in animalStock)
                {
                    if (entry.IsComplete)
                        result.Add(entry.species);
                }
                return result;
            }
        }

        // ── Animal Queries ──

        /// <summary>
        /// Returns the full set of animals allowed (producing) at this settlement:
        /// complete-stocked + basic (if enabled) + cloned (from Cloning Labs).
        /// </summary>
        public HashSet<ThingDef> GetAllowedAnimals()
        {
            HashSet<ThingDef> result = new HashSet<ThingDef>(RegisteredAnimals);

            if (FCAHSettings.BasicAnimalsEnabled)
            {
                foreach (ThingDef def in FCAHSettings.BasicAnimals)
                    result.Add(def);
            }

            result.UnionWith(CloningCache.GetClonedAnimals());
            return result;
        }

        public bool HasAnyAnimals()
        {
            foreach (AnimalStockEntry entry in animalStock)
            {
                if (entry.IsComplete) return true;
            }
            if (FCAHSettings.BasicAnimalsEnabled && FCAHSettings.BasicAnimals.Count > 0)
                return true;
            if (CloningCache.GetClonedAnimals().Count > 0) return true;
            return false;
        }

        /// <summary>A species' stocking progress (a fresh empty entry if none), for read-only UI use.</summary>
        public AnimalStockEntry GetStockSnapshot(ThingDef species)
        {
            return FindEntry(species) ?? new AnimalStockEntry(species);
        }

        /// <summary>
        /// Records one received individual of a species/gender. No-op when that combo is already
        /// stocked (the player is blocked from over-stocking). Caches are invalidated only on the
        /// incomplete->complete transition — a partial contribution changes no production output.
        /// (A complete->incomplete transition cannot happen here; nothing removes stock.)
        /// </summary>
        public void AddIndividual(ThingDef species, Gender gender)
        {
            if (species is null || species.race is null || !species.race.Animal) return;

            AnimalStockEntry entry = FindEntry(species);
            bool existed = entry is object;
            if (!existed) entry = new AnimalStockEntry(species);

            bool wasComplete = entry.IsComplete;
            if (!entry.Add(gender)) return;   // already had this combo; fresh entry (if any) discarded
            if (!existed) animalStock.Add(entry);

            if (!wasComplete && entry.IsComplete)
            {
                CloningCache.Invalidate();
                StockedAnimalCache.Invalidate();
                Settlement?.InvalidateResourceCaches();
            }
        }

        // True if the settlement still needs this species/gender combo (i.e. the add would advance).
        public bool StillNeeds(ThingDef species, Gender gender)
        {
            return (FindEntry(species) ?? new AnimalStockEntry(species)).Needs(gender);
        }

        /* -*-*-*- Transport-pod arrival (WorldObjectComp_SettlementPawnArrival) -*-*-*- */

        /* The single-pawn form of the caravan contribution rule (GetContributableSpecies /
           CaravanCanContribute): accept an animal whose species isn't already available here
           (producing / basic / cloned) and whose sex the settlement still needs. Anything rejected
           (non-animal, already-available species, sex already stocked) falls through to the base
           arrival fallback, which forms a caravan, so nothing is lost. */
        public override bool AcceptsPawn(Pawn pawn)
        {
            if (pawn?.def?.race is null || !pawn.def.race.Animal) return false;
            if (GetAllowedAnimals().Contains(pawn.def)) return false;
            return StillNeeds(pawn.def, pawn.gender);
        }

        public override string ArrivalMenuLabel => "AH_StockAnimalsAt".Translate(Settlement.Name);

        public override string ArrivalMessage(List<Pawn> pawns)
            => "AH_AnimalsStockedMsg".Translate(lastStockedCount, Settlement.Name);

        public override void ReceivePawns(List<Pawn> pawns)
        {
            if (pawns is null) return;

            lastStockedCount = 0;
            List<Pawn> overflow = null;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];

                /* Re-check live: an earlier pawn in this same pod may have just filled the sex
                   (e.g. two males of a brand-new species arriving together), so the registry no
                   longer needs this one even though AcceptsPawn passed against the pre-arrival state. */
                if (pawn?.def?.race is object && pawn.def.race.Animal
                    && !GetAllowedAnimals().Contains(pawn.def)
                    && StillNeeds(pawn.def, pawn.gender))
                {
                    StockIndividual(pawn);
                    lastStockedCount++;
                }
                else
                {
                    if (overflow is null) overflow = new List<Pawn>();
                    overflow.Add(pawn);
                }
            }

            // Anything we couldn't stock becomes a caravan at the settlement tile (mirrors
            // SettlementPawnArrivalFallback), so a player never loses an animal to over-stocking.
            if (overflow is object && overflow.Count > 0)
                CaravanMaker.MakeCaravan(overflow, Faction.OfPlayer, Settlement.Tile, true);
        }

        /* Abstracts one received individual into stock: record its species/sex, then destroy the
           pawn (the registry tracks stock abstractly, not live Pawns). Shared by the caravan
           register dialog and the transport-pod arrival path. The caller first detaches the pawn
           from any caravan it sits in; WorldPawns removal here defends the cases noted in the base
           prisoner comp (redressed / dropped pawns). */
        public void StockIndividual(Pawn pawn)
        {
            if (pawn is null) return;
            ThingDef species = pawn.def;
            Gender gender = pawn.gender;
            if (Find.WorldPawns is object && Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.RemovePawn(pawn);
            if (!pawn.Destroyed) pawn.Destroy();
            AddIndividual(species, gender);
            LogAH.MessageForce($"Stocked 1 {gender} {species.defName} at {Settlement.Name}");
        }

        // ── Water access (fish) ──

        // Geography + biome are static per tile, so the catchable-fish set is computed once and
        // cached. Not serialized: it's deterministic from the tile, so it's recomputed lazily after
        // load. The "tile not ready" case returns empty without caching, so a later call rebuilds.
        private static readonly HashSet<ThingDef> EmptyFish = new HashSet<ThingDef>();
        private HashSet<ThingDef> allowedFishCached;
        private bool allowedFishBuilt;

        /// <summary>
        /// Fish species this settlement can produce: the biome's fish for the water type(s) its
        /// tile touches (coast -> saltwater lists; river/lakeshore -> freshwater lists; union if
        /// both). Empty when landlocked, when the biome defines no fish, or without Odyssey.
        /// </summary>
        public HashSet<ThingDef> GetAllowedFish()
        {
            if (allowedFishBuilt) return allowedFishCached;
            if (!ModsConfig.OdysseyActive)
            {
                allowedFishCached = EmptyFish;
                allowedFishBuilt = true;
                return EmptyFish;
            }

            PlanetTile pt = Settlement is object ? Settlement.Tile : PlanetTile.Invalid;
            if (!pt.Valid) return EmptyFish;   // world/settlement not ready yet; recompute later
            Tile t = pt.Tile;
            if (t is null) return EmptyFish;

            BiomeFishTypes ft = t.PrimaryBiome?.fishTypes;
            HashSet<ThingDef> set = new HashSet<ThingDef>();
            if (ft is object)
            {
                bool saltwater = t.IsCoastal;                           // coast (ocean-adjacent)
                SurfaceTile st = t as SurfaceTile;
                bool freshwater = Find.World.LakeDirectionAt(pt) != Rot4.Invalid  // lakeshore
                    || (st is object && st.Rivers is object && st.Rivers.Count > 0); // river
                if (freshwater)
                {
                    AddFish(set, ft.freshwater_Common);
                    AddFish(set, ft.freshwater_Uncommon);
                }
                if (saltwater)
                {
                    AddFish(set, ft.saltwater_Common);
                    AddFish(set, ft.saltwater_Uncommon);
                }
            }

            allowedFishCached = set;
            allowedFishBuilt = true;
            return set;
        }

        private static void AddFish(HashSet<ThingDef> set, List<FishChance> chances)
        {
            if (chances is null) return;
            foreach (FishChance fc in chances)
                if (fc.fishDef is object) set.Add(fc.fishDef);
        }

        // Fish (Odyssey) are an axis orthogonal to breeding pairs: gated purely by water + biome.
        public bool CanProduceFishFromWater()
        {
            return FCAHSettings.RestrictFishToWater && GetAllowedFish().Count > 0;
        }

        // ── IResourceProductionModifier ──

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            return 0;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            if (resource.def != ResourceTypeDefOf.RTD_Animals) return 1;
            if (HasAnyAnimals()) return 1;
            if (CanProduceFishFromWater()) return 1;   // water enables fish with no breeding pairs
            return 0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            if (resource.def != ResourceTypeDefOf.RTD_Animals) return null;
            if (HasAnyAnimals()) return null;
            if (CanProduceFishFromWater()) return null;   // producing fish from water, not zero
            return "AH_NoAnimalsRegistered".Translate();
        }

        // ── Serialization ──

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref animalStock, "animalStock", LookMode.Deep);
            if (animalStock is null)
                animalStock = new List<AnimalStockEntry>();
        }

        // ── ISettlementPostLoadInit ──

        public void PostSettlementLoadInit(WorldSettlementFC settlement)
        {
            int removed = animalStock.RemoveAll(e =>
                e is null || e.species is null || e.species.race is null || !e.species.race.Animal);
            if (removed > 0)
                LogAH.Warning($"Removed {removed} invalid animal registration(s) from {settlement.Name}");
        }

        // ── Caravan Contribution ──

        /// <summary>
        /// Species in the caravan that can make stocking progress here: animals not already available
        /// (producing/basic/cloned) for which the caravan holds an individual the settlement still
        /// needs (gendered: a missing sex; genderless: any individual while the count is below 2).
        /// </summary>
        public List<ThingDef> GetContributableSpecies(Caravan caravan)
        {
            HashSet<ThingDef> available = GetAllowedAnimals();
            Dictionary<ThingDef, List<Pawn>> bySpecies = new Dictionary<ThingDef, List<Pawn>>();

            foreach (Pawn pawn in caravan.PawnsListForReading)
            {
                if (pawn.RaceProps is null || !pawn.RaceProps.Animal) continue;
                ThingDef race = pawn.def;
                if (available.Contains(race)) continue;

                if (!bySpecies.TryGetValue(race, out List<Pawn> list))
                {
                    list = new List<Pawn>();
                    bySpecies[race] = list;
                }
                list.Add(pawn);
            }

            List<ThingDef> result = new List<ThingDef>();
            foreach (KeyValuePair<ThingDef, List<Pawn>> kvp in bySpecies)
            {
                if (CaravanCanContribute(kvp.Key, kvp.Value))
                    result.Add(kvp.Key);
            }
            return result;
        }

        private bool CaravanCanContribute(ThingDef species, List<Pawn> caravanPawns)
        {
            if (species.race.hasGenders)
            {
                bool hasMale = caravanPawns.Any(p => p.gender == Gender.Male);
                bool hasFemale = caravanPawns.Any(p => p.gender == Gender.Female);
                return (hasMale && StillNeeds(species, Gender.Male))
                    || (hasFemale && StillNeeds(species, Gender.Female));
            }
            return caravanPawns.Count > 0 && StillNeeds(species, Gender.None);
        }

        // ── Caravan Gizmos & Float Menu ──

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            List<ThingDef> contributable = GetContributableSpecies(caravan);
            if (contributable.Count == 0) yield break;

            yield return new Command_Action
            {
                defaultLabel = "AH_RegisterAnimals".Translate(),
                defaultDesc = "AH_RegisterAnimalsDesc".Translate(Settlement.Name),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", true),
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_RegisterAnimals(caravan, this, contributable));
                }
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            List<ThingDef> contributable = GetContributableSpecies(caravan);
            if (contributable.Count == 0) yield break;

            yield return new FloatMenuOption(
                "AH_RegisterAnimalsAt".Translate(Settlement.Name),
                delegate
                {
                    Find.WindowStack.Add(new Dialog_RegisterAnimals(caravan, this, contributable));
                });
        }

        // ── ISettlementWindowOverview ──

        public void PreOpenWindow(WorldSettlementFC s)
        {
            scrollPosition = Vector2.zero;
        }

        public void OnTabSwitch()
        {
            scrollPosition = Vector2.zero;
        }

        public string OverviewTabName()
        {
            return "AH_LivestockTab".Translate();
        }

        public bool ShouldShowOverviewTab(WorldSettlementFC settlement) => true;

        private static readonly Color InProgressColor = new Color(0.9f, 0.8f, 0.45f);

        public void DrawOverviewTab(Rect boundingBox)
        {
            HashSet<ThingDef> available = GetAllowedAnimals();
            HashSet<ThingDef> cloned = CloningCache.GetClonedAnimals();
            HashSet<ThingDef> complete = new HashSet<ThingDef>(RegisteredAnimals);

            List<ThingDef> availableSorted = available.OrderBy(d => d.label).ToList();

            // In-progress species: stocked here but not yet complete, and not otherwise available.
            List<AnimalStockEntry> partial = new List<AnimalStockEntry>();
            foreach (AnimalStockEntry entry in animalStock)
            {
                if (entry.IsComplete) continue;
                if (available.Contains(entry.species)) continue;
                partial.Add(entry);
            }
            partial = partial.OrderBy(e => e.species.label).ToList();

            float headerHeight = 30f;
            Rect headerRect = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, headerHeight);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(headerRect, "AH_StockedHeader".Translate());
            Text.Font = GameFont.Small;

            float offsetY = boundingBox.y + headerHeight;

            // Fish status (Odyssey + setting): which species this tile's biome + water type offers.
            if (ModsConfig.OdysseyActive && FCAHSettings.RestrictFishToWater)
            {
                float fishLineHeight = 24f;
                Rect fishRect = new Rect(boundingBox.x, offsetY, boundingBox.width, fishLineHeight);
                HashSet<ThingDef> fish = GetAllowedFish();
                Color prev = GUI.color;
                string label;
                if (fish.Count > 0)
                {
                    string list = string.Join(", ", fish.Select(f => f.LabelCap.ToString()));
                    label = "AH_FishAvailable".Translate(list);
                    TooltipHandler.TipRegion(fishRect, label);
                }
                else
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    label = "AH_FishUnavailable".Translate();
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(fishRect, Text.ClampTextWithEllipsis(fishRect, label));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = prev;
                offsetY += fishLineHeight;
            }

            Rect listBox = new Rect(boundingBox.x, offsetY,
                boundingBox.width, boundingBox.height - (offsetY - boundingBox.y));

            float rowHeight = 32f;
            float sectionHeaderHeight = 28f;
            float viewHeight = availableSorted.Count * rowHeight
                + (partial.Count > 0 ? sectionHeaderHeight + partial.Count * rowHeight : 0f);
            Rect viewRect = ScrollUtil.BeginScrollView(listBox, ref scrollPosition, viewHeight);

            float y = 0;
            foreach (ThingDef race in availableSorted)
            {
                string source;
                if (complete.Contains(race))
                    source = "AH_SourceRegistered".Translate();
                else if (cloned.Contains(race))
                    source = "AH_SourceCloned".Translate();
                else
                    source = "AH_SourceBasic".Translate();

                DrawAnimalRow(viewRect.width, y, rowHeight, race, source, Color.white);
                y += rowHeight;
            }

            if (partial.Count > 0)
            {
                Rect sectionRect = new Rect(0, y, viewRect.width, sectionHeaderHeight);
                Text.Anchor = TextAnchor.LowerLeft;
                Widgets.Label(sectionRect, "AH_InProgressHeader".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                y += sectionHeaderHeight;

                foreach (AnimalStockEntry e in partial)
                {
                    ThingDef race = e.species;
                    string progress;
                    if (race.race.hasGenders)
                    {
                        if (e.hasMale && !e.hasFemale) progress = "AH_AwaitingFemale".Translate();
                        else if (!e.hasMale && e.hasFemale) progress = "AH_AwaitingMale".Translate();
                        else progress = "AH_AwaitingPair".Translate();
                    }
                    else
                    {
                        progress = "AH_GenderlessProgress".Translate(e.genderless, 2);
                    }

                    DrawAnimalRow(viewRect.width, y, rowHeight, race, progress, InProgressColor);
                    y += rowHeight;
                }
            }

            if (availableSorted.Count == 0 && partial.Count == 0)
            {
                Widgets.Label(new Rect(8, 0, viewRect.width - 16, 30),
                    "AH_NoAnimals".Translate());
            }

            ScrollUtil.EndScrollView();
        }

        private static void DrawAnimalRow(float width, float y, float rowHeight, ThingDef race,
            string rightText, Color rightColor)
        {
            Rect row = new Rect(0, y, width, rowHeight);

            Rect iconRect = new Rect(row.x + 4, row.y + 2, 28, 28);
            Widgets.ThingIcon(iconRect, race);

            Rect labelRect = new Rect(iconRect.xMax + 8, row.y, row.width - 160, rowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, race.LabelCap);

            Rect rightRect = new Rect(row.xMax - 144, row.y, 140, rowHeight);
            Text.Anchor = TextAnchor.MiddleRight;
            Color prev = GUI.color;
            GUI.color = rightColor;
            Widgets.Label(rightRect, rightText);
            GUI.color = prev;
            Text.Anchor = TextAnchor.UpperLeft;

            HashSet<ThingDef> products = AnimalProductMap.GetProducts(race);
            if (products is object && products.Count > 0)
            {
                string tip = string.Join(", ", products.Select(p => p.LabelCap.ToString()));
                TooltipHandler.TipRegion(row, "AH_ProductsTooltip".Translate(race.LabelCap, tip));
            }

            if (Mouse.IsOver(row))
                Widgets.DrawHighlight(row);
        }

        public void PostCloseWindow()
        {
        }
    }
}
