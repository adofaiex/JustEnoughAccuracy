using UnityEngine;
using UnityEngine.EventSystems;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Vertical scrollbar handle for the JEA previewer. Dragging (or clicking) the
    /// handle maps the pointer's position inside the track to a scroll fraction
    /// (0 = top, 1 = bottom) and hands it to <see cref="JePreviewer"/>.
    /// </summary>
    public sealed class ScrollbarDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        private RectTransform? _track;

        public void Setup(RectTransform track)
        {
            _track = track;
        }

        public void OnPointerDown(PointerEventData eventData) => Jump(eventData.position);

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) => Jump(eventData.position);

        private void Jump(Vector2 screenPos)
        {
            if (_track == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, screenPos, null, out var local))
                return;
            var trackH = _track.rect.height;
            var handleH = 0f;
            for (var i = 0; i < _track.childCount; i++)
            {
                var child = _track.GetChild(i) as RectTransform;
                if (child != null)
                {
                    handleH = child.rect.height;
                    break;
                }
            }
            var travel = Mathf.Max(trackH - handleH, 1f);
            var fracTop = Mathf.Clamp01((trackH * 0.5f - local.y) / travel);
            JePreviewer.OnScrollbarDragged(fracTop);
        }
    }
}