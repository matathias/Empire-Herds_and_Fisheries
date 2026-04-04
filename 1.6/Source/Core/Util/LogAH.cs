using FactionColonies;
using Verse;

namespace FactionColonies.AnimalHusbandry
{
    public static class LogAH
    {
        public const string Slug = "[Empire-AnimalHusbandry]";

        public static void Message(string message)
        {
            if (FCAHSettings.PrintDebug)
                Log.Message($"{Slug} {message}");
        }
        public static void MessageForce(string message)
        {
            Log.Message($"{Slug} {message}");
        }

        public static void Warning(string message)
        {
            Log.Warning($"{Slug} {message}");
        }

        public static void Error(string message)
        {
            Log.Error($"{Slug} {message}");
        }
    }
}
