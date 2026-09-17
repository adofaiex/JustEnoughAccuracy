using System;
using System.Reflection;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Cross-version shim for the official margin boundary API.
    ///
    /// 3.3.x: <c>double GetAdjustedAngleBoundaryInDeg(HitMarginGeneral, double, double, double)</c>
    /// 3.4.0: <c>HitMarginGeneralWithXPerfectValuesStruct&lt;double&gt; GetAdjustedAngleBoundaryInDeg(Difficulty, double, double, double)</c>
    ///
    /// The mod only needs the Perfect and Counted boundaries. The method is
    /// invoked through reflection so the same build works on both versions; if
    /// the lookup ever fails we fall back to the official floors (45° / 60°).
    /// </summary>
    internal static class BoundaryCompat
    {
        private static MethodInfo? _method;
        private static bool _initialized;
        private static bool _usesDifficulty;

        public static void GetOfficialBoundaries(
            double bpmTimesSpeed, double pitch, out double perfectDeg, out double countedDeg)
        {
            perfectDeg = 45.0;
            countedDeg = GCS.HITMARGIN_COUNTED;
            try
            {
                EnsureInitialized();
                if (_method == null) return;

                var args = new object[4];
                args[1] = bpmTimesSpeed;
                args[2] = pitch;
                args[3] = 1.0;

                if (_usesDifficulty)
                {
                    args[0] = GCS.difficulty;
                    var result = _method.Invoke(null, args);
                    if (result == null) return;
                    var type = result.GetType();
                    perfectDeg = ReadDoubleField(type, result, "Perfect", perfectDeg);
                    countedDeg = ReadDoubleField(type, result, "Counted", countedDeg);
                }
                else
                {
                    args[0] = Enum.Parse(_method.GetParameters()[0].ParameterType, "Perfect");
                    perfectDeg = Convert.ToDouble(_method.Invoke(null, args));
                    args[0] = Enum.Parse(_method.GetParameters()[0].ParameterType, "Counted");
                    countedDeg = Convert.ToDouble(_method.Invoke(null, args));
                }
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA][Compat] boundary lookup failed: {ex.Message}");
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            foreach (var m in typeof(scrMisc).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "GetAdjustedAngleBoundaryInDeg") continue;
                var ps = m.GetParameters();
                if (ps.Length < 3) continue;
                _usesDifficulty = ps[0].ParameterType == typeof(Difficulty);
                _method = m;
                return;
            }
        }

        private static double ReadDoubleField(Type type, object instance, string name, double fallback)
        {
            var field = type.GetField(name);
            if (field == null) return fallback;
            var value = field.GetValue(instance);
            return value == null ? fallback : Convert.ToDouble(value);
        }
    }
}
