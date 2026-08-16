using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

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
                __result = __result.TrimEnd() + "\n" + JeI18n.GetF("results.line",
                    Math.Floor(JeaScore.TotalScore), JeaScore.CachedAccuracy / 10000m, JeaScore.MaxCombo, JeaScore.Tiles);
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
                if (!Main.Settings.Enabled) return;
                // Never let a JEA failure abort the official Show() mid-method —
                // that would break the win sequence (last-tile unresponsive).
                try
                {
                    ResultsScreenButton.Show();
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] ResultsScreenButton.Show failed: {ex}");
                }
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
                JeaScore.CurrentBpm = __instance.conductor.bpm;
                JeaScore.CurrentDeviationDeg = Math.Abs(rad) * 180.0 / Math.PI;
                JeaScore.TileScore = JeaScore.BaseScore(JeaScore.NormalizeDeviation(JeaScore.CurrentDeviationDeg));
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
                JudgementRecorder.Clear();
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
                var rawDeg = JeaScore.CurrentDeviationDeg;
                var isTile = JeaScore.LastOperation == JeaScore.LastOp.Tile;

                JudgementRecorder.Capture(
                    tile: scrController.instance.currentSeqID + 1,
                    timestamp: scrConductor.instance.songposition_minusi,
                    rawDevDeg: rawDeg,
                    normalizedDevDeg: JeaScore.NormalizeDeviation(Math.Abs(rawDeg)),
                    jeaTileScore: JeaScore.TileScore,
                    jeaFinalTileScore: JeaScore.LastFinalTileScore,
                    jeaTotalScore: JeaScore.TotalScore,
                    jeaAccuracy: JeaScore.CachedAccuracy,
                    combo: JeaScore.Combo,
                    margin: hit,
                    acc: __instance.percentAcc,
                    xAcc: __instance.percentXAcc,
                    isEmptyPress: isEmpty && !isTile);
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
        /// machine nor deactivate detailedResults, so the results-button would linger.
        /// Hide our UI the moment the editor returns to edit mode.
        /// </summary>
        [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.SwitchToEditMode))]
        public static class scnEditor_SwitchToEditMode
        {
            public static void Postfix()
            {
                if (!Main.Settings.Enabled) return;
                try
                {
                    JePreviewer.Close();
                    ResultsScreenButton.Hide();
                }
                catch (Exception ex)
                {
                    Main.Handler?.Error($"[JEA][Patch] SwitchToEditMode cleanup failed: {ex}");
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
