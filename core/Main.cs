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

        private static bool _resultsWereVisible;
        private static bool _sawWon;

        private static void OnGameUpdate()
        {
            if (!Settings.Enabled)
            {
                JePreviewer.Close();
                ResultsScreenButton.Hide();
                return;
            }

            // The button is shown by a patch on DetailedResults.Show(); here we only
            // decide when to hide it. Show() runs BEFORE ChangeState(Won) and
            // currentState is only refreshed in scrController.Update(), so during
            // the frame Show() fires the state is still stale — don't require Won
            // until it has been observed at least once. Conversely, pressing Esc on
            // the results screen leaves Won WITHOUT deactivating the GameObject
            // (only the next level load does, scnGame) — so once Won was seen,
            // leaving Won means the player left the results screen.
            var ctl = scrController.instance;
            var resultsActive = ctl != null
                                && ctl.detailedResults != null
                                && ctl.detailedResults.gameObject.activeSelf;
            if (resultsActive && ctl!.currentState == States.Won)
                _sawWon = true;
            var resultsVisible = resultsActive && (!_sawWon || ctl!.currentState == States.Won);

            if (!resultsVisible)
            {
                JePreviewer.Close();
                ResultsScreenButton.Hide();
                _sawWon = false;
                // Leaving the results screen ends the run: wipe the data.
                if (_resultsWereVisible)
                    JudgementRecorder.Clear();
            }
            else if (!_resultsWereVisible)
            {
                Main.Handler?.Log($"[JEA][Main] resultsVisible became true (state={ctl?.currentState})");
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
