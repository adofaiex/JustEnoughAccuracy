using System.Collections.Generic;

namespace JustEnoughAccuracy
{
    public class Settings
    {
        public bool Enabled { get; set; } = true;

        public bool DisplayInJudgementTexts { get; set; } = true;
        public bool NoDisplayPerfect { get; set; } = true;

        public bool DisplayInDetailedResults { get; set; } = true;

        /// <summary>
        /// Angle thresholds (degrees) with their assigned scores.
        /// Tuned at <see cref="ReferenceBpm"/>. The measured deviation is scaled to that
        /// reference BPM before grading, so the same timing error in milliseconds gets the
        /// same score on any chart — matching how the official game widens its angle
        /// windows with speed (<c>TimeToAngleInRad(ms, bpm * speed)</c>).
        /// </summary>
        public List<AngleBand> AngleBands { get; set; } = new()
        {
            new AngleBand(1.0, 100),
            new AngleBand(2.0, 96),
            new AngleBand(3.0, 92),
            new AngleBand(4.0, 88),
            new AngleBand(5.0, 84),
            new AngleBand(6.0, 80),
            new AngleBand(8.0, 75),
            new AngleBand(10.0, 70),
            new AngleBand(13.0, 62),
            new AngleBand(16.0, 54),
            new AngleBand(20.0, 46),
            new AngleBand(25.0, 36),
            new AngleBand(30.0, 26),
            new AngleBand(40.0, 15)
        };

        /// <summary>
        /// The BPM the angle bands above are calibrated for. Deviation is normalized to
        /// this tempo before scoring. Uses the track's set BPM (conductor.bpm), not the
        /// pitch-adjusted one, so JEA feels the same on every chart.
        /// </summary>
        public double ReferenceBpm { get; set; } = 100;

        /// <summary>
        /// Score below which a tile is considered a break for the combo.
        /// The combo is tracked for display only; it does not multiply tile scores.
        /// </summary>
        public int ComboThreshold { get; set; } = 50;

        /// <summary>Legacy combo multiplier step, kept for settings compatibility (unused).</summary>
        public double ComboStep { get; set; } = 0.02;

        /// <summary>Legacy combo multiplier cap, kept for settings compatibility (unused).</summary>
        public double ComboMaxMultiplier { get; set; } = 3.0;

        /// <summary>
        /// Empty-press tolerance, mirroring the official <c>consecMultipressCounter &gt; 8</c> rule.
        /// The first N consecutive empty presses are forgiven entirely; after that each one
        /// costs a penalty and resets the combo.
        /// </summary>
        public int EmptyPressTolerance { get; set; } = 8;

        /// <summary>Score penalty for each empty press beyond the tolerance.</summary>
        public int EmptyPressPenalty { get; set; } = 100;

        /// <summary>Score for a missed tile in no-fail mode.</summary>
        public int FailMissScore { get; set; } = -100;

        /// <summary>Score for an overload fail.</summary>
        public int FailOverloadScore { get; set; } = -100;
    }

    public class AngleBand
    {
        public double MaxDeviationDeg { get; set; }
        public int Score { get; set; }

        public AngleBand() { }

        public AngleBand(double maxDeviationDeg, int score)
        {
            MaxDeviationDeg = maxDeviationDeg;
            Score = score;
        }
    }
}
