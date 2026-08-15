using System;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// One captured tile judgement for the previewer / report.
    /// All fields are captured at the moment the tile is judged (AddHit).
    /// </summary>
    public sealed class JudgementRecord
    {
        /// <summary>1-based tile number shown to players.</summary>
        public int Tile { get; set; }

        /// <summary>Chart time (seconds) at the moment the key was judged.</summary>
        public double Timestamp { get; set; }

        /// <summary>Raw angular deviation in degrees.</summary>
        public double RawDeviationDeg { get; set; }

        /// <summary>Deviation normalized to the reference BPM (degrees at ReferenceBpm).</summary>
        public double NormalizedDeviationDeg { get; set; }

        /// <summary>JEA band score for this tile.</summary>
        public int JeaTileScore { get; set; }

        /// <summary>JEA committed tile score (same as the band score, no multiplier).</summary>
        public long JeaFinalTileScore { get; set; }

        /// <summary>JEA cumulative total score up to and including this tile.</summary>
        public long JeaTotalScore { get; set; }

        /// <summary>JEA accuracy (hundred-thousandths, 1_000_000 == 100%) up to this tile.</summary>
        public long JeaAccuracy { get; set; }

        /// <summary>Combo count at this tile.</summary>
        public int Combo { get; set; }

        /// <summary>Original game judgement for this key press.</summary>
        public HitMargin Margin { get; set; }

        /// <summary>Original game Acc% (0..1) after this tile.</summary>
        public float Acc { get; set; }

        /// <summary>Original game X-Acc% (0..1) after this tile.</summary>
        public float XAcc { get; set; }

        /// <summary>Official judgement mapped to a fixed score (null when judgement has none).</summary>
        public int? OfficialScore { get; set; }

        /// <summary>NEA per-tile score (100 - |ms|), null when NEA is not present.</summary>
        public long? NeaScore { get; set; }

        /// <summary>Whether this tile was an empty press (multipress / overpress / too-early).</summary>
        public bool IsEmptyPress { get; set; }
    }
}
