using LudeonTK;
using System.Reflection;

namespace FactionColonies.AnimalHusbandry;

/// <summary>
/// Herds & Fisheries test runner: the debug-menu entry points for this mod's tests. Discovery, running and
/// reporting live in the base mod's <see cref="EmpireTestRunner"/>; this class only names the
/// suite (its assembly, the <see cref="LogAH"/> logger and its destructive-run warnings).
/// </summary>
public static class AHTestRunner
{
    private const string DebugCategory = "Empire Refactored: Herds & Fisheries";

    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        "Herds & Fisheries",
        "DESTRUCTIVE Herds & Fisheries tests mutate live game state: they create transient "
            + "settlements, construct/deconstruct buildings, stock animals, and generate then destroy "
            + "pawns. Transient settlements are removed at the end of each test, but any residue a test "
            + "leaves is NOT auto-reverted.",
        "Destructive tests create transient settlements, buildings and pawns. "
            + "Each test removes what it created, but if a test failed partway its residue may "
            + "persist. Reload your pre-test save to be safe.",
        LogAH.Message, LogAH.Error, LogAH.MessageForce);

    /*-*-*- Standard (non-destructive) tier -*-*-*/

    [DebugAction(DebugCategory, "Run All Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAll() => EmpireTestRunner.RunTests(Suite, null, destructive: false);

    [DebugAction(DebugCategory, "Run Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunByCategory() => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: false);

    /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

    [DebugAction(DebugCategory, "Run Destructive Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAllDestructive() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.RunTests(Suite, null, destructive: true));

    [DebugAction(DebugCategory, "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunDestructiveByCategory() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: true));
}
