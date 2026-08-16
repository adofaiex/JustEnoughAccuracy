using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughAccuracy
{
    public static class ResultsScreenButton
    {
        private static Canvas? _canvas;
        private static GameObject? _root;
        private static Button? _button;
        private static Sprite? _fillSprite;
        private static TextMeshProUGUI? _label;
        private static int _languageVersion = -1;

        public static void Show()
        {
            EnsureUI();
            if (_canvas == null || _root == null) return;
            ApplyTranslations();
            // The game may have added siblings after us; staying last keeps us
            // rendering after the results text within the same canvas.
            _canvas.transform.SetAsLastSibling();
            PositionNearResultsText();
            _canvas.gameObject.SetActive(true);
            _root.SetActive(true);
        }

        /// <summary>Re-applies localized text when the language changed.</summary>
        private static void ApplyTranslations()
        {
            if (_label == null || JeI18n.LanguageVersion == _languageVersion)
                return;
            _languageVersion = JeI18n.LanguageVersion;
            _label.text = "<b>" + JeI18n.Get("results.button") + "</b>";
        }

        public static void Hide()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        private static void PositionNearResultsText()
        {
            if (_root == null || _canvas == null) return;
            try
            {
                var textRect = scrController.instance?.detailedResults?.textComponent?.rectTransform;
                if (textRect == null) return;
                var canvasRect = _canvas.GetComponent<RectTransform>();

                var corners = new Vector3[4];
                textRect.GetWorldCorners(corners);
                var bottomCenter = (corners[0] + corners[3]) * 0.5f;
                var screenPos = RectTransformUtility.WorldToScreenPoint(null, bottomCenter);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local))
                    return;

                var btnRect = (RectTransform)_root.transform;
                btnRect.anchoredPosition = new Vector2(local.x, local.y - btnRect.sizeDelta.y * 0.5f - 10f);
            }
            catch
            {
            }
        }

        private static void EnsureUI()
        {
            if (_root != null) return;

            EnsureSprite();

            var host = ResolveHostCanvas();
            if (host == null)
            {
                Main.Handler?.Log("[JEA][Btn] EnsureUI: no host canvas");
                return;
            }

            var canvasObj = new GameObject("JEA_ResultsButton");
            canvasObj.transform.SetParent(host.transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            // High enough to beat every other canvas, but not exactly int.MaxValue
            // (which wraps negative if anything increments it). 30000 was verified
            // too low — a fullscreen canvas above it hid the button entirely.
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 2000000000;
            canvasObj.AddComponent<GraphicRaycaster>();
            // Stretch to fill the host canvas so positioning math is in canvas space.
            // (Canvas requires a RectTransform, so this cast is only valid after
            // AddComponent<Canvas>; doing it earlier throws InvalidCastException.)
            var canvasRect = (RectTransform)canvasObj.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasObj.transform.SetAsLastSibling();

            _root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            _root.transform.SetParent(canvasObj.transform, false);
            _root.SetActive(false);

            var btnRect = (RectTransform)_root.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(220f, 48f);

            var bg = _root.GetComponent<Image>();
            bg.sprite = _fillSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.16f, 0.55f, 0.38f, 0.9f);

            var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(_root.transform, false);
            var tr = (RectTransform)textObj.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            _label = textObj.GetComponent<TextMeshProUGUI>();
            var font = JeFont.Get();
            _label.font = font;
            _label.text = "<b>" + JeI18n.Get("results.button") + "</b>";
            _label.fontSize = 22f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            _button = _root.GetComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(() =>
            {
                Main.Handler?.Log("[JEA][Btn] Button clicked!");
                JePreviewer.Open();
            });

            Main.Handler?.Log($"[JEA][Btn] EnsureUI done: sprite={(_fillSprite != null)}, font={(font != null ? font.name : "null")}, host={host.name}, hostSorting={host.sortingOrder}, hostOverride={host.overrideSorting}, renderMode={host.renderMode}, sortingLayer={host.sortingLayerName}");

            var textCanvas = scrController.instance?.detailedResults?.textComponent?.canvas;
            var textGO = scrController.instance?.detailedResults?.textComponent?.gameObject;
            var textHasCanvas = textGO != null && textGO.GetComponent<Canvas>() != null;
            Main.Handler?.Log($"[JEA][Btn] textCanvas={textCanvas?.name}, textGO={textGO?.name}, textHasOwnCanvas={textHasCanvas}, textCanvasSorting={textCanvas?.sortingOrder}, textRenderMode={textCanvas?.renderMode}, textSortingLayer={textCanvas?.sortingLayerName}");
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

        private static void EnsureSprite()
        {
            if (_fillSprite != null) return;

            const int size = 64;
            const float radius = 14f;
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
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist)));
                }
            }
            texture.Apply();
            var border = new Vector4(radius + 4f, radius + 4f, radius + 4f, radius + 4f);
            _fillSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
