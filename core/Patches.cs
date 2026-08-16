using System;
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
                __result = $"{__result.TrimEnd()}\nJEA: {JeaScore.TotalScore} ({JeaScore.CachedAccuracy / 10000m}%) | Combo {JeaScore.MaxCombo} | Tiles {JeaScore.Tiles}";
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
            public static void Postfix()
            {
                Main.Handler?.Log("[JEA][Patch] DetailedResults.Show() postfix running");
                if (!Main.Settings.Enabled) 
                {
                    Main.Handler?.Log("[JEA][Patch] Mod disabled, skipping ResultsScreenButton.Show()");
                    return;
                }
                Main.Handler?.Log("[JEA][Patch] NOT calling ResultsScreenButton.Show() anymore; use Ctrl+F8 shortcut instead");
            }
        }

        [HarmonyPatch(typeof(scrHitTextMesh), nameof(scrHitTextMesh.Show))]
        public static class scrHitTextMesh_Show
        {
            public static void Prefix(scrHitTextMesh __instance)
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
                    __instance.text.text = $"\u200B\u200B{tileScore}\u200B\u200B";
                    return;
                }

                if (text.Contains("\u200B\u200B"))
                {
                    __instance.Init(__instance.hitMargin);
                    text = __instance.text.text;
                }

                long? score = __instance.hitMargin switch
                {
                    HitMargin.Multipress => null,
                    HitMargin.OverPress => null,
                    HitMargin.FailMiss => -100,
                    HitMargin.FailOverload => -100,
                    HitMargin.TooEarly => null,
                    HitMargin.TooLate => null,
                    _ => tileScore
                };

                if (score is null) return;

                text = RegexInjectedJudgementText.Replace(text, "");
                text += $"\u200B {score}\u200B";
                __instance.text.text = text;
            }
        }

        [HarmonyPatch(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen))]
        public static class scrPlanet_SwitchChosen
        {
            public static void Prefix(scrPlanet __instance)
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
            public static void Prefix()
            {
                JeaScore.Reset();
                JudgementRecorder.Clear();
            }
        }

        [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
        public static class scrMarginTracker_AddHit
        {
            public static void Prefix(HitMargin hit)
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

            public static void Postfix(scrMarginTracker __instance, HitMargin hit)
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
            public static void Postfix(scrMarginTracker __instance)
            {
                JeaScore.RevertTo(__instance.hitMargins.Count);
                JudgementRecorder.RevertTo(__instance.hitMargins.Count);
            }
        }
    }
}
