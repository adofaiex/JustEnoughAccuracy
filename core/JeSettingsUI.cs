using System;
using JustEnoughAccuracy.UI;
using UnityEngine;
using static JustEnoughAccuracy.UI.IridiumLayout;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Settings panel drawn inside the loader's settings window (UMM: the mod's
    /// page in the mod-manager UI). IridiumLayout-styled; every change saves
    /// immediately. Only display toggles live here — scoring is fixed by design.
    /// </summary>
    public static class JeSettingsUI
    {
        private static SizesGroup.Holder _sizesHolder = new();

        public static void Draw()
        {
            try
            {
                EnsureTexturesAlive();

                var s = Main.Settings;
                var sizes = _sizesHolder.Begin();

                Render(
                    VBox(
                        ContainerStyle.Padding,
                        null,
                        WithWidthMax(
                            Text($"JEA v{Main.Handler?.ModVersion}", TextStyle.Title),
                            Separator(),
                            IridiumPreset.SwitchOption(sizes, s.Enabled,
                                v => s.Enabled = v, "settings.enabled"),
                            Separator(),
                            IridiumPreset.SwitchOption(sizes, s.DisplayInJudgementTexts,
                                v => s.DisplayInJudgementTexts = v, "settings.displayJudgement"),
                            Enabled(
                                () => s.DisplayInJudgementTexts,
                                Separator(),
                                IridiumPreset.SwitchOption(sizes, s.NoDisplayPerfect,
                                    v => s.NoDisplayPerfect = v, "settings.noDisplayPerfect")
                            ),
                            Separator(),
                            IridiumPreset.SwitchOption(sizes, s.DisplayInDetailedResults,
                                v => s.DisplayInDetailedResults = v, "settings.displayResults"),
                            Separator(),
                            IridiumPreset.IconText(sizes, IconStyle.Information, "settings.hint")
                        )
                    )
                );

                if (GUI.changed)
                    Main.Handler?.SaveSettings(Main.Settings);
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA][Settings] draw failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static object[] WithWidthMax(params Element[] children)
        {
            var content = new object[children.Length + 1];
            content[0] = WidthMax;
            Array.Copy(children, 0, content, 1, children.Length);
            return content;
        }
    }
}
