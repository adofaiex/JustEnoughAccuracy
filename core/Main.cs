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

            handler.Log("Mod initialized");
            return true;
        }

        private static bool _resultsWereVisible;

        private static void OnGameUpdate()
        {
            if (!Settings.Enabled)
            {
                JePreviewer.Close();
                ResultsScreenButton.Hide();
                return;
            }

            // Results screen visibility drives the button + data lifetime.
            var resultsVisible = scrController.instance?.detailedResults != null
                                 && scrController.instance.detailedResults.gameObject.activeSelf;

            if (resultsVisible)
            {
                ResultsScreenButton.Show();
            }
            else
            {
                ResultsScreenButton.Hide();
                JePreviewer.Close();
                // Leaving the results screen ends the run: wipe the data.
                if (_resultsWereVisible)
                    JudgementRecorder.Clear();
            }

            _resultsWereVisible = resultsVisible;
            JePreviewer.OnUpdate();
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
