using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Optional bridge to Not Enough Accuracy's <c>NotEnoughAccuracy.Api</c>.
    /// Resolved lazily via reflection so JEA works with or without NEA installed.
    /// </summary>
    public static class NeaLink
    {
        private static Type? _apiType;
        private static PropertyInfo? _judgementsProperty;
        private static PropertyInfo? _scoresProperty;
        private static PropertyInfo? _tilesProperty;
        private static bool _resolved;
        private static bool _available;

        public static bool Available
        {
            get
            {
                TryResolve();
                return _available;
            }
        }

        private static void TryResolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "NotEnoughAccuracy")
                        continue;
                    _apiType = assembly.GetType("NotEnoughAccuracy.Api");
                    if (_apiType != null)
                    {
                        _judgementsProperty = _apiType.GetProperty("Judgements", BindingFlags.Public | BindingFlags.Static);
                        _scoresProperty = _apiType.GetProperty("Scores", BindingFlags.Public | BindingFlags.Static);
                        _tilesProperty = _apiType.GetProperty("Tiles", BindingFlags.Public | BindingFlags.Static);
                        _available = _judgementsProperty != null;
                    }
                    return;
                }
            }
            catch
            {
                _available = false;
            }
        }

        /// <summary>
        /// NEA running accuracy in percent (0..100 scale; NEA's accumulated
        /// score over accumulated tiles) at the given index, or null when NEA
        /// is absent or the index is out of range.
        /// </summary>
        public static double? ReadAccAt(int index)
        {
            if (!Available || _scoresProperty == null || _tilesProperty == null) return null;
            try
            {
                if (_scoresProperty.GetValue(null, null) is not IList scores ||
                    _tilesProperty.GetValue(null, null) is not IList tiles)
                    return null;
                if (index < 0 || index >= scores.Count || index >= tiles.Count) return null;
                var score = Convert.ToInt64(scores[index]);
                var tileCount = Convert.ToInt64(tiles[index]);
                if (tileCount <= 0) return null;
                // each NEA judgement is 0..100, so score/tiles is a percent
                return (double)score / tileCount;
            }
            catch
            {
                // ignore — NEA data may be transient
            }
            return null;
        }

        /// <summary>
        /// NEA per-tile score for the record at the given index, or null when NEA
        /// is absent or the index is out of range (NEA started later, etc.).
        /// </summary>
        public static long? ReadScoreAt(int index)
        {
            if (!Available || _judgementsProperty == null) return null;
            try
            {
                var value = _judgementsProperty.GetValue(null, null);
                if (value is IList list && index >= 0 && index < list.Count)
                {
                    var item = list[index];
                    return Convert.ToInt64(item);
                }
                if (value is IEnumerable enumerable)
                {
                    var current = 0;
                    foreach (var item in enumerable)
                    {
                        if (current == index)
                            return Convert.ToInt64(item);
                        current++;
                    }
                }
            }
            catch
            {
                // ignore — NEA data may be transient
            }
            return null;
        }
    }
}
