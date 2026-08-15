using System;
using System.IO;
using Newtonsoft.Json;
using UnityModManagerNet;

namespace JustEnoughAccuracy.Loaders
{
    public class UmmHandler : IHandler
    {
        private readonly UnityModManager.ModEntry _entry;
        private readonly string _settingsPath;

        public UmmHandler(UnityModManager.ModEntry entry)
        {
            _entry = entry;
            _settingsPath = Path.Combine(entry.Path, "Config", $"{entry.Info.Id}.json");

            entry.OnToggle = (_, value) =>
            {
                OnToggle?.Invoke(value);
                return true;
            };
            entry.OnGUI = _ => OnGUI?.Invoke();
            entry.OnSaveGUI = _ => OnSaveGUI?.Invoke();
            entry.OnUpdate = (_, dt) => OnUpdate?.Invoke(dt);
        }

        public string ModId => _entry.Info.Id;
        public string ModVersion => _entry.Info.Version;
        public string ModPath => _entry.Path;

        public void Log(string message) => _entry.Logger.Log(message);
        public void Warning(string message) => _entry.Logger.Warning(message);
        public void Error(string message) => _entry.Logger.Error(message);

        public T LoadSettings<T>() where T : class, new()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
            }
            catch (Exception ex)
            {
                Error($"Failed to load settings: {ex}");
            }
            return new T();
        }

        public void SaveSettings<T>(T settings) where T : class
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath)!;
                Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Error($"Failed to save settings: {ex}");
            }
        }

        public event Action<float>? OnUpdate;
        public event Action<bool>? OnToggle;
        public event Action? OnGUI;
        public event Action? OnSaveGUI;
    }
}
