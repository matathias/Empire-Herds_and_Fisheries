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
                foreach (ThingDef def in FCAHSettings.BasicAnimals)
                    result.Add(def);
            }

            result.UnionWith(CloningCache.GetClonedAnimals());
            return result;
        }

        public bool HasAnyAnimals()
        {
            if (registeredAnimals.Count > 0) return true;
            if (FCAHSettings.BasicAnimalsEnabled && FCAHSettings.BasicAnimals.Count > 0)
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

        // ── Breeding Pairs ──

        /// <summary>
        /// Determines whether a list of same-species caravan animals contains a valid breeding pair,
        /// and if so which two pawns to consume. Gendered species (RaceProps.hasGenders) require one
        /// male and one female; genderless species require any two individuals. Returns false (with
        /// null outputs) when no valid pair exists.
        /// </summary>
        public static bool TryGetBreedingPair(List<Pawn> sameSpecies, out Pawn first, out Pawn second)
        {
            first = null;
            second = null;
            if (sameSpecies is null || sameSpecies.Count < 2) return false;

            bool hasGenders = sameSpecies[0].RaceProps?.hasGenders ?? false;
            if (hasGenders)
            {
                Pawn male = sameSpecies.FirstOrDefault(p => p.gender == Gender.Male);
                Pawn female = sameSpecies.FirstOrDefault(p => p.gender == Gender.Female);
                if (male is object && female is object)
                {
                    first = male;
                    second = female;
                    return true;
                }
                return false;
            }

            // Genderless species: any two individuals form a pair.
            first = sameSpecies[0];
            second = sameSpecies[1];
            return true;
        }

        // ── Caravan Gizmos & Float Menu ──

        private List<ThingDef> GetRegistrableSpecies(Caravan caravan)
        {
            HashSet<ThingDef> allowed = GetAllowedAnimals();
            Dictionary<ThingDef, List<Pawn>> bySpecies = new Dictionary<ThingDef, List<Pawn>>();

            foreach (Pawn pawn in caravan.PawnsListForReading)
            {
                if (pawn.RaceProps is null || !pawn.RaceProps.Animal) continue;
                ThingDef race = pawn.def;
                if (allowed.Contains(race)) continue;

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
                if (TryGetBreedingPair(kvp.Value, out Pawn first, out Pawn second))
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

            float headerHeight = 30f;
            Rect headerRect = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, headerHeight);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(headerRect, "AH_StockedHeader".Translate());
            Text.Font = GameFont.Small;
            Rect listBox = new Rect(boundingBox.x, boundingBox.y + headerHeight,
                boundingBox.width, boundingBox.height - headerHeight);

            float rowHeight = 32f;
            float viewHeight = sorted.Count * rowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(listBox, ref scrollPosition, viewHeight);

            float y = 0;
            foreach (ThingDef race in sorted)
            {
                Rect row = new Rect(0, y, viewRect.width, rowHeight);

                Rect iconRect = new Rect(row.x + 4, row.y + 2, 28, 28);
                Widgets.ThingIcon(iconRect, race);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, row.width - 150, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
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
                Widgets.Label(new Rect(8, 0, viewRect.width - 16, 30),
                    "AH_NoAnimals".Translate());
            }

            ScrollUtil.EndScrollView();
        }

        public void PostCloseWindow()
        {
        }
    }
}
