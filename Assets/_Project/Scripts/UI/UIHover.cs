using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tells a window built in code when the pointer is on a part of it, for tooltips. The part needs a raycast target.</summary>
public sealed class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action Entered;
    public Action Exited;

    public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

    public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

    // Hidden under the pointer, it gets no exit event.
    private void OnDisable() => Exited?.Invoke();
}
