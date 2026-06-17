using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    public class WorldObjectComp_AnimalRegistry : WorldObjectComp,
        IResourceProductionModifier, ISettlementWindowOverview, ISettlementPostLoadInit
    {
        private HashSet<ThingDef> registeredAnimals = new HashSet<ThingDef>();

        private WorldSettlementFC _settlement;
        private Vector2 scrollPosition;

        public WorldSettlementFC Settlement
        {
            get
            {
                if (_settlement is null)
                    _settlement = parent as WorldSettlementFC;
                return _settlement;
            }
        }

        public IReadOnlyCollection<ThingDef> RegisteredAnimals
        {
            get { return registeredAnimals; }
        }

        // ── Animal Queries ──

        /// <summary>
        /// Returns the full set of animals allowed at this settlement:
        /// registered + basic (if enabled) + cloned (from Cloning Labs).
        /// </summary>
        public HashSet<ThingDef> GetAllowedAnimals()
        {
            HashSet<ThingDef> result = new HashSet<ThingDef>(registeredAnimals);

            if (FCAHSettings.BasicAnimalsEnabled)
            {
                foreach (ThingDef def in FCAHSettings.ResolvedBasicAnimals)
                    result.Add(def);
            }

            result.UnionWith(CloningCache.GetClonedAnimals());
            return result;
        }

        public bool HasAnyAnimals()
        {
            if (registeredAnimals.Count > 0) return true;
            if (FCAHSettings.BasicAnimalsEnabled && FCAHSettings.ResolvedBasicAnimals.Count > 0)
                return true;
            if (CloningCache.GetClonedAnimals().Count > 0) return true;
            return false;
        }

        public void RegisterAnimal(ThingDef race)
        {
            if (registeredAnimals.Add(race))
            {
                CloningCache.Invalidate();
                Settlement?.InvalidateResourceCaches();
            }
        }

        // ── IResourceProductionModifier ──

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            return 0;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            if (resource.def != ResourceTypeDefOf.RTD_Animals) return 1;
            return HasAnyAnimals() ? 1 : 0;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            if (resource.def != ResourceTypeDefOf.RTD_Animals) return null;
            if (!HasAnyAnimals())
                return "AH_NoAnimalsRegistered".Translate();
            return null;
        }

        // ── Serialization ──

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref registeredAnimals, "registeredAnimals", LookMode.Def);
            if (registeredAnimals is null)
                registeredAnimals = new HashSet<ThingDef>();
        }

        // ── ISettlementPostLoadInit ──

        public void PostSettlementLoadInit(WorldSettlementFC settlement)
        {
            int removed = registeredAnimals.RemoveWhere(def => def is null || def.race is null || !def.race.Animal);
            if (removed > 0)
                LogAH.Warning($"Removed {removed} invalid animal registration(s) from {settlement.Name}");
        }

        // ── Caravan Gizmos & Float Menu ──

        private List<ThingDef> GetRegistrableSpecies(Caravan caravan)
        {
            HashSet<ThingDef> allowed = GetAllowedAnimals();
            Dictionary<ThingDef, int> speciesCounts = new Dictionary<ThingDef, int>();

            foreach (Pawn pawn in caravan.PawnsListForReading)
            {
                if (pawn.RaceProps is null || !pawn.RaceProps.Animal) continue;
                ThingDef race = pawn.def;
                if (allowed.Contains(race)) continue;

                int count;
                speciesCounts.TryGetValue(race, out count);
                speciesCounts[race] = count + 1;
            }

            List<ThingDef> result = new List<ThingDef>();
            foreach (KeyValuePair<ThingDef, int> kvp in speciesCounts)
            {
                if (kvp.Value >= 2)
                    result.Add(kvp.Key);
            }
            return result;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            List<ThingDef> registrable = GetRegistrableSpecies(caravan);
            if (registrable.Count == 0) yield break;

            yield return new Command_Action
            {
                defaultLabel = "AH_RegisterAnimals".Translate(),
                defaultDesc = "AH_RegisterAnimalsDesc".Translate(Settlement.Name),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", true),
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_RegisterAnimals(caravan, this, registrable));
                }
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            List<ThingDef> registrable = GetRegistrableSpecies(caravan);
            if (registrable.Count == 0) yield break;

            yield return new FloatMenuOption(
                "AH_RegisterAnimalsAt".Translate(Settlement.Name),
                delegate
                {
                    Find.WindowStack.Add(new Dialog_RegisterAnimals(caravan, this, registrable));
                });
        }

        // ── ISettlementWindowOverview ──

        public void PreOpenWindow(WorldSettlementFC s)
        {
            _settlement = s;
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

        public void DrawOverviewTab(Rect boundingBox)
        {
            HashSet<ThingDef> allowed = GetAllowedAnimals();
            HashSet<ThingDef> cloned = CloningCache.GetClonedAnimals();

            List<ThingDef> sorted = allowed.OrderBy(d => d.label).ToList();

            float rowHeight = 32f;
            float viewHeight = sorted.Count * rowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(boundingBox, ref scrollPosition, viewHeight);

            float y = 0;
            foreach (ThingDef race in sorted)
            {
                Rect row = new Rect(0, y, viewRect.width, rowHeight);

                Rect iconRect = new Rect(row.x + 4, row.y + 2, 28, 28);
                Widgets.ThingIcon(iconRect, race);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, row.width - 150, rowHeight);
                Widgets.Label(labelRect, race.LabelCap);

                string source;
                if (registeredAnimals.Contains(race))
                    source = "AH_SourceRegistered".Translate();
                else if (cloned.Contains(race))
                    source = "AH_SourceCloned".Translate();
                else
                    source = "AH_SourceBasic".Translate();

                Rect sourceRect = new Rect(row.xMax - 120, row.y, 116, rowHeight);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(sourceRect, source);
                Text.Anchor = TextAnchor.UpperLeft;

                HashSet<ThingDef> products = AnimalProductMap.GetProducts(race);
                if (products is object && products.Count > 0)
                {
                    string tip = string.Join(", ", products.Select(p => p.LabelCap.ToString()));
                    TooltipHandler.TipRegion(row, "AH_ProductsTooltip".Translate(race.LabelCap, tip));
                }

                if (Mouse.IsOver(row))
                    Widgets.DrawHighlight(row);

                y += rowHeight;
            }

            if (sorted.Count == 0)
            {
                Widgets.Label(new Rect(8, 0, boundingBox.width - 16, 30),
                    "AH_NoAnimals".Translate());
            }

            ScrollUtil.EndScrollView();
        }

        public void PostCloseWindow()
        {
        }
    }
}
