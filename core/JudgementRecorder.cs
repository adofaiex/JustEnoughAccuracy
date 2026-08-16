using System;
using System.Collections.Generic;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Captures one record per judged tile, in lock-step with
    /// <c>scrMarginTracker.hitMargins</c>. Also owns the optional NEA linkage
    /// (read via reflection so JEA runs without NEA installed).
    /// Only keeps the current run — cleared on reset and when leaving results.
    /// </summary>
    public static class JudgementRecorder
    {
        private static readonly List<JudgementRecord> Records = new();
        private static readonly object Sync = new();

        public static IReadOnlyList<JudgementRecord> Snapshot()
        {
            lock (Sync)
            {
                var result = Records.ToArray();
                Main.Handler?.Log($"[JEA][Recorder] Snapshot: {result.Length} records");
                return result;
            }
        }

        public static int Count
        {
            get { lock (Sync) return Records.Count; }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Main.Handler?.Log("[JEA][Recorder] Clear() called");
                Records.Clear();
            }
        }

        /// <summary>Roll back records to the tracker's hit count (checkpoint revert / death).</summary>
        public static void RevertTo(int hitCount)
        {
            lock (Sync)
            {
                if (hitCount < 0) hitCount = 0;
                while (Records.Count > hitCount)
                    Records.RemoveAt(Records.Count - 1);
            }
        }

        /// <summary>
        /// Append the record for the current hit. Reads the game's freshly-updated
        /// Acc/X-Acc and, when present, the NEA per-tile score for the same index.
        /// </summary>
        public static void Capture(
            int tile,
            double timestamp,
            double rawDevDeg,
            double normalizedDevDeg,
            double jeaTileScore,
            double jeaFinalTileScore,
            double jeaTotalScore,
            long jeaAccuracy,
            int combo,
            HitMargin margin,
            float acc,
            float xAcc,
            bool isEmptyPress)
        {
            var record = new JudgementRecord
            {
                Tile = tile,
                Timestamp = timestamp,
                RawDeviationDeg = rawDevDeg,
                NormalizedDeviationDeg = normalizedDevDeg,
                JeaTileScore = jeaTileScore,
                JeaFinalTileScore = jeaFinalTileScore,
                JeaTotalScore = jeaTotalScore,
                JeaAccuracy = jeaAccuracy,
                Combo = combo,
                Margin = margin,
                Acc = acc,
                XAcc = xAcc,
                OfficialScore = OfficialScoreFor(margin),
                IsEmptyPress = isEmptyPress
            };

            lock (Sync)
            {
                record.NeaScore = NeaLink.ReadScoreAt(Records.Count);
                record.NeaAcc = NeaLink.ReadAccAt(Records.Count);
                Records.Add(record);
            }
        }

        /// <summary>
        /// Fixed score for a judgement, mirroring the official X-Acc weights.
        /// Pure Perfect = 100, EPerfect/LPerfect = 75, VeryEarly/VeryLate = 40,
        /// TooEarly/TooLate = 20, fails = 0.
        /// </summary>
        public static int? OfficialScoreFor(HitMargin margin)
        {
            return margin switch
            {
                HitMargin.Perfect => 100,
                HitMargin.Auto => 100,
                HitMargin.EarlyPerfect => 75,
                HitMargin.LatePerfect => 75,
                HitMargin.VeryEarly => 40,
                HitMargin.VeryLate => 40,
                HitMargin.TooEarly => 20,
                HitMargin.TooLate => 20,
                _ => null
            };
        }
    }
}
