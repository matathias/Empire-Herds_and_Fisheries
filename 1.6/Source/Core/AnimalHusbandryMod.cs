using System.Collections.Generic;
using System.Reflection;
using FactionColonies;
using HarmonyLib;
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref BasicAnimalsEnabled, "basicAnimalsEnabled", true);

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
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);

            ls.CheckboxLabeled("AH_DebugLogging".Translate(), ref printDebug);
            ls.GapLine();

            bool prevBasic = BasicAnimalsEnabled;
            ls.CheckboxLabeled(
                "AH_BasicAnimalsEnabled".Translate(),
                ref BasicAnimalsEnabled,
                "AH_BasicAnimalsEnabledDesc".Translate());

            if (prevBasic != BasicAnimalsEnabled)
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();

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
                    FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
                }
            }

            ls.Gap(12f);
            if (ls.ButtonText("AH_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empire.animalhusbandry", "AH_PatchTitle".Translate()));

            ls.End();
        }
    }

    [StaticConstructorOnStartup]
    public static class AnimalHusbandryStartup
    {
        static AnimalHusbandryStartup()
        {
            new Harmony("Matathias.Empire.AnimalHusbandry").PatchAll(Assembly.GetExecutingAssembly());
            EmpireCacheUtil.RegisterCacheInvalidator("AnimalHusbandry", () =>
            {
                AnimalProductMap.Clear();
                CloningCache.Clear();
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
