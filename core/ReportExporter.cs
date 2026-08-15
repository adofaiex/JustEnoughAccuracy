using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Exports the current run's judgements as a UTF-8 YAML report.
    /// </summary>
    public static class ReportExporter
    {
        public const string FileName = "JEA_report.yaml";

        public static string Build(string levelName)
        {
            var sb = new StringBuilder(4096);
            sb.Append("Timestamp: ")
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                .AppendLine();
            sb.Append("Level: ").AppendLine(Escape(levelName));
            sb.AppendLine();
            sb.AppendLine("Judgements:");

            foreach (var r in JudgementRecorder.Snapshot())
            {
                sb.AppendLine("  - Tile: " + r.Tile.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    Timestamp: " + FormatTimestamp(r.Timestamp));
                sb.AppendLine("    RawDeviationDeg: " + FormatNum(r.RawDeviationDeg));
                sb.AppendLine("    NormalizedDeviationDeg: " + FormatNum(r.NormalizedDeviationDeg));
                sb.AppendLine("    Margin: " + r.Margin);
                sb.AppendLine("    Acc: " + FormatPct(r.Acc));
                sb.AppendLine("    XAcc: " + FormatPct(r.XAcc));
                sb.AppendLine("    OfficialScore: " + (r.OfficialScore?.ToString(CultureInfo.InvariantCulture) ?? "null"));
                sb.AppendLine("    JeaTileScore: " + r.JeaTileScore.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaFinalTileScore: " + r.JeaFinalTileScore.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaTotalScore: " + r.JeaTotalScore.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    JeaAccuracy: " + FormatAccuracy(r.JeaAccuracy));
                sb.AppendLine("    Combo: " + r.Combo.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("    NeaScore: " + (r.NeaScore?.ToString(CultureInfo.InvariantCulture) ?? "null"));
            }

            return sb.ToString();
        }

        /// <summary>Write the report to the mod's own folder (UTF-8, no BOM).</summary>
        public static string WriteReport(string levelName, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var path = Path.Combine(outputDirectory, FileName);
            File.WriteAllText(path, Build(levelName), new UTF8Encoding(false));
            return path;
        }

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string FormatTimestamp(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
        }

        /// <summary>Format chart seconds as mm:ss.mmm (shared with the previewer UI).</summary>
        public static string FormatTimestampPublic(double seconds)
        {
            return FormatTimestamp(seconds);
        }

        private static string FormatNum(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatPct(float value)
        {
            return (value * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatAccuracy(long acc)
        {
            return (acc / 10000m).ToString("0.0000", CultureInfo.InvariantCulture) + "%";
        }
    }
}
