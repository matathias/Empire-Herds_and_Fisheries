using System;
using System.Collections.Generic;
using System.Linq;
using FactionColonies;
using RimWorld;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Generates tithe items from the filtered set of allowed animals and their products.
    /// Mirrors ThingSetMaker_Animals logic but restricts to allowed species.
    /// </summary>
    public class ThingSetMaker_AnimalHusbandry : ThingSetMaker
    {
        private readonly HashSet<ThingDef> allowedAnimals;

        private const int MAX_ATTEMPTS = 200;
        private const int MAX_ATTEMPTS_FEW = 20;

        public ThingSetMaker_AnimalHusbandry(HashSet<ThingDef> allowedAnimals)
        {
            this.allowedAnimals = allowedAnimals;
        }

        protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
        {
            List<PawnKindDef> animalDefs = new List<PawnKindDef>();
            foreach (PawnKindDef def in FactionCache.AllAnimalKindDefs)
            {
                if (!parms.filter.Allows(def.race)) continue;
                if (allowedAnimals is object && !allowedAnimals.Contains(def.race)) continue;
                animalDefs.Add(def);
            }

            List<ThingDef> productDefs = new List<ThingDef>();
            foreach (ThingDef def in parms.filter.AllowedThingDefs)
            {
                if (def.race is object) continue;
                if (allowedAnimals is object && !AnimalProductMap.IsProductAllowed(def, allowedAnimals))
                    continue;
                productDefs.Add(def);
            }

            int totalOptions = animalDefs.Count + productDefs.Count;
            if (totalOptions == 0)
            {
                LogAH.Warning("ThingSetMaker_AnimalHusbandry: no defs satisfied the criteria");
                return;
            }

            float cheapestAnimalValue = animalDefs.Count > 0
                ? animalDefs.Min(def => def.race.BaseMarketValue) : float.MaxValue;
            float cheapestProductValue = productDefs.Count > 0
                ? productDefs.Min(def => def.BaseMarketValue) : float.MaxValue;

            float totalValue = 0;
            int totalAttempts = 0;
            int minCount = parms.countRange.HasValue ? parms.countRange.Value.min : 0;
            int maxCount = parms.countRange.HasValue ? parms.countRange.Value.max : MAX_ATTEMPTS;
            float maxBudget = parms.totalMarketValueRange.Value.max;

            do
            {
                float remainingBudget = maxBudget - totalValue;
                if (remainingBudget < cheapestAnimalValue && remainingBudget < cheapestProductValue)
                    break;

                bool canAffordAnimal = animalDefs.Count > 0 && remainingBudget >= cheapestAnimalValue;
                bool canAffordProduct = productDefs.Count > 0 && remainingBudget >= cheapestProductValue;
                bool pickAnimal = canAffordAnimal
                    && (!canAffordProduct || Rand.Range(0, totalOptions) < animalDefs.Count);

                if (pickAnimal)
                {
                    Pawn pawn = null;
                    int attempts = 0;
                    do
                    {
                        if (pawn is object) { pawn.Destroy(); pawn = null; }
                        PawnGenerationRequest request = new PawnGenerationRequest(
                            kind: animalDefs.RandomElement(),
                            faction: Find.FactionManager.OfPlayer,
                            allowAddictions: false,
                            worldPawnFactionDoesntMatter: true);
                        pawn = PawnGenerator.GeneratePawn(request);
                        attempts++;
                    }
                    while (pawn.MarketValue + totalValue > maxBudget && attempts < MAX_ATTEMPTS_FEW);

                    if (attempts >= MAX_ATTEMPTS_FEW)
                    {
                        if (pawn is object) pawn.Destroy();
                    }
                    else
                    {
                        totalValue += pawn.MarketValue;
                        outThings.Add(pawn);
                    }
                }
                else if (canAffordProduct)
                {
                    ThingDef productDef = productDefs.RandomElement();
                    if (productDef.BaseMarketValue <= remainingBudget)
                    {
                        int stackCount = Math.Max(1, Math.Min(
                            (int)(remainingBudget / productDef.BaseMarketValue),
                            productDef.stackLimit));
                        Thing thing = ThingMaker.MakeThing(productDef);
                        thing.stackCount = stackCount;
                        totalValue += productDef.BaseMarketValue * stackCount;
                        outThings.Add(thing);
                    }
                }
                totalAttempts++;
            }
            while ((outThings.Count < minCount || totalValue < parms.totalMarketValueRange.Value.min) &&
                   !(outThings.Count >= maxCount || totalValue >= maxBudget) &&
                   totalAttempts < MAX_ATTEMPTS);
        }

        protected override IEnumerable<ThingDef> AllGeneratableThingsDebugSub(ThingSetMakerParams parms)
        {
            List<ThingDef> list = new List<ThingDef>();
            foreach (PawnKindDef def in FactionCache.AllAnimalKindDefs)
            {
                if (allowedAnimals is null || allowedAnimals.Contains(def.race))
                    list.Add(def.race);
            }
            if (parms.filter is object)
            {
                foreach (ThingDef def in parms.filter.AllowedThingDefs)
                {
                    if (def.race is null &&
                        (allowedAnimals is null || AnimalProductMap.IsProductAllowed(def, allowedAnimals)))
                        list.Add(def);
                }
            }
            return list;
        }
    }
}
