using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    public class Dialog_RegisterAnimals : Window
    {
        private readonly Caravan caravan;
        private readonly WorldObjectComp_AnimalRegistry comp;
        private readonly List<ThingDef> registrableSpecies;
        private readonly Dictionary<ThingDef, bool> selections = new Dictionary<ThingDef, bool>();
        private Vector2 scrollPosition;

        public override Vector2 InitialSize
        {
            get { return new Vector2(400f, 500f); }
        }

        public Dialog_RegisterAnimals(Caravan caravan, WorldObjectComp_AnimalRegistry comp,
            List<ThingDef> registrableSpecies)
        {
            this.caravan = caravan;
            this.comp = comp;
            this.registrableSpecies = registrableSpecies;
            foreach (ThingDef species in registrableSpecies)
                selections[species] = false;
            doCloseButton = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            Widgets.Label(titleRect, "AH_RegisterTitle".Translate(comp.Settlement.Name));
            Text.Font = GameFont.Small;

            float y = titleRect.yMax + 10f;
            Rect descRect = new Rect(inRect.x, y, inRect.width, 40f);
            Widgets.Label(descRect, "AH_RegisterDesc".Translate());
            y = descRect.yMax + 10f;

            float listHeight = inRect.height - y - 50f;
            Rect listRect = new Rect(inRect.x, y, inRect.width, listHeight);
            float viewHeight = registrableSpecies.Count * 36f;
            Rect viewRect = ScrollUtil.BeginScrollView(listRect, ref scrollPosition, viewHeight);

            float rowY = 0;
            foreach (ThingDef species in registrableSpecies)
            {
                Rect row = new Rect(0, rowY, viewRect.width, 34f);

                int count = caravan.PawnsListForReading
                    .Count(p => p.RaceProps is object && p.RaceProps.Animal && p.def == species);

                bool selected = selections[species];
                Rect checkRect = new Rect(row.x + 4, row.y + 4, 24, 24);
                Widgets.Checkbox(checkRect.position, ref selected);
                selections[species] = selected;

                Rect iconRect = new Rect(checkRect.xMax + 8, row.y + 3, 28, 28);
                Widgets.ThingIcon(iconRect, species);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, row.width - 180, 34f);
                Widgets.Label(labelRect, species.LabelCap);

                Rect countRect = new Rect(row.xMax - 60, row.y, 56, 34f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(countRect, "x" + count);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Mouse.IsOver(row))
                    Widgets.DrawHighlight(row);

                rowY += 36f;
            }

            ScrollUtil.EndScrollView();

            float buttonY = inRect.yMax - 40f;
            float buttonWidth = 120f;
            float gap = 20f;
            int selectedCount = selections.Count(kvp => kvp.Value);

            Rect confirmRect = new Rect(
                inRect.x + inRect.width / 2 - buttonWidth - gap / 2,
                buttonY, buttonWidth, 35f);
            Rect cancelRect = new Rect(
                inRect.x + inRect.width / 2 + gap / 2,
                buttonY, buttonWidth, 35f);

            if (Widgets.ButtonText(confirmRect, "AH_Confirm".Translate() +
                (selectedCount > 0 ? " (" + selectedCount + ")" : ""),
                active: selectedCount > 0))
            {
                DoRegistration();
                Close();
            }

            if (Widgets.ButtonText(cancelRect, "AH_Cancel".Translate()))
            {
                Close();
            }
        }

        private void DoRegistration()
        {
            foreach (KeyValuePair<ThingDef, bool> kvp in selections)
            {
                if (!kvp.Value) continue;
                ThingDef species = kvp.Key;

                List<Pawn> animals = caravan.PawnsListForReading
                    .Where(p => p.RaceProps is object && p.RaceProps.Animal && p.def == species)
                    .ToList();

                // Prefer 1 male + 1 female
                Pawn male = animals.FirstOrDefault(p => p.gender == Gender.Male);
                Pawn female = animals.FirstOrDefault(p => p.gender == Gender.Female);

                List<Pawn> toRemove = new List<Pawn>();
                if (male is object && female is object)
                {
                    toRemove.Add(male);
                    toRemove.Add(female);
                }
                else
                {
                    toRemove.AddRange(animals.Take(2));
                }

                foreach (Pawn pawn in toRemove)
                {
                    caravan.RemovePawn(pawn);
                    pawn.Destroy();
                }

                comp.RegisterAnimal(species);
                LogAH.MessageForce($"Registered {species.defName} at {comp.Settlement.Name}");
            }
        }
    }
}
