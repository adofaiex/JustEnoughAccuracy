using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughAccuracy
{
    public static class ResultsScreenButton
    {
        private static GameObject? _root;
        private static Button? _button;

        public static void Show()
        {
            EnsureUI();
            if (_root == null) return;
            _root.SetActive(true);
        }

        public static void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _button = null;
            }
        }

        private static void EnsureUI()
        {
            if (_root != null) return;

            var ctl = scrController.instance;
            if (ctl?.detailedResults?.textComponent == null) return;

            // Parent to the text's parent so we share the same layout coordinate space.
            var textGO = ctl.detailedResults.textComponent.gameObject;
            var parent = textGO.transform.parent;
            if (parent == null) return;

            _root = new GameObject("JEA_ResultsButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _root.transform.SetParent(parent, false);
            _root.SetActive(false);

            var btnRect = (RectTransform)_root.transform;
            btnRect.anchorMin = new Vector2(0.5f, 1f);
            btnRect.anchorMax = new Vector2(0.5f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.anchoredPosition = new Vector2(0f, -8f);
            btnRect.sizeDelta = new Vector2(220f, 48f);

            var bg = _root.GetComponent<Image>();
            bg.color = new Color(0.16f, 0.55f, 0.38f, 0.9f);

            var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(_root.transform, false);
            var tr = (RectTransform)textObj.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.font = JeFont.Get();
            text.text = "<b>浏览详细信息</b>";
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            _button = _root.GetComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(() => 
            {
                Main.Handler?.Log("[JEA][Btn] Button clicked!");
                JePreviewer.Open();
            });
        }
    }
}
