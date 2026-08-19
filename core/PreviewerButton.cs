using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// A persistent JEA icon button parented directly to the official difficulty
    /// selector so it follows the selector's animations for free. Created by
    /// Harmony patches on EditorDifficultySelector (editor) and
    /// scrUIController.ShowDifficultyContainer (game).
    /// </summary>
    public static class PreviewerButton
    {
        private static GameObject? _root;
        private static Button? _button;
        private static Sprite? _fillSprite;

        public static void EnsureAttached(RectTransform selectorRect)
        {
            if (!Main.Settings.Enabled || selectorRect == null) return;
            // selectorRect = selectorRectTransform (always visible in edit mode).
            // Parent to its canvas so the button is NOT dragged by DOAnchorPosY.
            var canvas = selectorRect.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRect = (RectTransform)canvas.transform;
            EnsureCreated(canvasRect);
            if (_root == null) return;
            if (_root.transform.parent != canvasRect)
                _root.transform.SetParent(canvasRect, false);
            PositionLeftOf(selectorRect, canvasRect);
            _root.SetActive(true);
        }

        public static void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        public static void OnUpdate()
        {
            if (!Main.Settings.Enabled || _root == null) { Hide(); return; }
            // Visibility only — never reposition here, because selectorRectTransform
            // animates (DOAnchorPosY) and we'd follow it down.
        }

        private static void PositionLeftOf(RectTransform selectorRect, RectTransform canvasRect)
        {
            if (_root == null) return;
            try
            {
                var corners = new Vector3[4];
                selectorRect.GetWorldCorners(corners);
                var leftCenter = (corners[0] + corners[1]) * 0.5f;
                var screenPos = RectTransformUtility.WorldToScreenPoint(null, leftCenter);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, screenPos, null, out var local))
                    return;
                var btnRect = (RectTransform)_root.transform;
                var half = btnRect.sizeDelta.x * 0.5f;
                btnRect.anchorMin = new Vector2(0.5f, 0.5f);
                btnRect.anchorMax = new Vector2(0.5f, 0.5f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.anchoredPosition = new Vector2(local.x - half - 8f, local.y);
            }
            catch { }
        }

        private static RectTransform? ResolveAnchor()
        {
            if (ADOBase.isLevelEditor && scnEditor.instance != null)
            {
                var sel = scnEditor.instance.editorDifficultySelector;
                if (sel != null && sel.selectorRectTransform != null)
                    return sel.selectorRectTransform;
            }
            var ui = scrUIController.instance;
            if (ui != null && ui.difficultyContainer != null && ui.difficultyContainer.gameObject.activeInHierarchy)
                return ui.difficultyContainer;
            return null;
        }

        private static void EnsureCreated(RectTransform parent)
        {
            if (_root != null) return;

            EnsureSprite();

            _root = new GameObject("JEA_PreviewerBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            _root.transform.SetParent(parent, false);

            var btnRect = (RectTransform)_root.transform;
            btnRect.sizeDelta = new Vector2(44f, 44f);

            var bg = _root.GetComponent<Image>();
            bg.sprite = _fillSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.16f, 0.55f, 0.38f, 0.9f);

            var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(TextMeshProUGUI));
            iconObj.transform.SetParent(_root.transform, false);
            var iconRect = (RectTransform)iconObj.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var icon = iconObj.GetComponent<TextMeshProUGUI>();
            var font = JeFont.Get();
            icon.font = font;
            icon.text = "<b>JEA</b>";
            icon.fontSize = 15f;
            icon.alignment = TextAlignmentOptions.Center;
            icon.color = Color.white;

            _button = _root.GetComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(() =>
            {
                Main.Handler?.Log("[JEA][PreviewerBtn] toggled");
                if (JePreviewer.IsOpen) JePreviewer.Close();
                else JePreviewer.Open();
            });

            Main.Handler?.Log("[JEA][PreviewerBtn] created");
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
