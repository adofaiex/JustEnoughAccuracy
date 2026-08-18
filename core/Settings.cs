namespace JustEnoughAccuracy
{
    /// <summary>
    /// User-visible mod settings. Only display toggles are configurable — the
    /// scoring model (bands, penalties, tolerance) is fixed by design and lives
    /// as constants in <see cref="JeaScore"/>.
    /// </summary>
    public class Settings
    {
        public bool Enabled { get; set; } = true;

        public bool DisplayInJudgementTexts { get; set; } = true;
        public bool NoDisplayPerfect { get; set; } = true;

        public bool DisplayInDetailedResults { get; set; } = true;

        /// <summary>Place a dashed hollow-circle marker at each ball's position when the player dies.</summary>
        public bool ShowDeathMarkers { get; set; } = true;

        /// <summary>Language code override (e.g. "en", "zh"); null = follow game language.</summary>
        public string? Language { get; set; }
    }
}
