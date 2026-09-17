using System;
using System.Collections.Generic;
using System.Reflection;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Cross-version compatibility for the game's judgement APIs.
    ///
    /// ADOFAI 3.4.0 renamed and reordered the <c>HitMargin</c> enum
    /// (<c>Perfect</c> → <c>PerfectMinus</c>/<c>XPerfect</c>/<c>PerfectPlus</c>,
    /// plus new <c>Midspin</c>/<c>FailedFloor</c> members) and re-typed
    /// <c>scrMisc.GetAdjustedAngleBoundaryInDeg</c>. Enum member references are
    /// compile-time constants, so a mod built against one version would read the
    /// wrong margins on the other. JEA therefore resolves members by NAME at
    /// startup and never references a version-specific member directly, so the
    /// same build runs on both 3.3.x and 3.4.x.
    /// </summary>
    internal static class HitMarginCompat
    {
        private static readonly Dictionary<string, HitMargin> ByName = new();

        public static readonly bool IsAtLeast340;

        public static readonly HitMargin Perfect;       // 3.3.x only
        public static readonly HitMargin PerfectMinus;  // 3.4.0+
        public static readonly HitMargin XPerfect;      // 3.4.0+
        public static readonly HitMargin PerfectPlus;   // 3.4.0+
        public static readonly HitMargin Auto;
        public static readonly HitMargin EarlyPerfect;
        public static readonly HitMargin LatePerfect;
        public static readonly HitMargin VeryEarly;
        public static readonly HitMargin VeryLate;
        public static readonly HitMargin TooEarly;
        public static readonly HitMargin TooLate;
        public static readonly HitMargin Multipress;
        public static readonly HitMargin OverPress;
        public static readonly HitMargin FailMiss;
        public static readonly HitMargin FailOverload;
        public static readonly HitMargin Midspin;       // 3.4.0+
        public static readonly HitMargin FailedFloor;   // 3.4.0+

        static HitMarginCompat()
        {
            foreach (HitMargin m in Enum.GetValues(typeof(HitMargin)))
                ByName[m.ToString()] = m;

            IsAtLeast340 = ByName.ContainsKey("XPerfect");

            Perfect = Get("Perfect");
            PerfectMinus = Get("PerfectMinus");
            XPerfect = Get("XPerfect");
            PerfectPlus = Get("PerfectPlus");
            Auto = Get("Auto");
            EarlyPerfect = Get("EarlyPerfect");
            LatePerfect = Get("LatePerfect");
            VeryEarly = Get("VeryEarly");
            VeryLate = Get("VeryLate");
            TooEarly = Get("TooEarly");
            TooLate = Get("TooLate");
            Multipress = Get("Multipress");
            OverPress = Get("OverPress");
            FailMiss = Get("FailMiss");
            FailOverload = Get("FailOverload");
            Midspin = Get("Midspin");
            FailedFloor = Get("FailedFloor");
        }

        private static HitMargin Get(string name)
            => ByName.TryGetValue(name, out var m) ? m : (HitMargin)(-1);

        /// <summary>Pure-perfect family: old <c>Perfect</c> or the 3.4.0 trio.</summary>
        public static bool IsPerfect(HitMargin m)
            => m == Perfect || m == PerfectMinus || m == XPerfect || m == PerfectPlus;

        public static bool IsAuto(HitMargin m) => m == Auto;
        public static bool IsSemiPerfect(HitMargin m) => m == EarlyPerfect || m == LatePerfect;
        public static bool IsVery(HitMargin m) => m == VeryEarly || m == VeryLate;
        public static bool IsToo(HitMargin m) => m == TooEarly || m == TooLate;
        public static bool IsTooEarly(HitMargin m) => m == TooEarly;
        public static bool IsEmptyPress(HitMargin m) => m == Multipress || m == OverPress;
        public static bool IsFail(HitMargin m) => m == FailMiss || m == FailOverload;
        public static bool IsFailOverload(HitMargin m) => m == FailOverload;
        public static bool IsNoop(HitMargin m) => m == Auto || m == Midspin || m == FailedFloor;

        /// <summary>Official-style per-judgement score (mirrors the X-Acc weights).</summary>
        public static int? OfficialScore(HitMargin m)
        {
            if (IsPerfect(m) || m == Auto) return 100;
            if (IsSemiPerfect(m)) return 75;
            if (IsVery(m)) return 40;
            if (IsToo(m)) return 20;
            return null;
        }

        /// <summary>Previewer row colour for a margin (stable across game versions).</summary>
        public static string ColorHex(HitMargin m)
        {
            if (IsPerfect(m) || m == Auto) return "#FFDA00";
            if (IsSemiPerfect(m)) return "#7CE0B3";
            if (IsVery(m)) return "#F3D98B";
            if (IsToo(m)) return "#E08A7C";
            if (IsFail(m)) return "#FF6B6B";
            if (IsEmptyPress(m)) return "#FF8C5A";
            return "#FFFFFF";
        }

        /// <summary>
        /// A stable display name for cross-version exports: the 3.4.0 perfect
        /// trio collapses to "Perfect", other members keep their own name.
        /// </summary>
        public static string DisplayName(HitMargin m)
        {
            if (m == PerfectMinus || m == XPerfect || m == PerfectPlus) return "Perfect";
            return m.ToString();
        }
    }
}
