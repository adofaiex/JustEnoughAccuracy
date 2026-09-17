using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;
using static JustEnoughAccuracy.UI.IridiumLayout;

namespace JustEnoughAccuracy.Loaders
{
    public class BepInHandler : IHandler
    {
        private readonly ManualLogSource _log;
        private readonly string _settingsPath;

        // BepInEx has no host settings UI, so JEA draws its own IMGUI window,
        // toggled by a hotkey (same approach as Iridium's loader).
        private bool _uiVisible;
        private Rect _rect;
        private Vector2 _scrollPos;
        private bool _isDragging;
        private Vector2 _dragOffset;
        private const float TitleBarHeight = 40f;

        public BepInHandler(ManualLogSource log)
        {
            _log = log;
            _settingsPath = Path.Combine(Paths.ConfigPath, $"{ModId}.json");
        }

        public string ModId => "JEA";
        public string ModVersion => "0.2.0";
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

        public void TriggerUpdate(float dt)
        {
            if (Main.Settings != null && CheckHotkey(Main.Settings.PanelToggleHotkey))
            {
                _uiVisible = !_uiVisible;
                float w = Mathf.Max(Screen.width * 0.8f, 960);
                float h = Mathf.Max(Screen.height * 0.8f, 720);
                _rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            }
            OnUpdate?.Invoke(dt);
        }

        public void TriggerGUI()
        {
            if (!_uiVisible) return;

            EnsureTexturesAlive();
            HandleWindowDrag();

            Render(
                Area(_rect,
                    VBox(
                        ContainerStyle.Background,
                        null,
                        WidthMax,
                        HBox(
                            ContainerStyle.None,
                            null,
                            WidthMax,
                            Text("Just Enough Accuracy", TextStyle.Title),
                            Fill(),
                            Button("\u00d7", ButtonStyle.Element, () => _uiVisible = false,
                                GUILayout.Width(28), GUILayout.Height(28))
                        ),
                        Space(8),
                        ScrollView(_scrollPos, p => _scrollPos = p, WidthMax, () => OnGUI?.Invoke())
                    )
                )
            );

            _rect.x = (int)_rect.x;
            _rect.y = (int)_rect.y;
        }

        /// <summary>Parses a hotkey like "Ctrl+F8" / "Alt+O" / "Shift+F9" and reports a key-down this frame.</summary>
        private static bool CheckHotkey(string? hotkey)
        {
            if (string.IsNullOrEmpty(hotkey)) return false;

            var parts = hotkey.Split('+');
            KeyCode? targetKey = null;
            bool needCtrl = false, needAlt = false, needShift = false;

            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Length == 0) continue;

                var lower = p.ToLowerInvariant();
                if (lower == "ctrl") needCtrl = true;
                else if (lower == "alt") needAlt = true;
                else if (lower == "shift") needShift = true;
                else if (Enum.TryParse<KeyCode>(p, ignoreCase: true, out var kc)) targetKey = kc;
            }

            if (targetKey == null) return false;

            if (needCtrl && !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return false;
            if (needAlt && !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return false;
            if (needShift && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) return false;

            return Input.GetKeyDown(targetKey.Value);
        }

        private void HandleWindowDrag()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    // Draggable only from the title bar, excluding the close button.
                    if (e.mousePosition.y >= _rect.y && e.mousePosition.y <= _rect.y + TitleBarHeight
                        && e.mousePosition.x < _rect.xMax - 36)
                    {
                        _isDragging = true;
                        _dragOffset = e.mousePosition - _rect.position;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    _isDragging = false;
                    break;
                case EventType.MouseDrag:
                    if (_isDragging)
                    {
                        _rect.position = e.mousePosition - _dragOffset;
                        e.Use();
                    }
                    break;
            }
        }

        public event Action<float>? OnUpdate;
        public event Action<bool>? OnToggle;
        public event Action? OnGUI;
        public event Action? OnSaveGUI;
    }
}
