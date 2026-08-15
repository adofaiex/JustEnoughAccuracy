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

            // Showing is driven by a patch on DetailedResults.Show(), so the button
            // only ever appears when the official results screen actually displays
            // (never in the editor, and never after we leave the Won state).
            // Hide once we're no longer on the results screen: the state machine is
            // the reliable signal (activeSelf can stay true in the main menu).
            var ctl = scrController.instance;
            var resultsVisible = ctl != null
                                 && ctl.currentState == States.Won
                                 && ctl.detailedResults != null
                                 && ctl.detailedResults.gameObject.activeSelf;

            if (!resultsVisible)
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
