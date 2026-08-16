using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// UI localization driven by <c>Resources/i18n/*.yaml</c>. Each file declares
    /// itself in a <c>_lang</c> header (code + which Unity SystemLanguages it
    /// covers), so dropping a new yaml into the folder is all it takes to add a
    /// language — no code changes. Falls back: current lang → en → the key
    /// itself. Keys are flattened ('.' joins); values may hold {0} placeholders.
    /// </summary>
    public static class JeI18n
    {
        private static readonly Dictionary<string, string> Fallback = new();
        private static readonly Dictionary<string, string> Entries = new();
        private static Dictionary<SystemLanguage, string> _langMap = new();
        private static readonly Dictionary<string, string> FilesByCode = new();
        private static readonly Dictionary<string, string> DisplayNames = new();
        private static string? _loadedLang;
        private static bool _scanned;
        private static bool _loggedFailure;

        /// <summary>Current UI language code (e.g. "zh", "en"); "en" if unmapped.</summary>
        public static string LangCode()
        {
            EnsureScanned();

            var override_ = Main.Settings?.Language;
            if (!string.IsNullOrEmpty(override_) && FilesByCode.ContainsKey(override_!))
                return override_!;

            try
            {
                return _langMap.TryGetValue(Persistence.language, out var code) ? code : "en";
            }
            catch
            {
                return "en";
            }
        }

        /// <summary>Translate a key; falls back to en, then to the key itself.</summary>
        public static string Get(string key)
        {
            EnsureLoaded();
            if (Entries.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            if (Fallback.TryGetValue(key, out var fb) && !string.IsNullOrEmpty(fb))
                return fb;
            return key;
        }

        /// <summary>Translate with string.Format placeholders, e.g. GetF("k", arg0, arg1).</summary>
        public static string GetF(string key, params object[] args)
        {
            try
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key), args);
            }
            catch (FormatException)
            {
                return Get(key);
            }
        }

        /// <summary>Returns all available language codes, with "en" first.</summary>
        public static string[] AvailableLanguages()
        {
            EnsureScanned();
            var result = new List<string> { "en" };
            foreach (var kv in FilesByCode)
                if (kv.Key != "en" && !result.Contains(kv.Key))
                    result.Add(kv.Key);
            return result.ToArray();
        }

        /// <summary>Returns a display name for a language code (for UI selectors).</summary>
        public static string DisplayName(string code)
        {
            EnsureScanned();
            return DisplayNames.TryGetValue(code, out var name) ? name : code.ToUpperInvariant();
        }

        /// <summary>Forces a reload on next Get() call. Call after changing language.</summary>
        public static void ForceReload()
        {
            _loadedLang = null;
        }

        // ---------- discovery ----------

        private static void EnsureScanned()
        {
            if (_scanned) return;
            _scanned = true;

            var dir = ResolveI18nDir();
            if (dir == null)
            {
                WarnOnce($"[JEA][I18n] i18n directory not found at {ExpectedPaths()}");
                return;
            }

            var map = new Dictionary<SystemLanguage, string>();
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.yaml");
            }
            catch (Exception ex)
            {
                WarnOnce($"[JEA][I18n] failed to list i18n files: {ex.Message}");
                return;
            }

            foreach (var path in files)
            {
                var doc = ParseFile(path);
                if (doc == null) continue;

                var code = Path.GetFileNameWithoutExtension(path);
                if (doc.TryGetValue("_lang", out var meta) && meta is Dictionary<string, object?> m)
                {
                    if (m.TryGetValue("code", out var c) && c is string s && !string.IsNullOrEmpty(s))
                        code = s;
                    if (m.TryGetValue("name", out var n0) && n0 is string disp && !string.IsNullOrEmpty(disp))
                        DisplayNames[code] = disp;
                    if (m.TryGetValue("systemLanguages", out var names) && names is List<object?> list)
                    {
                        foreach (var name in list)
                        {
                            if (name is string n &&
                                Enum.TryParse<SystemLanguage>(n.Trim(), true, out var sl))
                            {
                                map[sl] = code;
                            }
                        }
                    }
                }
                FilesByCode[code] = path;
            }

            _langMap = map;
            Main.Handler?.Log($"[JEA][I18n] scanned {files.Length} file(s), {map.Count} language mapping(s)");
        }

        // ---------- loading ----------

        private static void EnsureLoaded()
        {
            var lang = LangCode();
            if (_loadedLang == lang)
                return;
            _loadedLang = lang;

            if (Fallback.Count == 0)
                LoadInto("en", Fallback);

            Entries.Clear();
            if (lang != "en")
                LoadInto(lang, Entries);
        }

        private static void LoadInto(string code, Dictionary<string, string> dst)
        {
            if (!FilesByCode.TryGetValue(code, out var path))
            {
                WarnOnce($"[JEA][I18n] no file for language '{code}', falling back");
                return;
            }
            var doc = ParseFile(path);
            if (doc == null) return;
            Flatten(doc, "", dst);
        }

        private static Dictionary<string, object?>? ParseFile(string path)
        {
            try
            {
                return new MiniYamlParser(File.ReadAllText(path, Encoding.UTF8)).ParseObjectDocument();
            }
            catch (Exception ex)
            {
                Main.Handler?.Warning($"[JEA][I18n] failed to parse {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        private static void Flatten(Dictionary<string, object?> src, string prefix, Dictionary<string, string> dst)
        {
            foreach (var kv in src)
            {
                if (kv.Key.StartsWith("_", StringComparison.Ordinal))
                    continue; // _lang metadata, not UI strings
                var key = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
                if (kv.Value is Dictionary<string, object?> nested)
                    Flatten(nested, key, dst);
                else
                    dst[key] = kv.Value?.ToString() ?? "";
            }
        }

        // ---------- paths ----------

        private static string? ResolveI18nDir()
        {
            try
            {
                var assemblyDir = Path.GetDirectoryName(typeof(JeI18n).Assembly.Location);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    var candidate = Path.Combine(assemblyDir, "Resources", "i18n");
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
                // fall through to ModPath
            }

            if (Main.Handler != null)
            {
                var candidate = Path.Combine(Main.Handler.ModPath, "Resources", "i18n");
                if (Directory.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        private static string ExpectedPaths()
        {
            var assemblyDir = "(unknown)";
            try
            {
                var d = Path.GetDirectoryName(typeof(JeI18n).Assembly.Location);
                if (!string.IsNullOrEmpty(d)) assemblyDir = d;
            }
            catch
            {
                // keep "(unknown)"
            }
            return assemblyDir + "\\Resources\\i18n" +
                   (Main.Handler != null ? " or " + Path.Combine(Main.Handler.ModPath, "Resources", "i18n") : "");
        }

        private static void WarnOnce(string message)
        {
            if (_loggedFailure) return;
            _loggedFailure = true;
            Main.Handler?.Warning(message);
        }
    }
}
