using System;
using System.IO;
using UnityEngine;

namespace AdofaiMod.MultiLoader
{
    public static class ResourceLoader
    {
        public static string ResourcesPath
        {
            get
            {
                if (Main.Handler == null)
                    throw new InvalidOperationException("Mod is not initialized");
                return Path.Combine(Main.Handler.ModPath, "Resources");
            }
        }

        public static string LoadTextFile(string fileName)
        {
            var path = Path.Combine(ResourcesPath, fileName);
            if (!File.Exists(path))
            {
                Main.Handler?.Error($"Text file not found: {path}");
                return string.Empty;
            }
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"Failed to load text file: {fileName}\n{ex}");
                return string.Empty;
            }
        }

        public static Texture2D? LoadTexture(string fileName)
        {
            var path = Path.Combine(ResourcesPath, fileName);
            if (!File.Exists(path))
            {
                Main.Handler?.Error($"Image file not found: {path}");
                return null;
            }
            try
            {
                var data = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (ImageConversion.LoadImage(tex, data))
                    return tex;
                Main.Handler?.Error($"Failed to decode image: {fileName}");
                return null;
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"Failed to load texture: {fileName}\n{ex}");
                return null;
            }
        }

        public static byte[] LoadBinaryFile(string fileName)
        {
            var path = Path.Combine(ResourcesPath, fileName);
            if (!File.Exists(path))
            {
                Main.Handler?.Error($"Binary file not found: {path}");
                return Array.Empty<byte>();
            }
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"Failed to load binary file: {fileName}\n{ex}");
                return Array.Empty<byte>();
            }
        }

        public static string[] GetFiles(string searchPattern = "*.*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            try
            {
                if (!Directory.Exists(ResourcesPath))
                    return Array.Empty<string>();
                return Directory.GetFiles(ResourcesPath, searchPattern, searchOption);
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"Failed to list files: {ex}");
                return Array.Empty<string>();
            }
        }
    }
}
