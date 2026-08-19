using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace JustEnoughAccuracy.Loaders
{
    public class BepInHandler : IHandler
    {
        private readonly ManualLogSource _log;
        private readonly string _settingsPath;

        public BepInHandler(ManualLogSource log)
        {
            _log = log;
            _settingsPath = Path.Combine(Paths.ConfigPath, $"{ModId}.json");
        }

        public string ModId => "JEA";
        public string ModVersion => "0.1.8";
        public string ModPath => Paths.PluginPath;

        public void Log(string message) => _log.LogInfo(message);
        public void Warning(string message) => _log.LogWarning(message);
        public void Error(string message) => _log.LogError(message);

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
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Error($"Failed to save settings: {ex}");
            }
        }

        public void TriggerToggle(bool value) => OnToggle?.Invoke(value);
        public void TriggerUpdate(float dt) => OnUpdate?.Invoke(dt);
        public void TriggerGUI() => OnGUI?.Invoke();

        public event Action<float>? OnUpdate;
        public event Action<bool>? OnToggle;
        public event Action? OnGUI;
        public event Action? OnSaveGUI;
    }
}
