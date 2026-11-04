namespace FactionColonies.AnimalHusbandry;

public static class LogAH
{
    public const string Slug = "[Empire-AnimalHusbandry]";

    /// <summary>Gates <see cref="Message(ref DebugLogHandler{DebugGate})"/> on this mod's debug setting.</summary>
    public readonly struct DebugGate : IDebugLogGate
    {
        public static bool Enabled => FCAHSettings.PrintDebug;
    }

    public static void Message(string message)
    {
        if (FCAHSettings.PrintDebug)
            Log.Message($"{Slug} {message}");
    }

    /// <summary>
    /// Debug message from an interpolated string. The compiler picks this overload for every
    /// <c>Message($"...")</c> call; when debug logging is off, the string's holes are never evaluated
    /// and nothing is allocated.
    /// </summary>
    public static void Message(ref DebugLogHandler<DebugGate> message)
    {
        if (!message.Enabled) return;
        Log.Message($"{Slug} {message.ToStringAndClear()}");
    }
    public static void MessageForce(string message)
    {
        Log.Message($"{Slug}[Force] {message}");
    }

    public static void Warning(string message)
    {
        Log.Warning($"{Slug}[WARN] {message}");
    }

    public static void Error(string message)
    {
        Log.Error($"{Slug}[ERR] {message}");
    }
}
