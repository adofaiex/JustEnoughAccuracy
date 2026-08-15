using UnityModManagerNet;

namespace JustEnoughAccuracy.Loaders
{
    public static class UmmEntry
    {
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            var handler = new UmmHandler(modEntry);
            return Main.Initialize(handler);
        }
    }
}
