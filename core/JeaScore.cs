using System;
using System.Collections.Generic;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// JEA scoring engine.
    ///
    /// 赋分制 (point-allocation): the tile's raw angle deviation is normalized to
    /// a reference BPM and graded on a FIXED band table — the score interpolates
    /// linearly inside each band, so every point value (95, 97, 98.4 …) is
    /// reachable instead of only the band anchors.
    ///
    /// The band table and all penalties are constants by design: JEA is meant to
    /// be one fixed judgement, not a configurable one.
    ///
    /// 连锁 (combo): consecutive tiles scoring at or above <see cref="ComboThreshold"/>
    /// grow a combo that is tracked for display only — it does NOT multiply tile
    /// scores, so accuracy is capped at 100%.
    ///
    /// 空敲容错 (empty-press tolerance): mirrors the official <c>consecMultipressCounter &gt; 8</c>
    /// rule — the first N consecutive empty presses are forgiven, after which each one
    /// costs a penalty and resets the combo.
    /// </summary>
    public static class JeaScore
    {
        // ── fixed scoring model ────────────────────────────────────────────
        //
        // Bands are in normalized degrees (see NormalizeDeviation). Score is
        // linearly interpolated between neighbouring anchors:
        //   1.7°→100 (full-score window), 2.0°→96, … 8.0°→15, >8°→0.
        // Anchor mapping: full score = 1.7° normalized (≈ ±43.5° at 2560 BPM,
        // an 87°-wide full-score window), zero line = 8° (≈ 205° at 2560 BPM,
        // past the official TooEarly boundary — a hit always scores > 0).
        private static readonly (double Deg, int Score)[] Bands =
        {
            (1.7, 100),
            (2.0, 96),
            (2.4, 92),
            (2.8, 88),
            (3.2, 84),
            (3.6, 80),
            (4.0, 75),
            (4.5, 70),
            (5.0, 62),
            (5.5, 54),
            (6.0, 46),
            (6.6, 36),
            (7.2, 26),
            (8.0, 15),
        };

        private const double ReferenceBpmValue = 100;
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
        /// Interpolated score for an absolute (normalized) deviation in degrees:
        /// 100 inside the full-score window, then linear between band anchors,
        /// 0 beyond the last one.
        /// </summary>
        public static double BaseScore(double absDeviationDeg)
        {
            if (absDeviationDeg <= Bands[0].Deg)
                return Bands[0].Score;
            for (var i = 1; i < Bands.Length; i++)
            {
                if (absDeviationDeg <= Bands[i].Deg)
                {
                    var (d0, s0) = Bands[i - 1];
                    var (d1, s1) = Bands[i];
                    var t = (absDeviationDeg - d0) / (d1 - d0);
                    return s0 + (s1 - s0) * t;
                }
            }
            return 0;
        }

        /// <summary>
        /// Normalize a raw degree deviation to the reference BPM, so a constant timing
        /// error in milliseconds grades the same on any chart (the angle equivalent of a
        /// time window grows linearly with BPM, as in the official engine).
        /// </summary>
        public static double NormalizeDeviation(double absDeviationDeg)
        {
            var bpm = CurrentBpm <= 0 ? ReferenceBpmValue : CurrentBpm;
            return absDeviationDeg * ReferenceBpmValue / bpm;
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
            var baseScore = BaseScore(NormalizeDeviation(Math.Abs(CurrentDeviationDeg)));
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
