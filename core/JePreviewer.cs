using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// The JEA judgement previewer: an ADOFAI-styled overlay opened from the results
    /// screen. Shows every captured tile with search, a scrollable list, an export
    /// button and a close (X / Esc) control. Owns only the current run's data.
    /// </summary>
    public static class JePreviewer
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 640f;
        private const float Padding = 18f;
        private const float TitleHeight = 34f;
        private const float SearchHeight = 42f;
        private const float RowHeight = 30f;
        private const float ButtonSize = 44f;
        private const float ScrollbarWidth = 10f;
        private const float SeparatorHeight = 1f;
        private const string TitleMarkup = "<b><color=#FFFFFFFF>JustEnoughAcc</color></b>";

        private sealed class RowWidget
        {
            public GameObject Root = null!;
            public RectTransform Rect = null!;
            public TextMeshProUGUI Text = null!;
            public JudgementRecord Record = null!;
            public Button Button = null!;
        }

        private static readonly List<RowWidget> Rows = new();
        private static readonly StringBuilder Builder = new(256);

        private static Canvas? _canvas;
        private static RectTransform? _panelRect;
        private static Image? _background;
        private static Image? _border;
        private static TextMeshProUGUI? _title;
        private static TMP_InputField? _search;
        private static GameObject? _exportMenu;
        private static GameObject? _seriesMenu;
        // Unchecked chart series ids; empty set means "all on" (default).
        private static readonly HashSet<string> _chartSeriesOff = new();
        private static RectTransform? _viewportRect;
        private static RectTransform? _contentRect;
        private static float _rowWidth;
        private static TextMeshProUGUI? _summary;
        private static Texture2D? _fillTexture;
        private static Texture2D? _ringTexture;
        private static Sprite? _fillSprite;
        private static Sprite? _ringSprite;
        private static TMP_FontAsset? _font;

        private static RectTransform? _scrollbarTrack;
        private static RectTransform? _scrollbarHandle;
        private static ScrollbarDrag? _scrollbarDrag;

        private static int _visibleCount;
        private static float _maxScroll;
        private static float _scrollOffset;
        private static string _lastFilter = "";
        private static readonly List<(TextMeshProUGUI Label, string Key)> _translatedLabels = new();
        private static TextMeshProUGUI? _searchPlaceholder;
        private static int _languageVersion = -1;

        /// <summary>Whether the previewer overlay is currently open.</summary>
        public static bool IsOpen { get; private set; }

        // ---------- open / close ----------

        public static void Open()
        {
            Main.Handler?.Log("[JEA][Previewer] Open() called");
            try
            {
                EnsureUI();
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA][Previewer] EnsureUI failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            IsOpen = true;
            _scrollOffset = 0f;
            // Reset the title and the search filter: the previous run may have
            // left a "✓ exported" state or a stale search term behind.
            if (_title != null)
                _title.text = TitleMarkup;
            if (_search != null)
                _search.text = "";
            ApplyTranslations();
            if (_canvas != null)
            {
                _canvas.transform.SetAsLastSibling();
                _canvas.gameObject.SetActive(true);
            }
            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA][Previewer] Refresh failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void Close()
        {
            IsOpen = false;
            HitMarker.Clear();
            if (_exportMenu != null)
                _exportMenu.SetActive(false);
            if (_seriesMenu != null)
                _seriesMenu.SetActive(false);
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        /// <summary>Re-applies localized text when the language changed.</summary>
        private static void ApplyTranslations()
        {
            if (JeI18n.LanguageVersion == _languageVersion)
                return;
            _languageVersion = JeI18n.LanguageVersion;
            if (_searchPlaceholder != null)
                _searchPlaceholder.text = JeI18n.Get("previewer.searchPlaceholder");
            foreach (var (label, key) in _translatedLabels)
            {
                if (label != null)
                    label.text = "<b>" + JeI18n.Get(key) + "</b>";
            }
        }

        // ---------- update loop (driven from Main.OnUpdate) ----------

        public static void OnUpdate()
        {
            if (!IsOpen || _canvas == null)
                return;

            // Esc closes the previewer together with the results screen.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            HandleScroll();

            // Auto-refresh whenever the search text or data changed.
            var filter = _search != null ? _search.text : "";
            if (filter != _lastFilter || JudgementRecorder.Count != _visibleCount)
            {
                try { Refresh(); }
                catch (Exception ex) { Main.Handler?.Error($"[JEA][Previewer] Refresh failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
            }
        }

        private static void HandleScroll()
        {
            if (_maxScroll <= 0f || _viewportRect == null)
                return;
            var delta = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(delta, 0f))
                return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_viewportRect, Input.mousePosition, null))
                return;
            _scrollOffset = Mathf.Clamp(_scrollOffset - delta * RowHeight * 2f, 0f, _maxScroll);
            _contentRect!.anchoredPosition = new Vector2(0f, _scrollOffset);
            UpdateScrollbar();
        }

        /// <summary>Called by <see cref="ScrollbarDrag"/> when the handle is dragged.
        /// 0 = top of the list, 1 = bottom.</summary>
        public static void OnScrollbarDragged(float fracTop)
        {
            _scrollOffset = fracTop * _maxScroll;
            if (_contentRect != null)
                _contentRect.anchoredPosition = new Vector2(0f, _scrollOffset);
            UpdateScrollbar();
        }

        private static void UpdateScrollbar()
        {
            if (_scrollbarTrack == null || _scrollbarHandle == null || _contentRect == null)
                return;
            var trackH = _scrollbarTrack.rect.height;
            var contentH = _contentRect.rect.height;
            var handleH = trackH <= 0f || contentH <= 0f
                ? trackH
                : Mathf.Clamp(trackH * trackH / contentH, ScrollbarWidth, trackH);
            var handleRect = _scrollbarHandle;
            handleRect.sizeDelta = new Vector2(ScrollbarWidth, handleH);
            var travel = Mathf.Max(trackH - handleH, 0f);
            var frac = _maxScroll > 0f ? Mathf.Clamp01(_scrollOffset / _maxScroll) : 0f;
            handleRect.anchoredPosition = new Vector2(0f, -frac * travel);
        }

        // ---------- rendering ----------

        private static void Refresh()
        {
            Main.Handler?.Log("[JEA][Previewer] Refresh() called");
            if (_search == null) 
            {
                Main.Handler?.Log("[JEA][Previewer] Refresh() aborted: _search is null");
                return;
            }
            var filter = _search.text;
            _lastFilter = filter;

            var records = JudgementRecorder.Snapshot();
            Main.Handler?.Log($"[JEA][Previewer] Snapshot returned {records.Count} records");
            Builder.Length = 0;

            var matches = new List<JudgementRecord>(records.Count);
            foreach (var record in records)
            {
                if (MatchesFilter(record, filter))
                    matches.Add(record);
            }

            _visibleCount = matches.Count;
            Main.Handler?.Log($"[JEA][Previewer] matches={matches.Count}, records={records.Count}");

            // summary line — compact: score | acc% | combo | tiles
            Builder.Append("<color=#7CE0B3>")
                .Append(Math.Floor(JeaScore.TotalScore).ToString(CultureInfo.InvariantCulture))
                .Append("</color>")
                .Append("  <color=#5F6771>|</color>  ")
                .Append((JeaScore.CachedAccuracy / 10000m).ToString("0.####", CultureInfo.InvariantCulture))
                .Append("<color=#7CE0B3>%</color>")
                .Append("  <color=#5F6771>|</color>  x")
                .Append(JeaScore.MaxCombo.ToString(CultureInfo.InvariantCulture))
                .Append("  <color=#5F6771>|</color>  ")
                .Append(matches.Count.ToString(CultureInfo.InvariantCulture))
                .Append("<color=#5F6771>/</color>")
                .Append(records.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" <color=#5F6771>tiles</color>");
            _summary!.text = Builder.ToString();

            // rows
            var limit = Math.Min(matches.Count, 1000);
            if (matches.Count == 0)
            {
                var row = GetRow(0);
                row.Record = null!;
                row.Text.text = "<color=#5F6771>" + JeI18n.Get("previewer.noRecords") + "</color>";
                row.Button.onClick.RemoveAllListeners();
                if (!row.Root.activeSelf)
                    row.Root.SetActive(true);
                limit = 1;
            }
            else
            {
                for (var i = 0; i < limit; i++)
                {
                    var record = matches[i];
                    var row = GetRow(i);
                    row.Record = record;
                    Builder.Length = 0;
                    BuildRowText(Builder, record);
                    row.Text.text = Builder.ToString();
                    row.Button.onClick.RemoveAllListeners();
                    row.Button.onClick.AddListener(() => OnHitClicked(record));
                    if (!row.Root.activeSelf)
                        row.Root.SetActive(true);
                }
            }
            HideUnusedRows(limit);

            _contentRect!.sizeDelta = new Vector2(_rowWidth, limit * RowHeight);
            var viewportH = _viewportRect!.rect.height;
            _maxScroll = Mathf.Max(0f, limit * RowHeight - viewportH);
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, _maxScroll);
            _contentRect.anchoredPosition = new Vector2(0f, _scrollOffset);
            UpdateScrollbar();
        }

        /// <summary>
        /// A single hit row was clicked: move the camera to that tile and draw a
        /// hit-position marker on it (the previous selection's marker is replaced).
        /// </summary>
        private static void OnHitClicked(JudgementRecord record)
        {
            try
            {
                if (record == null) return;
                var pos = TilePosition(record.Tile);
                if (pos == null)
                {
                    Main.Handler?.Log($"[JEA][Previewer] No floor for tile {record.Tile}");
                    return;
                }
                MoveCameraTo(pos.Value);
                HitMarker.Show(pos.Value);
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA][Previewer] OnHitClicked failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>World position of a 1-based tile number, or null when the floor
        /// doesn't exist (e.g. chart not loaded, or past the last floor).</summary>
        private static Vector3? TilePosition(int tile)
        {
            var floors = scrLevelMaker.instance?.listFloors;
            if (floors == null || tile < 1 || tile > floors.Count)
                return null;
            var floor = floors[tile - 1];
            if (floor == null)
                return null;
            return floor.transform.position;
        }

        /// <summary>Point the relevant camera at a world position. In the editor the
        /// editor camera is moved; otherwise the gameplay camera, clamped to its Z.</summary>
        private static void MoveCameraTo(Vector3 pos)
        {
            Camera? cam = null;
            if (ADOBase.isLevelEditor && scnEditor.instance != null)
                cam = scnEditor.instance.camera;
            if (cam == null && scrCamera.instance != null)
                cam = scrCamera.instance.camobj;
            if (cam == null)
                cam = Camera.main;
            if (cam == null)
                return;
            cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
        }

        private static bool MatchesFilter(JudgementRecord record, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            Builder.Length = 0;
            BuildSearchable(Builder, record);
            return Builder.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void BuildSearchable(StringBuilder sb, JudgementRecord record)
        {
            sb.Append(record.Tile.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(ReportExporter.FormatTimestampPublic(record.Timestamp)).Append(' ')
                .Append(record.Margin).Append(' ')
                .Append(Math.Floor(record.JeaTileScore).ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Math.Floor(record.JeaFinalTileScore).ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Math.Floor(record.JeaTotalScore).ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(record.OfficialScore?.ToString(CultureInfo.InvariantCulture) ?? "").Append(' ')
                .Append(record.NeaScore?.ToString(CultureInfo.InvariantCulture) ?? "").Append(' ')
                .Append(record.RawDeviationDeg.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append(record.NormalizedDeviationDeg.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                .Append((record.Acc * 100f).ToString("0.00", CultureInfo.InvariantCulture)).Append(' ')
                .Append((record.XAcc * 100f).ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static void BuildRowText(StringBuilder sb, JudgementRecord r)
        {
            var marginColor = r.Margin switch
            {
                HitMargin.Perfect => "#FFDA00",
                HitMargin.Auto => "#FFDA00",
                HitMargin.EarlyPerfect or HitMargin.LatePerfect => "#7CE0B3",
                HitMargin.VeryEarly or HitMargin.VeryLate => "#F3D98B",
                HitMargin.TooEarly or HitMargin.TooLate => "#E08A7C",
                HitMargin.FailMiss or HitMargin.FailOverload => "#FF6B6B",
                HitMargin.Multipress or HitMargin.OverPress => "#FF8C5A",
                _ => "#FFFFFF"
            };

            // Compact: #tile  time  margin  score  deviation. Extra columns
            // (combo/Acc/Off/NEA) overflowed the panel width.
            sb.Append("<color=#5F6771>#")
                .Append(r.Tile.ToString(CultureInfo.InvariantCulture))
                .Append("</color> <color=#AEB8C4>")
                .Append(ReportExporter.FormatTimestampPublic(r.Timestamp))
                .Append("</color> <b><color=")
                .Append(marginColor)
                .Append(">")
                .Append(r.Margin)
                .Append("</color></b> <b><color=#FFFFFFFF>")
                .Append(Math.Floor(r.JeaTileScore).ToString(CultureInfo.InvariantCulture))
                .Append("</color></b> <color=#5F6771>")
                .Append(FormatDev(r.RawDeviationDeg))
                .Append("°</color>");
        }

        private static string FormatDev(double deg)
        {
            if (double.IsNaN(deg) || double.IsInfinity(deg)) return "0";
            return deg.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void HideUnusedRows(int from)
        {
            for (var i = from; i < Rows.Count; i++)
            {
                if (Rows[i].Root.activeSelf)
                    Rows[i].Root.SetActive(false);
            }
        }

        // ---------- UI construction ----------

        private static void EnsureUI()
        {
            if (_canvas != null)
                return;

            // A previous scene may have destroyed the row GameObjects while the
            // static Rows list survived; drop stale entries before rebuilding.
            Rows.Clear();

            Main.Handler?.Log("[JEA][Previewer] EnsureUI() starting");
            EnsureSprites();
            _font = JeFont.Get();

            var host = ResolveHostCanvas();
            Main.Handler?.Log($"[JEA][Previewer] ResolveHostCanvas returned: {(host != null ? host.name : "NULL")}");
            if (host == null)
            {
                Main.Handler?.Log("[JEA][Previewer] Host canvas is null, aborting UI creation");
                return;
            }

            var canvasObject = new GameObject("JEA_Previewer");
            canvasObject.transform.SetParent(host.transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            // High enough to beat every other canvas, but not exactly int.MaxValue
            // (which wraps negative if anything increments it). 30000 was verified
            // too low — a fullscreen canvas above it hid the button entirely.
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 2000000000;
            canvasObject.AddComponent<GraphicRaycaster>();
            // Stretch to fill the host canvas so the centered panel lands mid-screen.
            // (Canvas requires a RectTransform, so this cast is only valid after
            // AddComponent<Canvas>; doing it earlier throws InvalidCastException.)
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasObject.transform.SetAsLastSibling();
            // Build hidden; Open() activates only after construction fully succeeded.
            canvasObject.SetActive(false);

            var panelObject = new GameObject("Panel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            _panelRect = (RectTransform)panelObject.transform;
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            _background = panelObject.AddComponent<Image>();
            _background.sprite = _fillSprite;
            _background.type = Image.Type.Sliced;
            _background.color = new Color(0.020f, 0.028f, 0.042f, 0.94f);
            _background.raycastTarget = true;

            var drag = panelObject.AddComponent<PanelDrag>();
            drag.Setup(_panelRect, _canvas);

            _border = CreateChildImage(panelObject, "Border", _ringSprite!, new Color(1f, 1f, 1f, 0.90f));
            _border.type = Image.Type.Sliced;
            var borderRect = (RectTransform)_border.transform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            // title
            _title = CreateText(panelObject, "Title", 24f);
            _title.text = TitleMarkup;
            _title.rectTransform.anchoredPosition = new Vector2(Padding, -Padding * 0.7f);
            _title.rectTransform.sizeDelta = new Vector2(PanelWidth - Padding * 2f, TitleHeight);

            // close button (top-right X)
            var closeObj = new GameObject("Close", typeof(RectTransform));
            closeObj.transform.SetParent(panelObject.transform, false);
            var closeRect = (RectTransform)closeObj.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-Padding * 0.7f, -Padding * 0.7f);
            closeRect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            var closeImage = closeObj.AddComponent<Image>();
            closeImage.sprite = _fillSprite;
            closeImage.type = Image.Type.Sliced;
            closeImage.color = new Color(1f, 1f, 1f, 0.10f);
            var closeText = CreateText(closeObj, "X", 22f);
            closeText.text = "<b>✕</b>";
            closeText.rectTransform.anchorMin = Vector2.zero;
            closeText.rectTransform.anchorMax = Vector2.one;
            closeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            closeText.rectTransform.offsetMin = Vector2.zero;
            closeText.rectTransform.offsetMax = Vector2.zero;
            closeText.alignment = TextAlignmentOptions.Center;
            var closeButton = closeObj.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(Close);

            // search box
            var searchObj = new GameObject("Search", typeof(RectTransform));
            searchObj.transform.SetParent(panelObject.transform, false);
            var searchRect = (RectTransform)searchObj.transform;
            searchRect.anchorMin = new Vector2(0f, 1f);
            searchRect.anchorMax = new Vector2(0f, 1f);
            searchRect.pivot = new Vector2(0f, 1f);
            searchRect.anchoredPosition = new Vector2(Padding, -(Padding * 0.7f + TitleHeight + 6f));
            searchRect.sizeDelta = new Vector2(PanelWidth - Padding * 2f - ButtonSize - 8f, SearchHeight);

            var searchImage = searchObj.AddComponent<Image>();
            searchImage.sprite = _fillSprite;
            searchImage.type = Image.Type.Sliced;
            searchImage.color = new Color(1f, 1f, 1f, 0.06f);

            var inputObj = new GameObject("Input", typeof(RectTransform));
            inputObj.transform.SetParent(searchObj.transform, false);
            var inputRect = (RectTransform)inputObj.transform;
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.pivot = new Vector2(0f, 0.5f);
            inputRect.offsetMin = new Vector2(10f, 3f);
            inputRect.offsetMax = new Vector2(-10f, -3f);
            _search = inputObj.AddComponent<TMP_InputField>();

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(inputObj.transform, false);
            var textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.font = _font;
            textComp.fontSize = 20f;
            textComp.color = new Color(0.95f, 0.97f, 1f, 1f);
            textComp.textWrappingMode = TextWrappingModes.NoWrap;
            textComp.overflowMode = TextOverflowModes.Overflow;
            textComp.raycastTarget = true;
            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = Vector2.zero;
            _search.textComponent = textComp;

            var placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObj.transform.SetParent(inputObj.transform, false);
            var phComp = placeholderObj.AddComponent<TextMeshProUGUI>();
            phComp.font = _font;
            phComp.fontSize = 20f;
            phComp.color = new Color(0.55f, 0.60f, 0.68f, 1f);
            phComp.textWrappingMode = TextWrappingModes.NoWrap;
            phComp.text = JeI18n.Get("previewer.searchPlaceholder");
            phComp.raycastTarget = false;
            _searchPlaceholder = phComp;
            var phRect = (RectTransform)placeholderObj.transform;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.pivot = new Vector2(0f, 0.5f);
            phRect.offsetMin = new Vector2(4f, 0f);
            phRect.offsetMax = Vector2.zero;
            _search.placeholder = phComp;
            // fontAsset must be assigned only AFTER textComponent/placeholder exist:
            // TMP's SetGlobalFontAsset dereferences them and NREs otherwise.
            if (_font != null)
                _search.fontAsset = _font;

            // summary line
            _summary = CreateText(panelObject, "Summary", 19f);
            _summary.rectTransform.anchoredPosition = new Vector2(Padding, -(Padding * 0.7f + TitleHeight + 6f + SearchHeight + 8f));
            _summary.rectTransform.sizeDelta = new Vector2(PanelWidth - Padding * 2f, 24f);

            // scroll viewport
            var viewportObj = new GameObject("Viewport", typeof(RectTransform));
            viewportObj.transform.SetParent(panelObject.transform, false);
            _viewportRect = (RectTransform)viewportObj.transform;
            SetTopLeft(_viewportRect);
            viewportObj.AddComponent<RectMask2D>();
            var viewportY = Padding * 0.7f + TitleHeight + 6f + SearchHeight + 8f + 24f + 10f;
            _viewportRect.anchoredPosition = new Vector2(Padding, -viewportY);
            var bottomArea = ButtonSize + Padding * 1.2f;
            var viewportH = PanelHeight - viewportY - bottomArea;
            _viewportRect.sizeDelta = new Vector2(PanelWidth - Padding * 2f - ScrollbarWidth - 8f, viewportH);
            _rowWidth = _viewportRect.sizeDelta.x;

            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            _contentRect = (RectTransform)contentObj.transform;
            SetTopLeft(_contentRect);

            // vertical scrollbar (right of the viewport)
            var trackObj = new GameObject("Scrollbar", typeof(RectTransform));
            trackObj.transform.SetParent(panelObject.transform, false);
            var trackRect = (RectTransform)trackObj.transform;
            SetTopLeft(trackRect);
            trackRect.anchoredPosition = new Vector2(Padding + _viewportRect.sizeDelta.x + 4f, -viewportY);
            trackRect.sizeDelta = new Vector2(ScrollbarWidth, viewportH);
            var trackImage = trackObj.AddComponent<Image>();
            trackImage.sprite = _fillSprite;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(1f, 1f, 1f, 0.10f);
            _scrollbarTrack = trackRect;

            var handleObj = new GameObject("Handle", typeof(RectTransform));
            handleObj.transform.SetParent(trackObj.transform, false);
            _scrollbarHandle = (RectTransform)handleObj.transform;
            SetTopLeft(_scrollbarHandle);
            var handleImage = handleObj.AddComponent<Image>();
            handleImage.sprite = _fillSprite;
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(1f, 1f, 1f, 0.55f);
            _scrollbarDrag = handleObj.AddComponent<ScrollbarDrag>();
            _scrollbarDrag.Setup(trackRect);
            _scrollbarHandle.sizeDelta = new Vector2(ScrollbarWidth, viewportH);

            // export button (bottom-right)
            var exportObj = new GameObject("Export", typeof(RectTransform));
            exportObj.transform.SetParent(panelObject.transform, false);
            var exportRect = (RectTransform)exportObj.transform;
            exportRect.anchorMin = new Vector2(1f, 0f);
            exportRect.anchorMax = new Vector2(1f, 0f);
            exportRect.pivot = new Vector2(1f, 0f);
            exportRect.anchoredPosition = new Vector2(-Padding * 0.7f, Padding * 0.7f);
            exportRect.sizeDelta = new Vector2(180f, ButtonSize);
            var exportImage = exportObj.AddComponent<Image>();
            exportImage.sprite = _fillSprite;
            exportImage.type = Image.Type.Sliced;
            exportImage.color = new Color(0.16f, 0.55f, 0.38f, 0.9f);
            var exportText = CreateText(exportObj, "T", 19f);
            exportText.text = "<b>" + JeI18n.Get("previewer.export") + "</b>";
            _translatedLabels.Add((exportText, "previewer.export"));
            exportText.rectTransform.anchorMin = Vector2.zero;
            exportText.rectTransform.anchorMax = Vector2.one;
            exportText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            exportText.rectTransform.offsetMin = Vector2.zero;
            exportText.rectTransform.offsetMax = Vector2.zero;
            exportText.alignment = TextAlignmentOptions.Center;
            var exportButton = exportObj.AddComponent<Button>();
            exportButton.targetGraphic = exportImage;
            exportButton.onClick.AddListener(ToggleExportMenu);

            // export format menu (pops up above the export button)
            var menuObj = new GameObject("ExportMenu", typeof(RectTransform));
            menuObj.transform.SetParent(panelObject.transform, false);
            var menuRect = (RectTransform)menuObj.transform;
            menuRect.anchorMin = new Vector2(1f, 0f);
            menuRect.anchorMax = new Vector2(1f, 0f);
            menuRect.pivot = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(-Padding * 0.7f, Padding * 0.7f + ButtonSize + 10f);
            menuRect.sizeDelta = new Vector2(280f, 158f);
            var menuBg = menuObj.AddComponent<Image>();
            menuBg.sprite = _fillSprite;
            menuBg.type = Image.Type.Sliced;
            menuBg.color = new Color(0.020f, 0.028f, 0.042f, 0.98f);

            var menuTitle = CreateText(menuObj, "Title", 18f);
            menuTitle.text = "<b>" + JeI18n.Get("previewer.exportFormat") + "</b>";
            _translatedLabels.Add((menuTitle, "previewer.exportFormat"));
            menuTitle.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            menuTitle.rectTransform.sizeDelta = new Vector2(252f, 24f);

            MakeMenuButton(menuObj, "Yaml", 14f, -40f, 252f, 42f, "previewer.exportYaml", OnExportYamlClicked);
            MakeMenuButton(menuObj, "Chart", 14f, -90f, 252f, 42f, "previewer.exportChart", ShowSeriesMenu);

            menuObj.SetActive(false);
            _exportMenu = menuObj;

            // chart series menu (second level: checkbox list, default all checked)
            var seriesList = new List<(string Id, string I18nKey, string Color, bool Right)>();
            foreach (var s in ReportExporter.ChartSeries)
            {
                if (s.Id.StartsWith("nea", StringComparison.Ordinal) && !NeaLink.Available)
                    continue;
                seriesList.Add(s);
            }
            var seriesMenuH = 12f + 24f + 8f + seriesList.Count * 32f + 10f + 42f + 14f;
            var seriesMenuObj = new GameObject("SeriesMenu", typeof(RectTransform));
            seriesMenuObj.transform.SetParent(panelObject.transform, false);
            var seriesRect = (RectTransform)seriesMenuObj.transform;
            seriesRect.anchorMin = new Vector2(1f, 0f);
            seriesRect.anchorMax = new Vector2(1f, 0f);
            seriesRect.pivot = new Vector2(1f, 0f);
            seriesRect.anchoredPosition = new Vector2(-Padding * 0.7f, Padding * 0.7f + ButtonSize + 10f);
            seriesRect.sizeDelta = new Vector2(300f, seriesMenuH);
            var seriesBg = seriesMenuObj.AddComponent<Image>();
            seriesBg.sprite = _fillSprite;
            seriesBg.type = Image.Type.Sliced;
            seriesBg.color = new Color(0.020f, 0.028f, 0.042f, 0.98f);

            var seriesTitle = CreateText(seriesMenuObj, "Title", 18f);
            seriesTitle.text = "<b>" + JeI18n.Get("previewer.exportSeries") + "</b>";
            _translatedLabels.Add((seriesTitle, "previewer.exportSeries"));
            seriesTitle.rectTransform.anchoredPosition = new Vector2(14f, -10f);
            seriesTitle.rectTransform.sizeDelta = new Vector2(272f, 24f);

            var rowY = -44f;
            foreach (var s in seriesList)
            {
                MakeSeriesRow(seriesMenuObj, rowY, s.Id, s.I18nKey, JeI18n.Get(s.I18nKey), s.Color);
                rowY -= 32f;
            }

            var buttonsY = -(seriesMenuH - 12f - 42f);
            MakeMenuButton(seriesMenuObj, "Cancel", 14f, buttonsY, 133f, 42f,
                "previewer.exportCancel", HideSeriesMenu);
            MakeMenuButton(seriesMenuObj, "Confirm", 153f, buttonsY, 133f, 42f,
                "previewer.exportConfirm", OnExportChartClicked);

            seriesMenuObj.SetActive(false);
            _seriesMenu = seriesMenuObj;

            Main.Handler?.Log($"[JEA][Previewer] EnsureUI done: host={host.name}, hostRenderMode={host.renderMode}, hostSortingLayer={host.sortingLayerName}, hostSortingOrder={host.sortingOrder}");
        }

        private static Canvas? ResolveHostCanvas()
        {
            var ctl = scrController.instance;
            if (ctl != null && ctl.detailedResults != null)
            {
                var textCanvas = ctl.detailedResults.textComponent?.canvas;
                if (textCanvas != null)
                    return textCanvas;
            }
            if (scrUIController.instance != null && scrUIController.instance.canvas != null)
                return scrUIController.instance.canvas;
            return null;
        }

        private static void MakeMenuButton(GameObject menu, string name, float x, float y, float width, float height,
            string i18nKey, Action onClick)
        {
            var btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(menu.transform, false);
            var rect = (RectTransform)btnObj.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var image = btnObj.AddComponent<Image>();
            image.sprite = _fillSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.10f);
            var label2 = CreateText(btnObj, "T", 17f);
            label2.text = "<b>" + JeI18n.Get(i18nKey) + "</b>";
            _translatedLabels.Add((label2, i18nKey));
            label2.rectTransform.anchorMin = Vector2.zero;
            label2.rectTransform.anchorMax = Vector2.one;
            label2.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label2.rectTransform.offsetMin = Vector2.zero;
            label2.rectTransform.offsetMax = Vector2.zero;
            label2.alignment = TextAlignmentOptions.Center;
            var button = btnObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
        }

        private static void ToggleExportMenu()
        {
            if (_exportMenu == null) return;
            _exportMenu.SetActive(!_exportMenu.activeSelf);
        }

        private static void MakeSeriesRow(GameObject menu, float y, string id, string i18nKey, string label, string colorHex)
        {
            var rowObj = new GameObject("Row_" + id, typeof(RectTransform));
            rowObj.transform.SetParent(menu.transform, false);
            var rect = (RectTransform)rowObj.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, y);
            rect.sizeDelta = new Vector2(276f, 30f);

            var rowBg = rowObj.AddComponent<Image>();
            rowBg.sprite = _fillSprite;
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(1f, 1f, 1f, 0.04f);

            var boxObj = new GameObject("Box", typeof(RectTransform));
            boxObj.transform.SetParent(rowObj.transform, false);
            var boxRect = (RectTransform)boxObj.transform;
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(10f, 0f);
            boxRect.sizeDelta = new Vector2(16f, 16f);
            var box = boxObj.AddComponent<Image>();
            box.sprite = _fillSprite;
            box.type = Image.Type.Sliced;
            ColorUtility.TryParseHtmlString(colorHex, out var seriesColor);

            var label2 = CreateText(rowObj, "T", 16f);
            label2.text = label;
            _translatedLabels.Add((label2, i18nKey));
            label2.rectTransform.anchorMin = new Vector2(0f, 0f);
            label2.rectTransform.anchorMax = new Vector2(1f, 1f);
            label2.rectTransform.pivot = new Vector2(0f, 0.5f);
            label2.rectTransform.offsetMin = new Vector2(36f, 0f);
            label2.rectTransform.offsetMax = new Vector2(-8f, 0f);
            label2.alignment = TextAlignmentOptions.MidlineLeft;

            void ApplyState()
            {
                box.color = _chartSeriesOff.Contains(id)
                    ? new Color(0.16f, 0.19f, 0.25f, 0.9f)
                    : seriesColor;
            }

            ApplyState();
            var button = rowObj.AddComponent<Button>();
            button.targetGraphic = rowBg;
            button.onClick.AddListener(() =>
            {
                if (!_chartSeriesOff.Remove(id))
                    _chartSeriesOff.Add(id);
                ApplyState();
            });
        }

        private static void ShowSeriesMenu()
        {
            if (_exportMenu != null) _exportMenu.SetActive(false);
            if (_seriesMenu != null) _seriesMenu.SetActive(true);
        }

        private static void HideSeriesMenu()
        {
            if (_seriesMenu != null) _seriesMenu.SetActive(false);
        }

        private static void OnExportYamlClicked()
        {
            if (_exportMenu != null) _exportMenu.SetActive(false);
            Export(ReportExporter.WriteYaml);
        }

        private static void OnExportChartClicked()
        {
            HideSeriesMenu();
            // Empty "off" set → null → exporter draws every series (future ones default on)
            ICollection<string>? selected = null;
            if (_chartSeriesOff.Count > 0)
            {
                selected = new List<string>();
                foreach (var s in ReportExporter.ChartSeries)
                    if (!_chartSeriesOff.Contains(s.Id))
                        selected.Add(s.Id);
            }
            Export((level, dir) => ReportExporter.WriteChartHtml(level, dir, selected));
        }

        private static void Export(Func<string, string, string> writer)
        {
            try
            {
                var level = ADOBase.customLevel?.levelData?.fullCaption
                            ?? ADOBase.customLevel?.levelData?.song
                            ?? "unknown";
                var dir = Main.Handler != null ? System.IO.Path.Combine(Main.Handler.ModPath, "reports") : System.IO.Path.GetTempPath();
                var path = writer(level, dir);
                Main.Handler?.Log($"[JEA] report exported to {path}");
                if (_title != null)
                    _title.text = $"{TitleMarkup}  <color=#7CE0B3>✓ {System.IO.Path.GetFileName(path)}</color>";
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA] export failed: {ex}");
                if (_title != null)
                    _title.text = $"{TitleMarkup}  <color=#FF6B6B>✗ export failed</color>";
            }
        }

        private static RowWidget GetRow(int index)
        {
            while (Rows.Count <= index)
            {
                var rowObject = new GameObject("Row_" + Rows.Count.ToString(CultureInfo.InvariantCulture), typeof(RectTransform));
                rowObject.transform.SetParent(_contentRect, false);
                var rect = (RectTransform)rowObject.transform;
                SetTopLeft(rect);
                rect.anchoredPosition = new Vector2(0f, -Rows.Count * RowHeight);
                rect.sizeDelta = new Vector2(_rowWidth, RowHeight);

                var rowImage = rowObject.AddComponent<Image>();
                rowImage.sprite = _fillSprite;
                rowImage.type = Image.Type.Sliced;
                rowImage.color = new Color(1f, 1f, 1f, 0.03f);
                var button = rowObject.AddComponent<Button>();
                button.targetGraphic = rowImage;

                // separator: a thin line under every hit so rows read as one block each.
                var sep = new GameObject("Separator", typeof(RectTransform));
                sep.transform.SetParent(rowObject.transform, false);
                var sepRect = (RectTransform)sep.transform;
                sepRect.anchorMin = new Vector2(0f, 0f);
                sepRect.anchorMax = new Vector2(1f, 0f);
                sepRect.pivot = new Vector2(0.5f, 0f);
                sepRect.offsetMin = Vector2.zero;
                sepRect.offsetMax = new Vector2(0f, SeparatorHeight);
                var sepImage = sep.AddComponent<Image>();
                sepImage.sprite = _fillSprite;
                sepImage.type = Image.Type.Sliced;
                sepImage.color = new Color(1f, 1f, 1f, 0.10f);
                sepImage.raycastTarget = false;

                var text = CreateText(rowObject, "Text", 17f);
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.offsetMin = new Vector2(4f, 0f);
                textRect.offsetMax = Vector2.zero;
                text.alignment = TextAlignmentOptions.MidlineLeft;

                rowObject.SetActive(false);
                Rows.Add(new RowWidget { Root = rowObject, Rect = rect, Text = text, Button = button });
            }
            return Rows[index];
        }

        private static void SetTopLeft(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }

        private static Image CreateChildImage(GameObject parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(GameObject parent, string name, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            SetTopLeft((RectTransform)go.transform);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.richText = true;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color(0.95f, 0.97f, 1f, 1f);
            text.raycastTarget = false;
            if (_font != null)
                text.font = _font;
            return text;
        }

        private static void EnsureSprites()
        {
            if (_fillSprite != null)
                return;

            const int size = 64;
            const float radius = 14f;
            _fillTexture = CreateRoundedTexture(size, radius, 0f);
            _ringTexture = CreateRoundedTexture(size, radius, 3f);
            var border = new Vector4(radius + 4f, radius + 4f, radius + 4f, radius + 4f);
            _fillSprite = Sprite.Create(_fillTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            _ringSprite = Sprite.Create(_ringTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Texture2D CreateRoundedTexture(int size, float radius, float ringThickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var half = size * 0.5f;
            var box = half - radius - 1.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Abs(x + 0.5f - half) - box;
                    var dy = Mathf.Abs(y + 0.5f - half) - box;
                    var ax = Mathf.Max(dx, 0f);
                    var ay = Mathf.Max(dy, 0f);
                    var dist = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                    var alpha = ringThickness <= 0f
                        ? Mathf.Clamp01(0.5f - dist)
                        : Mathf.Clamp01(Mathf.Min(0.5f - dist, dist + ringThickness + 0.5f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
