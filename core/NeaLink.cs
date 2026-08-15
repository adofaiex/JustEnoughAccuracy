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
