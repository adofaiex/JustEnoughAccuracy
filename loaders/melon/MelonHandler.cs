using System;
using System.IO;
using MelonLoader;
using Newtonsoft.Json;

namespace AdofaiMod.MultiLoader.Loaders
{
    public class MelonHandler : IHandler
    {
        private readonly MelonMod _mod;
        private readonly string _settingsPath;

        public MelonHandler(MelonMod mod)
        {
            _mod = mod;
            _settingsPath = Path.Combine("UserData", $"{ModId}.json");
        }

        public string ModId => _mod.MelonAssembly.Assembly.GetName().Name ?? "AdofaiMod";
        public string ModVersion => _mod.MelonAssembly.Assembly.GetName().Version?.ToString() ?? "1.0.0";
        public string ModPath => Directory.GetCurrentDirectory();

        public void Log(string message) => MelonLogger.Msg(message);
        public void Warning(string message) => MelonLogger.Warning(message);
        public void Error(string message) => MelonLogger.Error(message);

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
