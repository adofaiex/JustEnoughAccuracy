namespace JustEnoughAccuracy
{
    public static class Main
    {
        public static IHandler Handler { get; private set; } = null!;
        public static Settings Settings { get; private set; } = null!;

        public static bool Initialize(IHandler handler)
        {
            Handler = handler;
            Settings = handler.LoadSettings<Settings>();
            PatchManager.Initialize(handler.ModId);

            handler.OnToggle += OnToggle;
            handler.OnUpdate += _ => OnGameUpdate();
            handler.OnGUI += JeSettingsUI.Draw;
            handler.OnSaveGUI += () => Handler.SaveSettings(Settings);

            handler.Log("Mod initialized");
            return true;
        }

        private static void OnGameUpdate()
        {
            if (!Settings.Enabled)
            {
                JePreviewer.Close();
                PreviewerButton.Hide();
                DeathMarker.Clear();
                return;
            }

            // The previewer icon is persistent (sits next to the official
            // difficulty selector) and its data survives Esc / results screens.
            // The recorder is only wiped when a run actually restarts
            // (scrMarginTracker_Reset) — not when leaving the results screen.
            PreviewerButton.OnUpdate();
            HitMarker.OnUpdate();
            DeathMarker.OnUpdate();
            JePreviewer.OnUpdate();

            // Winning an official (non-editor) level clears death markers: the run
            // is over and their positions are no longer relevant. In the editor,
            // markers persist until a new chart is opened instead.
            var ctl = scrController.instance;
            if (ctl != null && ctl.currentState == States.Won && !ADOBase.isLevelEditor)
                DeathMarker.Clear();
        }

        private static void OnToggle(bool value)
        {
            if (value)
            {
                Handler.Log("Mod enabled");
                PatchManager.UpdateAllPatches();
            }
            else
            {
                Handler.Log("Mod disabled");
                PatchManager.UnpatchAll();
            }
        }
    }
}
