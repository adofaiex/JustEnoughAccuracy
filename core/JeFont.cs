using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Shared TMP font for the JEA UI, mirroring Jade's approach: a bundled
    /// MiSans-Bold.ttf is turned into a dynamic TMP font asset so Chinese and
    /// Latin render the same as Jade. Falls back to the game's Chinese font.
    /// </summary>
    public static class JeFont
    {
        private static TMP_FontAsset? _asset;
        private static bool _logged;

        public static TMP_FontAsset? Get()
        {
            if (_asset != null)
                return _asset;

            // 1) bundled MiSans (matches Jade)
            if (TryLoadBundled(out _asset))
                return _asset;

            // 2) game's Chinese font asset
            try
            {
                if (RDConstants.data != null && RDConstants.data.chineseFontTMPro != null)
                {
                    _asset = RDConstants.data.chineseFontTMPro;
                    return _asset;
                }
                if (RDConstants.data != null && RDConstants.data.latinFontTMPro != null)
                {
                    _asset = RDConstants.data.latinFontTMPro;
                    return _asset;
                }
            }
            catch (Exception ex)
            {
                LogOnce("[JEA] failed to get game font: " + ex.Message);
            }
            return null;
        }

        private static bool TryLoadBundled(out TMP_FontAsset? asset)
        {
            asset = null;
            try
            {
                string path = ResolveFontPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    LogOnce("[JEA] MiSans-Bold.ttf not found at " + (path ?? "?"));
                    return false;
                }

                var font = new Font(path);
                var created = TMP_FontAsset.CreateFontAsset(
                    font, 72, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (created != null)
                {
                    created.name = "JEA_MiSans";
                    created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    created.isMultiAtlasTexturesEnabled = true;
                    asset = created;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogOnce("[JEA] failed to create MiSans font: " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// The font lives in <c>Resources/MiSans-Bold.ttf</c> next to the deployed
        /// assembly (UMM: Mods/&lt;Mod&gt;/Resources, BepInEx: plugins/&lt;Mod&gt;/Resources,
        /// Melon: Mods/Resources). Resolve from the assembly location first, then the
        /// handler's ModPath as a fallback.
        /// </summary>
        private static string ResolveFontPath()
        {
            try
            {
                var assemblyDir = Path.GetDirectoryName(typeof(JeFont).Assembly.Location);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    var candidate = Path.Combine(assemblyDir, "Resources", "MiSans-Bold.ttf");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception ex)
            {
                LogOnce("[JEA] failed to resolve assembly dir: " + ex.Message);
            }

            if (Main.Handler != null)
                return Path.Combine(Main.Handler.ModPath, "Resources", "MiSans-Bold.ttf");
            return string.Empty;
        }

        private static void LogOnce(string message)
        {
            if (_logged) return;
            _logged = true;
            Main.Handler?.Warning(message);
        }
    }
}
