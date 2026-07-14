using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Pure coverage for <see cref="AnimalStockEntry"/> completion / needs / add semantics. No game
    /// state required — uses throwaway <see cref="ThingDef"/> fixtures whose only relevant property is
    /// <c>race.hasGenders</c> (<see cref="AHTestHelper.MakeAnimalDef"/>).
    /// </summary>
    public static class AnimalStockEntryTests
    {
        private static AnimalStockEntry Gendered() => new AnimalStockEntry(AHTestHelper.MakeAnimalDef(true));
        private static AnimalStockEntry Genderless() => new AnimalStockEntry(AHTestHelper.MakeAnimalDef(false));

        /*-*-*- IsComplete -*-*-*/

        [EmpireTest("AH.StockEntry")]
        public static void IsComplete_GenderedNeedsBothSexes()
        {
            AnimalStockEntry e = Gendered();
            TestAssert.IsFalse(e.IsComplete, "empty gendered entry should be incomplete");

            e.Add(Gender.Male);
            TestAssert.IsFalse(e.IsComplete, "gendered entry with only a male should be incomplete");

            e.Add(Gender.Female);
            TestAssert.IsTrue(e.IsComplete, "gendered entry with a male and a female should be complete");
        }

        [EmpireTest("AH.StockEntry")]
        public static void IsComplete_GenderlessNeedsTwo()
        {
            AnimalStockEntry e = Genderless();
            TestAssert.IsFalse(e.IsComplete, "empty genderless entry should be incomplete");

            e.Add(Gender.None);
            TestAssert.IsFalse(e.IsComplete, "genderless entry with one individual should be incomplete");

            e.Add(Gender.None);
            TestAssert.IsTrue(e.IsComplete, "genderless entry with two individuals should be complete");
        }

        /*-*-*- Needs -*-*-*/

        [EmpireTest("AH.StockEntry")]
        public static void Needs_GenderedTracksEachSexIndependently()
        {
            AnimalStockEntry e = Gendered();
            TestAssert.IsTrue(e.Needs(Gender.Male), "empty entry needs a male");
            TestAssert.IsTrue(e.Needs(Gender.Female), "empty entry needs a female");
            // A gendered species never "needs" a genderless individual.
            TestAssert.IsFalse(e.Needs(Gender.None), "gendered entry should not need a genderless individual");

            e.Add(Gender.Male);
            TestAssert.IsFalse(e.Needs(Gender.Male), "entry with a male no longer needs a male");
            TestAssert.IsTrue(e.Needs(Gender.Female), "entry with a male still needs a female");
        }

        [EmpireTest("AH.StockEntry")]
        public static void Needs_GenderlessIgnoresGenderUntilCapped()
        {
            AnimalStockEntry e = Genderless();
            TestAssert.IsTrue(e.Needs(Gender.None), "empty genderless entry needs an individual");
            TestAssert.IsTrue(e.Needs(Gender.Male), "genderless entry ignores the queried gender");

            e.Add(Gender.None);
            TestAssert.IsTrue(e.Needs(Gender.None), "genderless entry with one still needs another");

            e.Add(Gender.None);
            TestAssert.IsFalse(e.Needs(Gender.None), "genderless entry at cap needs nothing more");
        }

        /*-*-*- Add -*-*-*/

        [EmpireTest("AH.StockEntry")]
        public static void Add_ReturnsFalseWhenComboAlreadyStocked()
        {
            AnimalStockEntry e = Gendered();
            TestAssert.IsTrue(e.Add(Gender.Male), "first male should be recorded");
            TestAssert.IsFalse(e.Add(Gender.Male), "second male should be a no-op");
            TestAssert.IsTrue(e.hasMale, "the male flag should remain set");
            TestAssert.IsFalse(e.hasFemale, "over-stocking a male should not fill the female slot");
        }

        [EmpireTest("AH.StockEntry")]
        public static void Add_GenderlessCapsAtTwo()
        {
            AnimalStockEntry e = Genderless();
            TestAssert.IsTrue(e.Add(Gender.None), "first genderless individual recorded");
            TestAssert.IsTrue(e.Add(Gender.None), "second genderless individual recorded");
            TestAssert.IsFalse(e.Add(Gender.None), "a third genderless individual should be a no-op");
            TestAssert.AreEqual(2, e.genderless, "genderless count should be capped at 2");
        }

        [EmpireTest("AH.StockEntry")]
        public static void Add_GenderedRejectsGenderlessIndividual()
        {
            AnimalStockEntry e = Gendered();
            TestAssert.IsFalse(e.Add(Gender.None),
                "a gendered species should not accept a genderless individual");
            TestAssert.IsFalse(e.IsComplete, "the entry should remain incomplete");
        }
    }
}
