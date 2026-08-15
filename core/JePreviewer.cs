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

        private sealed class RowWidget
        {
            public GameObject Root;
            public RectTransform Rect;
            public TextMeshProUGUI Text;
            public JudgementRecord Record;
        }

        private static readonly List<RowWidget> Rows = new();
        private static readonly StringBuilder Builder = new(256);

        private static Canvas? _canvas;
        private static RectTransform? _panelRect;
        private static Image? _background;
        private static Image? _border;
        private static TextMeshProUGUI? _title;
        private static TMP_InputField? _search;
        private static RectTransform? _viewportRect;
        private static RectTransform? _contentRect;
        private static TextMeshProUGUI? _summary;
        private static Texture2D? _fillTexture;
        private static Texture2D? _ringTexture;
        private static Sprite? _fillSprite;
        private static Sprite? _ringSprite;
        private static TMP_FontAsset? _font;
        private static bool _fontLogged;

        private static int _visibleCount;
        private static float _maxScroll;
        private static float _scrollOffset;
        private static string _lastFilter = "";

        /// <summary>Whether the previewer overlay is currently open.</summary>
        public static bool IsOpen { get; private set; }

        // ---------- open / close ----------

        public static void Open()
        {
            EnsureUI();
            IsOpen = true;
            _scrollOffset = 0f;
            Refresh();
            _canvas!.gameObject.SetActive(true);
        }

        public static void Close()
        {
            IsOpen = false;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
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
                Refresh();
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
        }

        // ---------- rendering ----------

        private static void Refresh()
        {
            if (_search == null) return;
            var filter = _search.text;
            _lastFilter = filter;

            var records = JudgementRecorder.Snapshot();
            Builder.Length = 0;

            var matches = new List<JudgementRecord>(records.Count);
            foreach (var record in records)
            {
                if (MatchesFilter(record, filter))
                    matches.Add(record);
            }

            _visibleCount = matches.Count;

            // summary line
            Builder.Append("<color=#AEB8C4>JEA </color>")
                .Append("<b><color=#FFFFFFFF>")
                .Append((JeaScore.TotalScore).ToString(CultureInfo.InvariantCulture))
                .Append("</color></b>")
                .Append("  <color=#AEB8C4>|</color>  <color=#7CE0B3>")
                .Append((JeaScore.CachedAccuracy / 10000m).ToString("0.0000", CultureInfo.InvariantCulture))
                .Append("%</color>")
                .Append("  <color=#AEB8C4>|</color>  x")
                .Append(JeaScore.MaxCombo.ToString(CultureInfo.InvariantCulture))
                .Append("  <color=#AEB8C4>|</color>  ")
                .Append(matches.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" / ")
                .Append(records.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" <color=#5F6771>tiles</color>");
            _summary!.text = Builder.ToString();

            // rows
            var limit = Math.Min(matches.Count, 1000);
            for (var i = 0; i < limit; i++)
            {
                var row = GetRow(i);
                row.Record = matches[i];
                Builder.Length = 0;
                BuildRowText(Builder, matches[i]);
                row.Text.text = Builder.ToString();
                if (!row.Root.activeSelf)
                    row.Root.SetActive(true);
            }
            HideUnusedRows(limit);

            _contentRect!.sizeDelta = new Vector2(0f, limit * RowHeight);
            var viewportH = _viewportRect!.rect.height;
            _maxScroll = Mathf.Max(0f, limit * RowHeight - viewportH);
            _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, _maxScroll);
            _contentRect.anchoredPosition = new Vector2(0f, _scrollOffset);
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
                .Append(record.JeaTileScore.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(record.JeaFinalTileScore.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(record.JeaTotalScore.ToString(CultureInfo.InvariantCulture)).Append(' ')
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

            sb.Append("<color=#5F6771>#")
                .Append(r.Tile.ToString(CultureInfo.InvariantCulture))
                .Append("</color>  <color=#AEB8C4>")
                .Append(ReportExporter.FormatTimestampPublic(r.Timestamp))
                .Append("</color>  <b><color=")
                .Append(marginColor)
                .Append(">")
                .Append(r.Margin)
                .Append("</color></b>  <color=#AEB8C4>JEA </color><b><color=#FFFFFFFF>")
                .Append(r.JeaTileScore.ToString(CultureInfo.InvariantCulture))
                .Append("</color></b> <color=#5F6771>x")
                .Append(r.Combo.ToString(CultureInfo.InvariantCulture))
                .Append(" → </color><b><color=#7CE0B3>")
                .Append(r.JeaFinalTileScore.ToString(CultureInfo.InvariantCulture))
                .Append("</color></b>  <color=#AEB8C4>Acc ")
                .Append((r.Acc * 100f).ToString("0.00", CultureInfo.InvariantCulture))
                .Append("% / X-Acc ")
                .Append((r.XAcc * 100f).ToString("0.00", CultureInfo.InvariantCulture))
                .Append("%</color>");

            if (r.OfficialScore != null)
                sb.Append("  <color=#AEB8C4>Off </color><b><color=#F3D98B>")
                    .Append(r.OfficialScore.Value.ToString(CultureInfo.InvariantCulture))
                    .Append("</color></b>");

            if (r.NeaScore != null)
                sb.Append("  <color=#AEB8C4>NEA </color><b><color=#8AA7FF>")
                    .Append(r.NeaScore.Value.ToString(CultureInfo.InvariantCulture))
                    .Append("</color></b>");

            sb.Append("  <color=#5F6771>")
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

            EnsureSprites();
            _font = JeFont.Get();

            var canvasObject = new GameObject("JEA_Previewer");
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

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

            _border = CreateChildImage(panelObject, "Border", _ringSprite, new Color(1f, 1f, 1f, 0.90f));
            _border.type = Image.Type.Sliced;
            var borderRect = (RectTransform)_border.transform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.pivot = new Vector2(0.5f, 0.5f);
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            // title
            _title = CreateText(panelObject, "Title", 24f);
            _title.text = "<b><color=#FFFFFFFF>JEA</color><color=#7CE0B3>.Previewer</color></b>";
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
            if (_font != null)
                _search.fontAsset = _font;

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
            phComp.text = "搜索 / Search…";
            phComp.raycastTarget = false;
            var phRect = (RectTransform)placeholderObj.transform;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.pivot = new Vector2(0f, 0.5f);
            phRect.offsetMin = new Vector2(4f, 0f);
            phRect.offsetMax = Vector2.zero;
            _search.placeholder = phComp;

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
            _viewportRect.sizeDelta = new Vector2(PanelWidth - Padding * 2f, PanelHeight - viewportY - bottomArea);

            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            _contentRect = (RectTransform)contentObj.transform;
            SetTopLeft(_contentRect);

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
            exportText.text = "<b>导出报告 / Export</b>";
            exportText.rectTransform.anchorMin = Vector2.zero;
            exportText.rectTransform.anchorMax = Vector2.one;
            exportText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            exportText.rectTransform.offsetMin = Vector2.zero;
            exportText.rectTransform.offsetMax = Vector2.zero;
            exportText.alignment = TextAlignmentOptions.Center;
            var exportButton = exportObj.AddComponent<Button>();
            exportButton.targetGraphic = exportImage;
            exportButton.onClick.AddListener(OnExportClicked);

            _canvas.gameObject.SetActive(false);
        }

        private static void OnExportClicked()
        {
            try
            {
                var level = ADOBase.customLevel?.levelData?.fullCaption
                            ?? ADOBase.customLevel?.levelData?.song
                            ?? "unknown";
                var dir = Main.Handler != null ? System.IO.Path.Combine(Main.Handler.ModPath, "reports") : System.IO.Path.GetTempPath();
                var path = ReportExporter.WriteReport(level, dir);
                Main.Handler?.Log($"[JEA] report exported to {path}");
                if (_title != null)
                    _title.text = $"<b><color=#FFFFFFFF>JEA</color><color=#7CE0B3>.Previewer</color></b>  <color=#7CE0B3>✓ {System.IO.Path.GetFileName(path)}</color>";
            }
            catch (Exception ex)
            {
                Main.Handler?.Error($"[JEA] export failed: {ex}");
                if (_title != null)
                    _title.text = $"<b><color=#FFFFFFFF>JEA</color><color=#7CE0B3>.Previewer</color></b>  <color=#FF6B6B>✗ export failed</color>";
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

                var text = CreateText(rowObject, "Text", 17f);
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.offsetMin = new Vector2(4f, 0f);
                textRect.offsetMax = Vector2.zero;
                text.alignment = TextAlignmentOptions.MidlineLeft;

                rowObject.SetActive(false);
                Rows.Add(new RowWidget { Root = rowObject, Rect = rect, Text = text });
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
