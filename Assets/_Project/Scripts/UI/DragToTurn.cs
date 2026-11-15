using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Turns something round as the pointer drags across this UI element: a model shown in a render texture (the character
/// window's portrait). Raises <see cref="Turned"/> with degrees; a drag to the right turns it to its left, as if the
/// pointer pushed its near side.
/// </summary>
public sealed class DragToTurn : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Tooltip("Degrees turned per pixel dragged.")]
    [SerializeField] private float degreesPerPixel = 0.45f;

    /// <summary>Raised while dragging, with the degrees to turn by.</summary>
    public event Action<float> Turned;

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        Turned?.Invoke(-eventData.delta.x * degreesPerPixel);
    }
}
