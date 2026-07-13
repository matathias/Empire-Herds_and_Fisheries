using System.Collections.Generic;
using FactionColonies;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    public class FCAHSettings : ModSettings
    {
        private static bool printDebug = false;
        public static bool PrintDebug => printDebug;

        // Basic animals
        public static bool BasicAnimalsEnabled = true;

        public static List<ThingDef> BasicAnimals = new List<ThingDef>();

        // Fish (Odyssey) are gated to water-adjacent settlements, orthogonal to the breeding-pair rule.
        public static bool RestrictFishToWater = true;

        // Gate the military unit designer's animal/mount pickers to animals at least one settlement
        // has stocked (the empire-wide allowed set). Off => the vanilla full animal list.
        public static bool RestrictMercAnimalsToStocked = true;

        // Persisted form only: mod settings load before the def database exists, so we save defNames
        // (LookMode.Value) and resolve them to ThingDefs in InitializeBasicAnimals() from
        // [StaticConstructorOnStartup]. null => never saved (use DefOf defaults); empty => user cleared all.
        private static List<string> savedBasicAnimalDefNames;

        public static void InitializeBasicAnimals()
        {
            BasicAnimals = new List<ThingDef>();
            if (savedBasicAnimalDefNames is null)
            {
                BasicAnimals.Add(AnimalHusbandryDefOf.Chicken);
                BasicAnimals.Add(AnimalHusbandryDefOf.Cow);
                BasicAnimals.Add(AnimalHusbandryDefOf.Pig);
                BasicAnimals.Add(AnimalHusbandryDefOf.Sheep);
                BasicAnimals.Add(AnimalHusbandryDefOf.Goat);
                return;
            }
            foreach (string defName in savedBasicAnimalDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def is object)
                    BasicAnimals.Add(def);
                else
                    LogAH.Warning($"Basic animal def '{defName}' not found");
            }
        }

        public static void ResetToDefaults()
        {
            printDebug = false;
            BasicAnimalsEnabled = true;
            RestrictFishToWater = true;
            RestrictMercAnimalsToStocked = true;
            savedBasicAnimalDefNames = null;   // "never saved" => InitializeBasicAnimals re-adds the defaults
            InitializeBasicAnimals();
            StockedAnimalCache.Invalidate();   // basic-animal list changed
            FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref BasicAnimalsEnabled, "basicAnimalsEnabled", true);
            Scribe_Values.Look(ref RestrictFishToWater, "restrictFishToWater", true);
            Scribe_Values.Look(ref RestrictMercAnimalsToStocked, "restrictMercAnimalsToStocked", true);

            // Snapshot the working ThingDef list back to defNames on save.
            // On load, InitializeBasicAnimals() resolves these at startup.
            if (Scribe.mode == LoadSaveMode.Saving && BasicAnimals is object)
            {
                savedBasicAnimalDefNames = new List<string>();
                foreach (ThingDef d in BasicAnimals)
                    savedBasicAnimalDefNames.Add(d.defName);
            }
            Scribe_Collections.Look(ref savedBasicAnimalDefNames, "basicAnimalDefNames", LookMode.Value);
        }

        public void DoWindowContents(Rect inRect)
        {
            // Pin "Open Patch Notes" to the bottom as a footer, separated from everything above
            // by a GapLine-style line.
            float footerButtonHeight = 32f;
            Rect patchNotesRect = new Rect(inRect.x, inRect.yMax - footerButtonHeight, inRect.width, footerButtonHeight);
            if (Widgets.ButtonText(patchNotesRect, "AH_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empire.animalhusbandry", "AH_PatchTitle".Translate()));

            float lineY = patchNotesRect.y - 12f;
            Color prevColor = GUI.color;
            GUI.color = prevColor * new Color(1f, 1f, 1f, 0.4f);
            Widgets.DrawLineHorizontal(inRect.x, lineY, inRect.width);
            GUI.color = prevColor;

            Rect mainRect = new Rect(inRect.x, inRect.y, inRect.width, lineY - inRect.y);

            Listing_Standard ls = new Listing_Standard();
            ls.Begin(mainRect);

            ls.CheckboxLabeled("AH_DebugLogging".Translate(), ref printDebug);
            ls.GapLine();

            bool prevBasic = BasicAnimalsEnabled;
            ls.CheckboxLabeled(
                "AH_BasicAnimalsEnabled".Translate(),
                ref BasicAnimalsEnabled,
                "AH_BasicAnimalsEnabledDesc".Translate());

            if (prevBasic != BasicAnimalsEnabled)
            {
                StockedAnimalCache.Invalidate();
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
            }

            if (BasicAnimalsEnabled)
            {
                ls.Gap(8f);
                ls.Label("AH_BasicAnimalsList".Translate());

                List<ThingDef> toRemove = new List<ThingDef>();
                foreach (ThingDef animal in BasicAnimals)
                {
                    Rect row = ls.GetRect(24f);
                    Widgets.Label(new Rect(row.x + 12, row.y, row.width - 40, row.height), animal.LabelCap);
                    if (Widgets.ButtonImage(new Rect(row.xMax - 24, row.y, 20, 20), TexButton.Delete))
                        toRemove.Add(animal);
                }
                foreach (ThingDef animal in toRemove)
                {
                    BasicAnimals.Remove(animal);
                    StockedAnimalCache.Invalidate();
                    FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
                }

                ls.Gap(4f);
                if (ls.ButtonText("AH_AddBasicAnimal".Translate()))
                    Find.WindowStack.Add(new Dialog_PickBasicAnimal());
            }

            ls.GapLine();

            bool prevFish = RestrictFishToWater;
            ls.CheckboxLabeled(
                "AH_RestrictFishToWater".Translate(),
                ref RestrictFishToWater,
                "AH_RestrictFishToWaterDesc".Translate());

            if (prevFish != RestrictFishToWater)
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();

            ls.GapLine();

            bool prevMercAnimals = RestrictMercAnimalsToStocked;
            ls.CheckboxLabeled(
                "AH_RestrictMercAnimalsToStocked".Translate(),
                ref RestrictMercAnimalsToStocked,
                "AH_RestrictMercAnimalsToStockedDesc".Translate());

            // The set doesn't change, but the gate flips, so the picker should reflect it immediately.
            if (prevMercAnimals != RestrictMercAnimalsToStocked)
                StockedAnimalCache.Invalidate();

            ls.Gap(12f);
            if (ls.ButtonText("AH_ResetSettings".Translate()))
                ResetToDefaults();

            ls.End();
        }
    }

    [StaticConstructorOnStartup]
    public static class AnimalHusbandryStartup
    {
        // Invalidates StockedAnimalCache when a settlement is removed (the merc picker would otherwise
        // keep offering a destroyed settlement's species until reload).
        private static readonly AnimalHusbandryLifecycleHandler _lifecycleHandler = new AnimalHusbandryLifecycleHandler();

        static AnimalHusbandryStartup()
        {
            // Gate the base mod's unit-designer animal/mount pickers to stocked animals.
            EmpireRegistry.Register(MercAnimalStockFilter.Instance);
            EmpireRegistry.Register(_lifecycleHandler);

            EmpireCacheUtil.RegisterCacheInvalidator("AnimalHusbandry", () =>
            {
                AnimalProductMap.Clear();
                CloningCache.Clear();
                StockedAnimalCache.Clear();
                // InvalidateAll runs EmpireRegistry.ClearAll() before these callbacks, so re-register.
                EmpireRegistry.Register(MercAnimalStockFilter.Instance);
                EmpireRegistry.Register(_lifecycleHandler);
            });
            AnimalProductMap.EnsureBuilt();
            FCAHSettings.InitializeBasicAnimals();
            LogAH.MessageForce("Animal Husbandry initialized");
        }
    }

    public class AnimalHusbandryMod : Mod
    {
        public FCAHSettings settings;

        public AnimalHusbandryMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FCAHSettings>();
            
            string modVersion = content?.ModMetaData?.ModVersion;
            if (modVersion.NullOrEmpty())
            {
                LogAH.MessageForce("Did not load a mod version");
            }
            else
            {
                LogAH.MessageForce($"v{modVersion}");
            }
        }

        public override string SettingsCategory() => "AH_Title".Translate();

        public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
    }
}
