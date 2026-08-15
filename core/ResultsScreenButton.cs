using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// The "浏览详细信息" button shown alongside the official results screen.
    /// Clicking opens the <see cref="JePreviewer"/>. Hidden when results hide.
    /// </summary>
    public static class ResultsScreenButton
    {
        private static Canvas? _canvas;
        private static Button? _button;

        public static void Show()
        {
            EnsureUI();
            PositionNearResultsText();
            _canvas!.gameObject.SetActive(true);
        }

        /// <summary>
        /// Places the button right below the official results text so it sits
        /// next to the JEA summary line instead of floating at screen centre.
        /// </summary>
        private static void PositionNearResultsText()
        {
            if (_button == null || _canvas == null) return;

            var ctl = scrController.instance;
            if (ctl == null || ctl.detailedResults == null) return;

            var textRect = ctl.detailedResults.textComponent.rectTransform;
            if (textRect == null) return;

            // Bottom edge of the results text, in world space, then to screen.
            var corners = new Vector3[4];
            textRect.GetWorldCorners(corners); // 0 BL, 1 TL, 2 TR, 3 BR
            var bottomCenter = (corners[0] + corners[3]) * 0.5f;
            var screenPos = RectTransformUtility.WorldToScreenPoint(null, bottomCenter);

            // Screen point → this canvas' local coords (handles the scaler).
            var canvasRect = _canvas.GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var localPoint))
                return;

            var btnRect = _button.GetComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(localPoint.x, localPoint.y - 14f);
        }

        public static void Hide()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        private static void EnsureUI()
        {
            if (_canvas != null)
                return;

            var canvasObject = new GameObject("JEA_ResultsButton");
            Object.DontDestroyOnLoad(canvasObject);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 2147483647;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var btnObj = new GameObject("BrowseDetails", typeof(RectTransform));
            btnObj.transform.SetParent(canvasObject.transform, false);
            var btnRect = (RectTransform)btnObj.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0f, -220f);
            btnRect.sizeDelta = new Vector2(220f, 48f);

            var bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.55f, 0.38f, 0.9f);

            var textObj = new GameObject("Label", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.font = JeFont.Get();
            text.text = "<b>浏览详细信息</b>";
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            _button = btnObj.AddComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(JePreviewer.Open);

            _canvas.gameObject.SetActive(false);
        }
    }
}
