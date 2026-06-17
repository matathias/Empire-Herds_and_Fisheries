using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using UnityEngine;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Searchable single-select picker that lists every animal race ThingDef not already in
    /// <see cref="FCAHSettings.BasicAnimals"/>. Clicking a row adds that animal to the basic list
    /// (invalidating settlement stat caches) and closes the dialog. Used from the mod settings.
    /// </summary>
    public class Dialog_PickBasicAnimal : Window
    {
        private readonly List<ThingDef> candidates;
        private string searchTerm = "";
        private Vector2 scrollPosition;

        private const float RowHeight = 34f;

        public override Vector2 InitialSize
        {
            get { return new Vector2(420f, 560f); }
        }

        public Dialog_PickBasicAnimal()
        {
            // Use the same animal filter as the RTD_Animals resource (FactionCache.AllAnimalKindDefs is
            // built from PawnKindDefExtensions.IsAnimalAndAllowed), so the picker allows/blacklists the
            // same animals - notably excluding dryads, monster, and genetic animals. Map each allowed
            // kind to its race ThingDef (BasicAnimals is race-based, like the rest of the submod) and
            // de-duplicate, since multiple PawnKindDefs can share a race.
            candidates = FactionCache.AllAnimalKindDefs
                .Select(kind => kind.race)
                .Distinct()
                .Where(race => !FCAHSettings.BasicAnimals.Contains(race))
                .OrderBy(d => d.label ?? d.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            Widgets.Label(titleRect, "AH_PickBasicAnimalTitle".Translate());
            Text.Font = GameFont.Small;

            float y = titleRect.yMax + 6f;
            Rect searchRect = new Rect(inRect.x, y, inRect.width, 28f);
            searchTerm = Widgets.TextField(searchRect, searchTerm);
            if (string.IsNullOrEmpty(searchTerm))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(searchRect.x + 6f, searchRect.y, searchRect.width - 12f, searchRect.height),
                    "AH_PickBasicAnimalSearch".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            y = searchRect.yMax + 8f;

            List<ThingDef> shown = candidates;
            if (!string.IsNullOrEmpty(searchTerm))
                shown = candidates
                    .Where(d => (d.label ?? d.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

            Rect listRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 4f);
            float viewHeight = shown.Count * RowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(listRect, ref scrollPosition, viewHeight);

            ThingDef chosen = null;
            float rowY = 0;
            foreach (ThingDef def in shown)
            {
                Rect row = new Rect(0, rowY, viewRect.width, RowHeight);

                Rect iconRect = new Rect(row.x + 4, row.y + 3, 28, 28);
                Widgets.ThingIcon(iconRect, def);

                Rect labelRect = new Rect(iconRect.xMax + 8, row.y, row.width - iconRect.width - 16, RowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, def.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Mouse.IsOver(row))
                    Widgets.DrawHighlight(row);

                if (Widgets.ButtonInvisible(row))
                    chosen = def;

                rowY += RowHeight;
            }

            ScrollUtil.EndScrollView();

            if (shown.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(listRect, "AH_PickBasicAnimalNone".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (chosen is object)
            {
                FCAHSettings.BasicAnimals.Add(chosen);
                FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
                Close();
            }
        }
    }
}
