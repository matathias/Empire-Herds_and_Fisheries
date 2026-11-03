using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Lets the player drop off individual animals from a caravan to stock a settlement. Each species
    /// row offers per-sex contribution (gendered) or a count toward 2 (genderless); a sex already
    /// present at the settlement, or absent from the caravan, is not selectable. Production starts once
    /// a species has both a male and a female (or two genderless individuals). Progress persists.
    /// </summary>
    public class Dialog_RegisterAnimals : Window
    {
        private struct SexSelection
        {
            public bool male;
            public bool female;
            public int genderless;   // number of genderless individuals to contribute (0..needed/available)
        }

        private struct CaravanAvail
        {
            public int males;
            public int females;
            public int total;
        }

        private readonly Caravan caravan;
        private readonly WorldObjectComp_AnimalRegistry comp;
        private readonly List<ThingDef> species;
        private readonly Dictionary<ThingDef, SexSelection> selections = new Dictionary<ThingDef, SexSelection>();
        private readonly Dictionary<ThingDef, CaravanAvail> caravanAvail = new Dictionary<ThingDef, CaravanAvail>();
        private Vector2 scrollPosition;

        private const float RowHeight = 40f;
        private const float CellWidth = 108f;

        public override Vector2 InitialSize
        {
            get { return new Vector2(480f, 520f); }
        }

        public Dialog_RegisterAnimals(Caravan caravan, WorldObjectComp_AnimalRegistry comp,
            List<ThingDef> contributableSpecies)
        {
            this.caravan = caravan;
            this.comp = comp;
            species = contributableSpecies;

            foreach (ThingDef sp in contributableSpecies)
            {
                selections[sp] = default(SexSelection);

                List<Pawn> pawns = caravan.PawnsListForReading
                    .Where(p => p.RaceProps is object && p.RaceProps.Animal && p.def == sp)
                    .ToList();
                CaravanAvail avail = new CaravanAvail
                {
                    males = pawns.Count(p => p.gender == Gender.Male),
                    females = pawns.Count(p => p.gender == Gender.Female),
                    total = pawns.Count
                };
                caravanAvail[sp] = avail;
            }

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

            float y = titleRect.yMax + 8f;
            Rect descRect = new Rect(inRect.x, y, inRect.width, 64f);
            Widgets.Label(descRect, "AH_RegisterDesc".Translate());
            y = descRect.yMax + 8f;

            float listHeight = inRect.height - y - 50f;
            Rect listRect = new Rect(inRect.x, y, inRect.width, listHeight);
            float viewHeight = species.Count * RowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(listRect, ref scrollPosition, viewHeight);

            float rowY = 0;
            foreach (ThingDef sp in species)
            {
                DrawSpeciesRow(new Rect(0, rowY, viewRect.width, RowHeight), sp);
                rowY += RowHeight;
            }

            ScrollUtil.EndScrollView();

            float buttonY = inRect.yMax - 40f;
            float buttonWidth = 120f;
            float gap = 20f;
            int selectedCount = CountSelectedIndividuals();

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
                DoContribution();
                Close();
            }

            if (Widgets.ButtonText(cancelRect, "AH_Cancel".Translate()))
            {
                Close();
            }
        }

        private void DrawSpeciesRow(Rect row, ThingDef sp)
        {
            if (Mouse.IsOver(row))
                Widgets.DrawHighlight(row);

            Rect iconRect = new Rect(row.x + 4, row.y + 6, 28, 28);
            Widgets.ThingIcon(iconRect, sp);

            AnimalStockEntry snap = comp.GetStockSnapshot(sp);
            CaravanAvail avail = caravanAvail[sp];
            SexSelection sel = selections[sp];

            if (sp.race.hasGenders)
            {
                Rect maleCell = new Rect(row.xMax - CellWidth * 2 - 8, row.y, CellWidth, row.height);
                Rect femaleCell = new Rect(row.xMax - CellWidth - 4, row.y, CellWidth, row.height);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, maleCell.x - iconRect.xMax - 12, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, sp.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                sel.male = DrawSexCell(maleCell, Gender.Male, snap.hasMale, avail.males, sel.male);
                sel.female = DrawSexCell(femaleCell, Gender.Female, snap.hasFemale, avail.females, sel.female);
            }
            else
            {
                Rect cell = new Rect(row.xMax - CellWidth * 2 - 8, row.y, CellWidth * 2 + 4, row.height);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, cell.x - iconRect.xMax - 12, row.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, sp.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                int need = 2 - snap.genderless;
                int canGive = Mathf.Min(need, avail.total);

                Rect checkRect = new Rect(cell.x, cell.y + (cell.height - 24f) / 2f, 24f, 24f);
                Rect lblRect = new Rect(checkRect.xMax + 4, cell.y, cell.xMax - checkRect.xMax - 4, cell.height);

                bool val = sel.genderless > 0;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(lblRect, "AH_GenderlessSend".Translate(canGive, snap.genderless, 2));
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Checkbox(checkRect.position, ref val, 24f, canGive <= 0);
                sel.genderless = (val && canGive > 0) ? canGive : 0;
            }

            selections[sp] = sel;
        }

        // Draws one sex's control: static "have" text if already stocked, else an (en/dis)abled checkbox.
        private bool DrawSexCell(Rect cell, Gender gender, bool settlementHas, int caravanCount, bool selected)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            if (settlementHas)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.5f, 0.85f, 0.5f);
                Widgets.Label(cell, (gender == Gender.Male ? "AH_HaveMale" : "AH_HaveFemale").Translate());
                GUI.color = prev;
                Text.Anchor = TextAnchor.UpperLeft;
                return false;
            }

            bool inCaravan = caravanCount > 0;
            Rect checkRect = new Rect(cell.x, cell.y + (cell.height - 24f) / 2f, 24f, 24f);
            Rect lblRect = new Rect(checkRect.xMax + 4, cell.y, cell.xMax - checkRect.xMax - 4, cell.height);

            string label = (gender == Gender.Male ? "AH_ColMale" : "AH_ColFemale").Translate();
            if (inCaravan) label += " (x" + caravanCount + ")";

            Color c = GUI.color;
            if (!inCaravan) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.Label(lblRect, label);
            GUI.color = c;
            Text.Anchor = TextAnchor.UpperLeft;

            bool val = selected && inCaravan;
            Widgets.Checkbox(checkRect.position, ref val, 24f, !inCaravan);
            return inCaravan && val;
        }

        private int CountSelectedIndividuals()
        {
            int n = 0;
            foreach (KeyValuePair<ThingDef, SexSelection> kvp in selections)
            {
                SexSelection s = kvp.Value;
                if (s.male) n++;
                if (s.female) n++;
                n += s.genderless;
            }
            return n;
        }

        private void DoContribution()
        {
            foreach (KeyValuePair<ThingDef, SexSelection> kvp in selections)
            {
                ThingDef sp = kvp.Key;
                SexSelection sel = kvp.Value;

                List<Pawn> pool = caravan.PawnsListForReading
                    .Where(p => p.RaceProps is object && p.RaceProps.Animal && p.def == sp)
                    .ToList();

                if (sp.race.hasGenders)
                {
                    if (sel.male)
                    {
                        Pawn m = pool.FirstOrDefault(p => p.gender == Gender.Male);
                        if (m is object) ConsumeAndRecord(m);
                    }
                    if (sel.female)
                    {
                        Pawn f = pool.FirstOrDefault(p => p.gender == Gender.Female);
                        if (f is object) ConsumeAndRecord(f);
                    }
                }
                else
                {
                    for (int i = 0; i < sel.genderless && i < pool.Count; i++)
                        ConsumeAndRecord(pool[i]);
                }
            }
        }

        // Detach from the caravan, then hand off to the registry's shared consume logic
        // (records the species/sex and destroys the pawn). Same path the transport-pod arrival uses.
        private void ConsumeAndRecord(Pawn pawn)
        {
            // Keep the animal's cargo with the caravan when another member can carry it (vanilla parity:
            // Settlement_TraderTracker does the same before selling a pack animal). Anything no remaining
            // member can take stays on the pawn and is rerouted home by StockIndividual, never destroyed.
            CaravanInventoryUtility.MoveAllInventoryToSomeoneElse(pawn, caravan.PawnsListForReading);
            caravan.RemovePawn(pawn);
            comp.StockIndividual(pawn);
        }
    }
}
