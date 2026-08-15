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
            _canvas!.gameObject.SetActive(true);
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
            _canvas.sortingOrder = 29000;
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
