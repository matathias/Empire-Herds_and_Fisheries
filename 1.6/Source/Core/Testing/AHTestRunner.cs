using LudeonTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    /// <summary>
    /// Herds &amp; Fisheries test runner. A submod-local copy of the base mod's EmpireTestRunner shape:
    /// it reuses the base mod's public test surface (<see cref="EmpireTestAttribute"/> /
    /// <see cref="EmpireDestructiveTestAttribute"/>, <see cref="TestAssert"/>,
    /// <see cref="DestructiveTestUtil"/>) but discovers tests in this submod's own assembly
    /// (<see cref="Assembly.GetExecutingAssembly"/> resolves here), so the base runner needs no change
    /// to see them. Results are logged with the <c>[Empire-AnimalHusbandry]</c> slug via <see cref="LogAH"/>.
    /// </summary>
    public static class AHTestRunner
    {
        private const string DebugCategory = "Empire Refactored: Herds & Fisheries";

        /*-*-*- Standard (non-destructive) tier -*-*-*/

        [DebugAction(DebugCategory, "Run All Tests", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunAll() => RunTests(null, destructive: false);

        [DebugAction(DebugCategory, "Run Tests by Category", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunByCategory() => ShowCategoryMenu(destructive: false);

        /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

        [DebugAction(DebugCategory, "Run Destructive Tests", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunAllDestructive() =>
            ConfirmDestructive(() => RunTests(null, destructive: true));

        [DebugAction(DebugCategory, "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunDestructiveByCategory() =>
            ConfirmDestructive(() => ShowCategoryMenu(destructive: true));

        private static void ConfirmDestructive(Action confirmedAct)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "DESTRUCTIVE Herds & Fisheries tests mutate live game state: they create transient "
                + "settlements, construct/deconstruct buildings, stock animals, and generate then destroy "
                + "pawns. Transient settlements are removed at the end of each test, but any residue a test "
                + "leaves is NOT auto-reverted.\n\n"
                + "SAVE FIRST. The runner will not crash, but your game state may be thrashed.\n\n"
                + "Continue?",
                confirmedAct, destructive: true, title: "Run Destructive Herds & Fisheries Tests"));
        }

        private static void ShowCategoryMenu(bool destructive)
        {
            var categories = DiscoverTests()
                .Where(t => t.attr.Destructive == destructive)
                .Select(t => t.attr.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var options = new List<DebugMenuOption>();
            foreach (string cat in categories)
            {
                string local = cat;
                options.Add(new DebugMenuOption(local, DebugMenuOptionMode.Action,
                    () => RunTests(local, destructive)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        public static void RunTests(string category, bool destructive = false)
        {
            var tests = DiscoverTests().Where(t => t.attr.Destructive == destructive);
            if (category != null)
                tests = tests.Where(t => t.attr.Category == category);
            var list = tests.ToList();

            int passed = 0, failed = 0, errors = 0, skipped = 0;
            var skipDetails = new List<string>();
            var failDetails = new List<string>();
            var errorDetails = new List<string>();
            foreach (var (method, attr) in list)
            {
                string testName = $"[{attr.Category}] {method.DeclaringType.Name}.{method.Name}";
                try
                {
                    method.Invoke(null, null);
                    passed++;
                    LogAH.Message($"PASS: {testName}");
                }
                catch (TargetInvocationException tie) when (tie.InnerException is TestSkippedException tse)
                {
                    skipped++;
                    LogAH.Message($"SKIP: {testName} -- {tse.Message}");
                    skipDetails.Add($"  SKIP: {testName} -- {tse.Message}");
                }
                catch (TargetInvocationException tie) when (tie.InnerException is TestFailedException tfe)
                {
                    failed++;
                    LogAH.Error($"FAIL: {testName} -- {tfe.Message}");
                    failDetails.Add($"  FAIL: {testName} -- {tfe.Message}");
                }
                catch (Exception ex)
                {
                    errors++;
                    var inner = ex is TargetInvocationException t ? t.InnerException : ex;
                    LogAH.Error($"ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
                    errorDetails.Add($"  ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
                }
            }

            string label = (category != null ? $"[{category}]" : "[All]")
                + (destructive ? " DESTRUCTIVE" : "");
            LogAH.MessageForce($"Test results {label}: {passed} passed, {failed} failed, {errors} errors, {skipped} skipped (of {list.Count} total)");
            if (failDetails.Count > 0)
                LogAH.MessageForce("Failed tests:\n" + string.Join("\n", failDetails));
            if (errorDetails.Count > 0)
                LogAH.MessageForce("Errored tests:\n" + string.Join("\n", errorDetails));
            if (skipDetails.Count > 0)
                LogAH.MessageForce("Skipped tests:\n" + string.Join("\n", skipDetails));
            if (destructive && list.Count > 0)
                LogAH.MessageForce("Destructive tests create transient settlements, buildings and pawns. "
                    + "Each test removes what it created, but if a test failed partway its residue may "
                    + "persist. Reload your pre-test save to be safe.");
        }

        private static List<(MethodInfo method, EmpireTestAttribute attr)> DiscoverTests()
        {
            return Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Select(m => (method: m, attr: m.GetCustomAttribute<EmpireTestAttribute>()))
                .Where(pair => pair.attr != null)
                .OrderBy(pair => pair.attr.Category)
                .ThenBy(pair => pair.method.Name)
                .ToList();
        }
    }
}
