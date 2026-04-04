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
        public static List<string> BasicAnimalDefNames = new List<string>
        {
            "Chicken", "Cow", "Pig", "Sheep", "Goat"
        };

        private static List<ThingDef> resolvedBasicAnimals;
        public static List<ThingDef> ResolvedBasicAnimals
        {
            get
            {
                if (resolvedBasicAnimals == null)
                    ResolveBasicAnimals();
                return resolvedBasicAnimals;
            }
        }

        public static void ResolveBasicAnimals()
        {
            resolvedBasicAnimals = new List<ThingDef>();
            foreach (string defName in BasicAnimalDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                    resolvedBasicAnimals.Add(def);
                else
                    LogAH.Warning($"Basic animal def '{defName}' not found");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref BasicAnimalsEnabled, "basicAnimalsEnabled", true);
            Scribe_Collections.Look(ref BasicAnimalDefNames, "basicAnimalDefNames", LookMode.Value);
            if (BasicAnimalDefNames == null)
                BasicAnimalDefNames = new List<string> { "Chicken", "Cow", "Pig", "Sheep", "Goat" };
            resolvedBasicAnimals = null;
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);

            ls.CheckboxLabeled("Enable debug logging", ref printDebug);
            ls.GapLine();

            bool prevBasic = BasicAnimalsEnabled;
            ls.CheckboxLabeled(
                "AH_BasicAnimalsEnabled".Translate(),
                ref BasicAnimalsEnabled,
                "AH_BasicAnimalsEnabledDesc".Translate());

            if (prevBasic != BasicAnimalsEnabled)
                FactionCache.FactionComp?.InvalidateAllSettlementStatCaches();

            if (BasicAnimalsEnabled)
            {
                ls.Gap(8f);
                ls.Label("AH_BasicAnimalsList".Translate());

                List<string> toRemove = new List<string>();
                foreach (string defName in BasicAnimalDefNames)
                {
                    Rect row = ls.GetRect(24f);
                    Widgets.Label(new Rect(row.x + 12, row.y, row.width - 40, row.height), defName);
                    if (Widgets.ButtonImage(new Rect(row.xMax - 24, row.y, 20, 20), TexButton.Delete))
                        toRemove.Add(defName);
                }
                foreach (string name in toRemove)
                {
                    BasicAnimalDefNames.Remove(name);
                    ResolveBasicAnimals();
                    FactionCache.FactionComp?.InvalidateAllSettlementStatCaches();
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
        private static readonly AnimalHusbandryLifecycleHandler _lifecycleHandler = new AnimalHusbandryLifecycleHandler();

        static AnimalHusbandryStartup()
        {
            new Harmony("Matathias.Empire.AnimalHusbandry").PatchAll(Assembly.GetExecutingAssembly());
            LifecycleRegistry.Register(_lifecycleHandler);
            EmpireCacheUtil.RegisterCacheInvalidator("AnimalHusbandry", () =>
            {
                AnimalProductMap.Clear();
                CloningCache.Clear();
                // Re-register after InvalidateAll clears all registries
                LifecycleRegistry.Register(_lifecycleHandler);
            });
            AnimalProductMap.EnsureBuilt();
            FCAHSettings.ResolveBasicAnimals();
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
