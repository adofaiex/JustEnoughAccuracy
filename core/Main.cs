using System.Reflection;
using HarmonyLib;

namespace AdofaiMod.MultiLoader
{
    public static class Main
    {
        public static IHandler Handler { get; private set; } = null!;
        public static Harmony Harmony { get; private set; } = null!;
        public static Settings Settings { get; private set; } = null!;

        public static bool Initialize(IHandler handler)
        {
            Handler = handler;
            Settings = handler.LoadSettings<Settings>();
            Harmony = new Harmony(handler.ModId);

            handler.OnToggle += OnToggle;

            handler.Log("Mod initialized");
            return true;
        }

        private static void OnToggle(bool value)
        {
            if (value)
            {
                Handler.Log("Mod enabled");
                Harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            else
            {
                Handler.Log("Mod disabled");
                Harmony.UnpatchAll();
            }
        }
    }
}
