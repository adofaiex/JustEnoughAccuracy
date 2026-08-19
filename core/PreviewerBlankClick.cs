using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Attached to the JEA previewer panel background. A click that lands on the
    /// panel itself (not on a row / button / input) clears the current judgement
    /// selection. Because child widgets are separate raycast targets, they swallow
    /// their own clicks; this fires only for blank panel space.
    /// </summary>
    public sealed class PreviewerBlankClick : MonoBehaviour, IPointerClickHandler
    {
        private Action? _onBlankClick;

        /// <summary>Register the callback fired when blank panel space is clicked.</summary>
        public void Setup(Action onBlankClick)
        {
            _onBlankClick = onBlankClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onBlankClick?.Invoke();
        }
    }
}
