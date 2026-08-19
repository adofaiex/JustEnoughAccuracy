using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming

namespace JustEnoughAccuracy
{
    public static class Patches
    {
        private static readonly Regex RegexInjectedJudgementText = new("\u200B.*?\u200B");

        [HarmonyPatch(typeof(DetailedResults), "GenerateResults")]
        public static class DetailedResults_GenerateResults
        {
            public static void Postfix(ref string __result)
            {
                if (!Main.Settings.DisplayInDetailedResults || !Main.Settings.Enabled) return;
                var acc = JeaScore.CachedAccuracy;
                __result = __result.TrimEnd() + "\n" + JeI18n.GetF("results.line",
                    Math.Floor(JeaScore.TotalScore), acc / 10000m, JeaScore.MaxCombo, JeaScore.Tiles,
                    JeaScore.AccuracyColorHex(acc));
            }
        }

        /// <summary>
        /// Shows the browse button at the exact moment the official detailed
        /// results appear. Tied to the real Show() so it can't misfire in the
        /// editor or on transient state-machine states.
        /// </summary>
        [HarmonyPatch(typeof(DetailedResults), "Show")]
        public static class DetailedResults_Show
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectBeforeEveryRet(codes,
                    OpCodes.Call, typeof(DetailedResults_Show).GetMethod(nameof(OnShow),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnShow()
            {
                Main.Handler?.Log("[JEA][Patch] DetailedResults.Show() transpiler fired");
                // The previewer icon is now attached via Harmony patches on the
                // difficulty selectors — no action needed here.
            }
        }

        [HarmonyPatch(typeof(scrHitTextMesh), nameof(scrHitTextMesh.Show))]
        public static class scrHitTextMesh_Show
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectAtStart(codes, new CodeInstruction(OpCodes.Ldarg_0),
                    OpCodes.Call, typeof(scrHitTextMesh_Show).GetMethod(nameof(OnHitTextShow),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnHitTextShow(scrHitTextMesh __instance)
            {
                var text = __instance.text.text;
                if (!Main.Settings.DisplayInJudgementTexts || !Main.Settings.Enabled ||
                    scrController.instance.playerOne.midspinInfiniteMargin)
                {
                    if (text.Contains('\u200B')) __instance.Init(__instance.hitMargin);
                    return;
                }

                var tileScore = JeaScore.TileScore;
                if (Main.Settings.NoDisplayPerfect && __instance.hitMargin == HitMargin.Perfect)
                {
                    __instance.text.text = $"\u200B\u200B{(long)Math.Floor(tileScore)}\u200B\u200B";
                    return;
                }

                if (text.Contains("\u200B\u200B"))
                {
                    __instance.Init(__instance.hitMargin);
                    text = __instance.text.text;
                }

                // floored for the in-game hit text; the exact value goes to the recorder
                double? score = __instance.hitMargin switch
                {
                    HitMargin.Multipress => null,
                    HitMargin.OverPress => null,
                    HitMargin.FailMiss => -100,
                    HitMargin.FailOverload => -100,
                    HitMargin.TooEarly => null,
                    HitMargin.TooLate => null,
                    _ => Math.Floor(tileScore)
                };

                if (score is null) return;

                text = RegexInjectedJudgementText.Replace(text, "");
                text += $"\u200B {(long)score}\u200B";
                __instance.text.text = text;
            }
        }

        [HarmonyPatch(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen))]
        public static class scrPlanet_SwitchChosen
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectAtStart(codes, new CodeInstruction(OpCodes.Ldarg_0),
                    OpCodes.Call, typeof(scrPlanet_SwitchChosen).GetMethod(nameof(OnSwitchChosen),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnSwitchChosen(scrPlanet __instance)
            {
                var rad = __instance.cachedAngle - __instance.targetExitAngle;
                if (!__instance.planetarySystem.isCW) rad = -rad;

                // Effective rate = BPM × speed × pitch. Pitch lives on an AudioSource
                // field we can't reference without UnityEngine.AudioModule, so it
                // is read through reflection instead.
                float pitch = 1f;
                try
                {
                    var songField = __instance.conductor.GetType().GetField("song");
                    if (songField != null)
                    {
                        var song = songField.GetValue(__instance.conductor);
                        if (song != null)
                            pitch = (float)(song.GetType().GetProperty("pitch")?.GetValue(song) ?? 1f);
                    }
                }
                catch { }

                JeaScore.CurrentBpm = __instance.conductor.bpm *
                    (float)__instance.planetarySystem.speed * pitch;
                JeaScore.CurrentDeviationSignedDeg = rad * 180.0 / Math.PI;
                JeaScore.CurrentDeviationDeg = Math.Abs(rad) * 180.0 / Math.PI;
                JeaScore.TileScore = JeaScore.BaseScore(JeaScore.CurrentDeviationDeg);

                // Capture every ball's position at this exact instant. At
                // SwitchChosen the planets are still at the hit spot, whereas by
                // the time the margin-tracker hook runs they've started moving.
                try
                {
                    var pts = new List<Vector3>();
                    var system = __instance.planetarySystem;
                    if (system != null)
                    {
                        foreach (var p in system.planetList)
                            if (p != null) pts.Add(p.transform.position);
                    }
                    JudgementRecorder.PendingBallPositions = pts;
                }
                catch (Exception ex)
                {
                    JudgementRecorder.PendingBallPositions = null;
                    Main.Handler?.Error($"[JEA][Patch] SwitchChosen ball capture failed: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.Reset))]
        public static class scrMarginTracker_Reset
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectBeforeEveryRet(codes,
                    OpCodes.Call, typeof(scrMarginTracker_Reset).GetMethod(nameof(OnReset),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnReset()
            {
                JeaScore.Reset();
                // JudgementRecorder data is intentionally NOT cleared here —
                // scrMarginTracker.Reset fires on death, Esc exit, AND restart.
                // We only clear on a fresh run start (scnEditor.Play patch).
            }
        }

        [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
        public static class scrMarginTracker_AddHit
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectAtStart(codes, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    OpCodes.Call, typeof(scrMarginTracker_AddHit).GetMethod(nameof(OnAddHit),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                InjectBeforeEveryRet(codes,
                    new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    OpCodes.Call, typeof(scrMarginTracker_AddHit).GetMethod(nameof(OnCaptureHit),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnAddHit(scrMarginTracker __instance, HitMargin hit)
            {
                if (!Main.Settings.Enabled) return;
                if (scrController.instance.playerOne.midspinInfiniteMargin)
                {
                    JeaScore.AddNoop();
                    return;
                }

                switch (hit)
                {
                    case HitMargin.Multipress:
                        JeaScore.AddEmptyPress();
                        break;
                    case HitMargin.OverPress:
                        JeaScore.AddEmptyPress();
                        break;
                    case HitMargin.FailMiss:
                        JeaScore.AddFail(false);
                        break;
                    case HitMargin.FailOverload:
                        JeaScore.AddFail(true);
                        break;
                    case HitMargin.TooEarly:
                        JeaScore.AddEmptyPress();
                        break;
                    case HitMargin.Auto:
                        JeaScore.AddNoop();
                        break;
                    default:
                        JeaScore.AddTile();
                        break;
                }
                JeaScore.Cache();
            }

            private static void OnCaptureHit(scrMarginTracker __instance, HitMargin hit)
            {
                if (!Main.Settings.Enabled) return;

                var isEmpty = JeaScore.LastOperation == JeaScore.LastOp.EmptyPress;
                var rawDeg = JeaScore.CurrentDeviationSignedDeg;
                var isTile = JeaScore.LastOperation == JeaScore.LastOp.Tile;

                // Prefer the exact positions captured at SwitchChosen. Fall back
                // to reading the planets now if that snapshot is missing.
                var ballPositions = JudgementRecorder.PendingBallPositions;
                JudgementRecorder.PendingBallPositions = null;
                if (ballPositions == null || ballPositions.Count == 0)
                {
                    try
                    {
                        var system = scrController.instance?.planetarySystem;
                        if (system != null)
                        {
                            ballPositions = new List<Vector3>();
                            foreach (var p in system.planetList)
                                if (p != null) ballPositions.Add(p.transform.position);
                        }
                    }
                    catch { ballPositions = null; }
                }

                JudgementRecorder.Capture(
                    tile: scrController.instance.currentSeqID + 1,
                    timestamp: scrConductor.instance.songposition_minusi,
                    rawDevDeg: rawDeg,
                    normalizedDevDeg: JeaScore.NormalizeDeviation(rawDeg),
                    jeaTileScore: JeaScore.TileScore,
                    jeaFinalTileScore: JeaScore.LastFinalTileScore,
                    jeaTotalScore: JeaScore.TotalScore,
                    jeaAccuracy: JeaScore.CachedAccuracy,
                    combo: JeaScore.Combo,
                    margin: hit,
                    acc: __instance.percentAcc,
                    xAcc: __instance.percentXAcc,
                    isEmptyPress: isEmpty && !isTile,
                    ballPositions: ballPositions);
            }
        }

        [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.RevertToLastCheckpoint))]
        public static class scrMarginTracker_RevertToLastCheckpoint
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                InjectBeforeEveryRet(codes, new CodeInstruction(OpCodes.Ldarg_0),
                    OpCodes.Call, typeof(scrMarginTracker_RevertToLastCheckpoint).GetMethod(nameof(OnRevert),
                        BindingFlags.Static | BindingFlags.NonPublic)!);
                return codes;
            }

            private static void OnRevert(scrMarginTracker __instance)
            {
                JeaScore.RevertTo(__instance.hitMargins.Count);
                JudgementRecorder.RevertTo(__instance.hitMargins.Count);
            }
        }

        /// <summary>
        /// Leaving the editor's play mode (Esc) does NOT reset the controller state
        /// machine nor deactivate detailedResults. The JEA previewer now persists on
        /// purpose (so the player can locate a tile after playback ends), so we only
        /// hide the death markers here, not the previewer/button.
        /// </summary>
        [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.SwitchToEditMode))]
        public static class scnEditor_SwitchToEditMode
        {
            public static void Postfix()
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    // Keep marker positions, just hide them while editing.
                    DeathMarker.Hide();
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] SwitchToEditMode cleanup failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Re-entering the editor's play mode re-shows death markers for the same
        /// chart (they stay hidden while editing).
        /// </summary>
        [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.Play))]
        public static class scnEditor_Play
        {
            public static void Postfix()
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    JudgementRecorder.Clear();
                    HitMarker.Clear();
                    DeathMarker.Show();
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] scnEditor.Play DeathMarker.Show failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Loading a different chart drops any death markers recorded for the
        /// previous chart.
        /// </summary>
        [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.OpenLevel))]
        public static class scnEditor_OpenLevel
        {
            public static void Postfix()
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    DeathMarker.Clear();
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] scnEditor.OpenLevel DeathMarker.Clear failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Attach the JEA previewer icon next to the editor difficulty selector.
        /// </summary>
        [HarmonyPatch(typeof(EditorDifficultySelector), "OnEnable")]
        public static class EditorDifficultySelector_OnEnable
        {
            public static void Postfix(EditorDifficultySelector __instance)
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    PreviewerButton.EnsureAttached(__instance.selectorRectTransform);
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] EditorDifficultySelector attach failed: {ex}");
                }
            }
        }

        /// <summary>
        /// Attach the JEA previewer icon next to the game HUD difficulty container.
        /// </summary>
        [HarmonyPatch(typeof(scrUIController), nameof(scrUIController.ShowDifficultyContainer))]
        public static class scrUIController_ShowDifficultyContainer
        {
            public static void Postfix(scrUIController __instance)
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    PreviewerButton.EnsureAttached(__instance.difficultyContainer);
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] ShowDifficultyContainer attach failed: {ex}");
                }
            }
        }

        /// <summary>
        /// The player died: record one dashed circle per ball at the moment of
        /// death, before the level resets. Tied to FailAction which only runs when
        /// the run actually ends (no-fail/lives paths return earlier).
        /// </summary>
        [HarmonyPatch(typeof(scrController), nameof(scrController.FailAction))]
        public static class scrController_FailAction
        {
            public static void Postfix(scrController __instance)
            {
                if (!Main.Settings.Enabled || !Main.Settings.ShowDeathMarkers) return;
                try
                {
                    var pts = new List<Vector3>();
                    var system = __instance?.planetarySystem;
                    if (system != null)
                    {
                        foreach (var p in system.planetList)
                            if (p != null) pts.Add(p.transform.position);
                    }
                    DeathMarker.Mark(pts, __instance != null ? __instance.currentSeqID : 0);
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] FailAction death marker failed: {ex}");
                }
            }
        }

        // ---------- transpiler helpers ----------

        /// <summary>Inserts [prefixInsts..., call] at the very start of the method, moving any
        /// branch labels off the original first instruction.</summary>
        private static void InjectAtStart(List<CodeInstruction> codes, CodeInstruction prefixInst,
            OpCode callOp, MethodInfo target)
        {
            InjectAtStart(codes, new[] { prefixInst }, callOp, target);
        }

        private static void InjectAtStart(List<CodeInstruction> codes,
            CodeInstruction prefixInst1, CodeInstruction prefixInst2, OpCode callOp, MethodInfo target)
        {
            InjectAtStart(codes, new[] { prefixInst1, prefixInst2 }, callOp, target);
        }

        private static void InjectAtStart(List<CodeInstruction> codes, CodeInstruction[] prefixInsts,
            OpCode callOp, MethodInfo target)
        {
            var injected = new List<CodeInstruction>(prefixInsts)
            {
                new(callOp, target)
            };
            if (codes.Count > 0)
                codes[0].MoveLabelsTo(injected[0]);
            codes.InsertRange(0, injected);
        }

        /// <summary>Inserts [prefixInsts..., call] right before every 'ret' so the call runs on
        /// all exit paths. Branch labels targeting a ret are moved to the inserted call.</summary>
        private static void InjectBeforeEveryRet(List<CodeInstruction> codes, CodeInstruction prefixInst,
            OpCode callOp, MethodInfo target)
        {
            InjectBeforeEveryRet(codes, new[] { prefixInst }, callOp, target);
        }

        private static void InjectBeforeEveryRet(List<CodeInstruction> codes,
            OpCode callOp, MethodInfo target)
        {
            InjectBeforeEveryRet(codes, Array.Empty<CodeInstruction>(), callOp, target);
        }

        private static void InjectBeforeEveryRet(List<CodeInstruction> codes,
            CodeInstruction prefixInst1, CodeInstruction prefixInst2, OpCode callOp, MethodInfo target)
        {
            InjectBeforeEveryRet(codes, new[] { prefixInst1, prefixInst2 }, callOp, target);
        }

        private static void InjectBeforeEveryRet(List<CodeInstruction> codes, CodeInstruction[] prefixInsts,
            OpCode callOp, MethodInfo target)
        {
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ret) continue;

                var injected = new List<CodeInstruction>(prefixInsts)
                {
                    new(callOp, target)
                };
                codes[i].MoveLabelsTo(injected[0]);
                codes[i].MoveBlocksTo(injected[0]);
                codes.InsertRange(i, injected);
                i += injected.Count;
            }
        }
    }
}
