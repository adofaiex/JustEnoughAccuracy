using System;
using System.Collections.Generic;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// JEA scoring engine.
    ///
    /// 角度赋分制 (angular point-allocation) — JEA's own identity: the score is
    /// a function of the ANGULAR deviation (∠), not of time (ms) like NEA. The
    /// curve is anchored on the official margin boundaries:
    ///
    ///  - FULL-SCORE window: 4° of angle (widening with the effective rate
    ///    BPM × speed × pitch), inside which the tile scores 100.
    ///  - From the window edge out to the OFFICIAL Perfect boundary
    ///    (max(45°, TimeToAngle(0.03)) — includes the 45° floor), the score
    ///    maps 99 → 90.
    ///  - From the Perfect boundary out to the OFFICIAL Counted boundary
    ///    (max(HITMARGIN_COUNTED, TimeToAngle(0.065))), the score maps 80 → 0.
    ///  - Beyond Counted the hit is a miss anyway (Fail paths handle it).
    ///
    /// Every valid judgement scores — no banding cut-offs and no per-millisecond
    /// penalty, which is what separates JEA from NEA.
    ///
    /// 连锁 (combo): consecutive tiles scoring at or above <see cref="ComboThreshold"/>
    ///  grow a combo that is tracked for display only — it does NOT multiply tile
    ///  scores, so accuracy is capped at 100%.
    ///
    /// 空敲容错 (empty-press tolerance): mirrors the official <c>consecMultipressCounter &gt; 8</c>
    ///  rule — the first N consecutive empty presses are forgiven, after which each one
    ///  costs a penalty and resets the combo.
    /// </summary>
    public static class JeaScore
    {
        // ── scoring model ─────────────────────────────────────────────────
        //
        // Full-score window (degrees): max(4°, rate × 0.04) capped at 20°.
        //   4°  @ 100 BPM ≡ 6.67 ms of time tolerance.
        //   8°  @ 200 BPM (same 6.67 ms — window widens with BPM).
        //   20° cap @ 500+ BPM so the window never goes absurd.
        // The official boundary anchors (Perfect / Counted) are computed per hit
        // via scrMisc.GetAdjustedAngleBoundaryInDeg at the effective rate, so
        // they follow the real margins (including the 45° / 60° floors).
        private const double ReferenceBpmValue = 100;
        private const double FullWindowFloorDeg = 4.0;
        private const double FullWindowDegPerBpm = 0.04;
        private const double FullWindowCapDeg = 20.0;

        private const int ComboThreshold = 50;
        private const int EmptyPressTolerance = 8;
        private const int EmptyPressPenaltyScore = 100;
        private const int FailMissScoreValue = -100;
        private const int FailOverloadScoreValue = -100;

        private static readonly List<double> _scoreLog = new();
        private static readonly List<int> _tileLog = new();
        private static readonly List<int> _comboLog = new();
        private static readonly List<int> _emptyLog = new();

        public static IReadOnlyList<double> ScoreLog => _scoreLog;
        public static IReadOnlyList<int> TileLog => _tileLog;
        public static IReadOnlyList<int> ComboLog => _comboLog;
        public static IReadOnlyList<int> EmptyLog => _emptyLog;

        public static double TotalScore { get; private set; }
        public static int Tiles { get; private set; }
        public static int Combo { get; private set; }
        public static int MaxCombo { get; private set; }
        public static int ConsecutiveEmptyPresses { get; private set; }
        public static int EmptyPresses { get; private set; }

        /// <summary>Angle deviation (degrees) of the current hit, set by the SwitchChosen patch.</summary>
        public static double CurrentDeviationDeg { get; set; }

        /// <summary>Track's set BPM for the current hit, set by the SwitchChosen patch.</summary>
        public static double CurrentBpm { get; set; } = 100;

        /// <summary>
        /// Official Perfect boundary in degrees at the current effective rate
        /// (includes the 45° floor). Set by the SwitchChosen patch.
        /// </summary>
        public static double PerfectBoundaryDeg { get; set; } = 45.0;

        /// <summary>
        /// Official Counted boundary in degrees at the current effective rate
        /// (includes the HITMARGIN_COUNTED floor). Set by the SwitchChosen patch.
        /// </summary>
        public static double CountedBoundaryDeg { get; set; } = 60.0;

        /// <summary>
        /// Signed deviation of the current hit in degrees (positive = late,
        /// negative = early), set by the SwitchChosen patch. The absolute value
        /// is what scoring uses; the sign is preserved for display.
        /// </summary>
        public static double CurrentDeviationSignedDeg { get; set; }

        /// <summary>Interpolated band score of the current hit, set by the SwitchChosen patch.</summary>
        public static double TileScore { get; set; }

        /// <summary>Final committed tile score (combo applied) of the last hit.</summary>
        public static double LastFinalTileScore { get; private set; }

        /// <summary>Last committed operation kind, used by the recorder.</summary>
        public static LastOp LastOperation { get; private set; }

        /// <summary>Scaled accuracy in hundred-thousandths (1_000_000 == 100%).</summary>
        public static long CachedAccuracy { get; private set; }

        public static void Reset()
        {
            _scoreLog.Clear();
            _tileLog.Clear();
            _comboLog.Clear();
            _emptyLog.Clear();
            TotalScore = 0;
            Tiles = 0;
            Combo = 0;
            MaxCombo = 0;
            ConsecutiveEmptyPresses = 0;
            EmptyPresses = 0;
            CachedAccuracy = 0;
        }

        /// <summary>
        /// Full-score window in degrees at the current effective rate:
        /// 4° floor, widening with BPM, capped at <see cref="FullWindowCapDeg"/>.
        /// </summary>
        private static double FullWindowDeg()
        {
            var bpm = CurrentBpm <= 0 ? ReferenceBpmValue : CurrentBpm;
            return Math.Min(
                Math.Max(FullWindowFloorDeg, FullWindowDegPerBpm * bpm),
                FullWindowCapDeg);
        }

        /// <summary>
        /// Score for a raw angular deviation (degrees), anchored on the official
        /// margin boundaries:
        ///   ≤ full-score window            → 100
        ///   window → official Perfect      → 99…90  (linear)
        ///   official Perfect → Counted     → 80…0   (linear)
        ///   beyond Counted                 → 0 (a miss; Fail paths own those)
        /// The score is a pure function of ANGLE, not milliseconds — that is
        /// what separates JEA from NEA. No banding snap, no per-ms penalty.
        /// </summary>
        public static double BaseScore(double absDeviationDeg)
        {
            var window = FullWindowDeg();
            if (absDeviationDeg <= window) return 100.0;

            var perfect = PerfectBoundaryDeg > window ? PerfectBoundaryDeg : window;
            if (absDeviationDeg <= perfect)
            {
                // 99 at the window edge → 90 at the official Perfect boundary.
                var t = (absDeviationDeg - window) / (perfect - window);
                return 99.0 - 9.0 * t;
            }

            var counted = CountedBoundaryDeg > perfect ? CountedBoundaryDeg : perfect;
            if (absDeviationDeg <= counted)
            {
                // 80 at Perfect → 0 at the official Counted boundary.
                var t = (absDeviationDeg - perfect) / (counted - perfect);
                var score = 80.0 - 80.0 * t;
                return score < 0.0 ? 0.0 : score;
            }

            return 0.0;
        }

        /// <summary>
        /// Convert a raw angular deviation to a time error in milliseconds at
        /// the current effective rate (360° per crotchet = 60/bpm s).
        /// Used for display/export only — scoring itself is angular.
        /// </summary>
        private static double DegToMs(double absDeviationDeg)
        {
            var bpm = CurrentBpm <= 0 ? ReferenceBpmValue : CurrentBpm;
            return absDeviationDeg * 1000.0 / (bpm * 6.0);
        }

        /// <summary>
        /// Signed angular deviation converted to an equivalent signed time error
        /// in milliseconds at the current effective rate (positive = late,
        /// negative = early). Used for display; scoring itself uses
        /// <see cref="BaseScore"/> on the absolute deviation.
        /// </summary>
        public static double NormalizeDeviation(double signedDeviationDeg)
        {
            var sign = signedDeviationDeg < 0.0 ? -1.0 : 1.0;
            return DegToMs(Math.Abs(signedDeviationDeg)) * sign;
        }

        /// <summary>What the last committed operation was, for the judgement recorder.</summary>
        public enum LastOp
        {
            None,
            Tile,
            Fail,
            EmptyPress,
            Noop
        }

        private static void SetLastOp(LastOp op, double finalTileScore)
        {
            LastOperation = op;
            LastFinalTileScore = finalTileScore;
        }

        /// <summary>Commit a scored tile (deviation already captured).</summary>
        public static void AddTile()
        {
            var baseScore = BaseScore(Math.Abs(CurrentDeviationDeg));
            Combo = baseScore >= ComboThreshold ? Combo + 1 : 0;
            MaxCombo = Math.Max(MaxCombo, Combo);
            ConsecutiveEmptyPresses = 0;

            SetLastOp(LastOp.Tile, baseScore);
            Commit(baseScore, tile: true);
        }

        /// <summary>Commit a failed tile (miss / overload).</summary>
        public static void AddFail(bool overload)
        {
            var tileScore = overload ? (double)FailOverloadScoreValue : FailMissScoreValue;
            Combo = 0;
            ConsecutiveEmptyPresses = 0;
            SetLastOp(LastOp.Fail, tileScore);
            Commit(tileScore, tile: true);
        }

        /// <summary>
        /// Commit an empty press (multipress / overpress / too-early mashing).
        /// Forgiven while consecutive empty presses stay within the tolerance;
        /// beyond it each one costs a penalty and breaks the combo.
        /// </summary>
        public static void AddEmptyPress()
        {
            ConsecutiveEmptyPresses++;
            EmptyPresses++;
            if (ConsecutiveEmptyPresses <= EmptyPressTolerance)
            {
                SetLastOp(LastOp.EmptyPress, 0);
                Commit(0, tile: false);
                return;
            }
            Combo = 0;
            var penalty = -(double)EmptyPressPenaltyScore;
            SetLastOp(LastOp.EmptyPress, penalty);
            Commit(penalty, tile: false);
        }

        private static void Commit(double delta, bool tile)
        {
            if (tile)
            {
                Tiles++;
                _tileLog.Add(Tiles);
            }
            else
            {
                _tileLog.Add(Tiles);
            }
            TotalScore += delta;
            _scoreLog.Add(TotalScore);
            _comboLog.Add(Combo);
            _emptyLog.Add(ConsecutiveEmptyPresses);
            CacheAccuracy();
        }

        /// <summary>
        /// Append a no-op entry (midspin / auto tiles) so the internal logs stay in
        /// lock-step with <c>scrMarginTracker.hitMargins</c> for checkpoint rollbacks.
        /// </summary>
        public static void AddNoop()
        {
            SetLastOp(LastOp.Noop, 0);
            Commit(0, tile: false);
        }

        private static void CacheAccuracy()
        {
            CachedAccuracy = Tiles == 0
                ? 0
                : (long)Math.Round(TotalScore * 1_000_000.0 / (Tiles * 100.0));
        }

        /// <summary>
        /// Accuracy colour for the results screen: green→gold as the accuracy rises.
        /// Acc is in hundred-thousandths (1_000_000 == 100%); callers pass it as-is.
        /// </summary>
        public static string AccuracyColorHex(long acc)
        {
            var pct = acc / 10000.0;
            if (pct >= 100.0) return "#FFDA00";   // gold — pure perfect
            if (pct >= 99.0) return "#7CE0B3";    // green
            if (pct >= 96.0) return "#A8D8A0";    // light green
            if (pct >= 92.0) return "#F3D98B";    // yellow
            if (pct >= 85.0) return "#E08A7C";    // orange
            return "#FF6B6B";                     // red
        }
        /// <summary>
        /// Fired after the score state is refreshed (see <see cref="Cache"/>).
        /// Other mods can subscribe to react to JEA updates without referencing Unity.
        /// </summary>
        public static event Action? Updated;

        /// <summary>Refresh derived values and notify listeners (results screen / other mods).</summary>
        public static void Cache()
        {
            CacheAccuracy();
            Updated?.Invoke();
        }

        /// <summary>Roll back to the tracker's current hit count (checkpoint revert / death).</summary>
        public static void RevertTo(int hitCount)
        {
            if (hitCount < 0) hitCount = 0;
            while (_scoreLog.Count > hitCount)
            {
                _scoreLog.RemoveAt(_scoreLog.Count - 1);
                _tileLog.RemoveAt(_tileLog.Count - 1);
                _comboLog.RemoveAt(_comboLog.Count - 1);
                _emptyLog.RemoveAt(_emptyLog.Count - 1);
            }
            TotalScore = _scoreLog.Count == 0 ? 0 : _scoreLog[^1];
            Tiles = _tileLog.Count == 0 ? 0 : _tileLog[^1];
            Combo = _comboLog.Count == 0 ? 0 : _comboLog[^1];
            ConsecutiveEmptyPresses = _emptyLog.Count == 0 ? 0 : _emptyLog[^1];
            MaxCombo = 0;
            foreach (var c in _comboLog)
                MaxCombo = Math.Max(MaxCombo, c);
            CacheAccuracy();
        }
    }
}
