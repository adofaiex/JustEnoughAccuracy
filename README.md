# Just Enough Accuracy (JEA)

View judgements and details, export playing data with Acc changes.

A new angle-level accuracy judgement for *A Dance of Fire and Ice* (ADOFAI). It
reads the same hit data as the official judgement but grades it with its own
fixed scoring table, so it coexists with — and never affects — the original
judgement.

## Features

1. **New `JEA` accuracy** — Adds a separate JEA accuracy to ADOFAI. It does not
   affect the original judgement.
2. **Detailed results viewer** — After every level you can open a window to
   view per-tile details, search and scroll through the whole session.
3. **Export & share** — Export the session to a `.yaml` file or an interactive
   `.html` chart to share your score.

## Judgement mechanism

JEA is a fixed, point-allocation (赋分制) judgement:

- Each tile's raw **angle deviation is normalized to a reference BPM**
  (`deviation × 100 / BPM`), so a constant timing error in milliseconds grades
  identically on any chart.
- The normalized deviation is graded against a **fixed band table**, and the
  score **interpolates linearly** between neighbouring anchors — so every point
  value (95, 97, 98.4 …) is reachable, not just the anchors.

| Normalized deviation | Score |
|---|---|
| ≤ 1.7° | 100 |
| 2.0° | 96 |
| 2.4° | 92 |
| 2.8° | 88 |
| 3.2° | 84 |
| 3.6° | 80 |
| 4.0° | 75 |
| 4.5° | 70 |
| 5.0° | 62 |
| 5.5° | 54 |
| 6.0° | 46 |
| 6.6° | 36 |
| 7.2° | 26 |
| 8.0° | 15 |
| > 8.0° | 0 |

- **Combo** — consecutive tiles scoring ≥ 50 build a combo. The combo is
  tracked for display only; it does **not** multiply tile scores, so JEA
  accuracy is capped at 100%.
- **Empty-press tolerance** — mirrors the official `consecutiveMultipressCounter > 8`
  rule: the first 8 consecutive empty presses are forgiven, after which each
  one costs a −100 penalty and resets the combo.
- **Miss / overload** — each failed tile scores −100.
- **Accuracy** — `JEA Acc = TotalScore / (Tiles × 100)`, displayed with up to
  4 decimal places.

## Export

Exported files are written to `<mod directory>/reports`.

## Supported Loaders

UnityModManager, MelonLoader, BepInEx

## Supported Game Version

≥ 3.1.0

## Linked mod

NotEnoughAccuracy (NEA) — if installed, its data will also be displayed in the
detail viewer and exports.