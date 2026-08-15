using System;
using System.Collections.Generic;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// JEA scoring engine.
    ///
    /// 赋分制 (point-allocation): every tile's raw angle deviation is mapped to a fixed
    /// score band, no BPM / ms conversion involved.
    ///
    /// 连锁 (combo): consecutive tiles scoring at or above <see cref="Settings.ComboThreshold"/>
    /// grow a combo that multiplies the tile score, so accuracy can exceed 100%.
    ///
    /// 空敲容错 (empty-press tolerance): mirrors the official <c>consecMultipressCounter &gt; 8</c>
    /// rule — the first N consecutive empty presses are forgiven, after which each one costs a
    /// penalty and resets the combo.
    /// </summary>
    public static class JeaScore
    {
        private static readonly List<long> _scoreLog = new();
        private static readonly List<int> _tileLog = new();
        private static readonly List<int> _comboLog = new();
        private static readonly List<int> _emptyLog = new();

        public static IReadOnlyList<long> ScoreLog => _scoreLog;
        public static IReadOnlyList<int> TileLog => _tileLog;
        public static IReadOnlyList<int> ComboLog => _comboLog;
        public static IReadOnlyList<int> EmptyLog => _emptyLog;

        public static long TotalScore { get; private set; }
        public static int Tiles { get; private set; }
        public static int Combo { get; private set; }
        public static int MaxCombo { get; private set; }
        public static int ConsecutiveEmptyPresses { get; private set; }
        public static int EmptyPresses { get; private set; }

        /// <summary>Angle deviation (degrees) of the current hit, set by the SwitchChosen patch.</summary>
        public static double CurrentDeviationDeg { get; set; }

        /// <summary>Track's set BPM for the current hit, set by the SwitchChosen patch.</summary>
        public static double CurrentBpm { get; set; } = 100;

        /// <summary>Fixed band score of the current hit, set by the SwitchChosen patch.</summary>
        public static int TileScore { get; set; }

        /// <summary>Final committed tile score (combo applied) of the last hit.</summary>
        public static long LastFinalTileScore { get; private set; }

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

        /// <summary>Look up the fixed score band for an absolute deviation in degrees.</summary>
        public static int BaseScore(double absDeviationDeg)
        {
            foreach (var band in Main.Settings.AngleBands)
            {
                if (absDeviationDeg <= band.MaxDeviationDeg)
                    return band.Score;
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
            var reference = Main.Settings.ReferenceBpm <= 0 ? 100 : Main.Settings.ReferenceBpm;
            var bpm = CurrentBpm <= 0 ? reference : CurrentBpm;
            return absDeviationDeg * reference / bpm;
        }

        private static double ComboMultiplier()
        {
            var m = 1.0 + Main.Settings.ComboStep * Combo;
            return Math.Min(m, Main.Settings.ComboMaxMultiplier);
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

        private static void SetLastOp(LastOp op, long finalTileScore)
        {
            LastOperation = op;
            LastFinalTileScore = finalTileScore;
        }

        /// <summary>Commit a scored tile (deviation already captured).</summary>
        public static void AddTile()
        {
            var s = Main.Settings;
            var baseScore = BaseScore(NormalizeDeviation(Math.Abs(CurrentDeviationDeg)));
            Combo = baseScore >= s.ComboThreshold ? Combo + 1 : 0;
            MaxCombo = Math.Max(MaxCombo, Combo);
            ConsecutiveEmptyPresses = 0;

            var tileScore = (long)Math.Round(baseScore * ComboMultiplier());
            SetLastOp(LastOp.Tile, tileScore);
            Commit(tileScore, tile: true);
        }

        /// <summary>Commit a failed tile (miss / overload).</summary>
        public static void AddFail(bool overload)
        {
            var s = Main.Settings;
            var tileScore = overload ? s.FailOverloadScore : s.FailMissScore;
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
            var s = Main.Settings;
            ConsecutiveEmptyPresses++;
            EmptyPresses++;
            if (ConsecutiveEmptyPresses <= s.EmptyPressTolerance)
            {
                SetLastOp(LastOp.EmptyPress, 0);
                Commit(0, tile: false);
                return;
            }
            Combo = 0;
            var penalty = -s.EmptyPressPenalty;
            SetLastOp(LastOp.EmptyPress, penalty);
            Commit(penalty, tile: false);
        }

        private static void Commit(long delta, bool tile)
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
