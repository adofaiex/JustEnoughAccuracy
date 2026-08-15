using UnityEngine;
using UnityEngine.EventSystems;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Lets the JEA previewer panel be dragged by its background. The drag moves
    /// the panel's anchored position; because the previewer canvas uses a
    /// CanvasScaler, the pointer delta is divided by its scaleFactor to keep the
    /// motion 1:1 with the cursor on screen.
    /// </summary>
    public sealed class PanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
    {
        private RectTransform? _target;
        private Canvas? _canvas;

        /// <summary>Attach to the drag surface (e.g. the panel background).</summary>
        public void Setup(RectTransform target, Canvas canvas)
        {
            _target = target;
            _canvas = canvas;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Bring the panel along with the click; nothing else needed here.
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Snapshot the panel position so dragging starts from it.
            if (_target != null)
                _dragStartPos = _target.anchoredPosition;
            _dragStartPointer = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null) return;

            var scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var delta = (eventData.position - _dragStartPointer) / scale;
            // The panel lives in a canvas whose root anchor is (0.5, 0.5).
            _target.anchoredPosition = _dragStartPos + delta;
        }

        private Vector2 _dragStartPos;
        private Vector2 _dragStartPointer;
    }
}