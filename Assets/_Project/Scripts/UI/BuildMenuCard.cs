using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>One tile in the build menu: tells the menu when the pointer (or keyboard focus) is on it.</summary>
public sealed class BuildMenuCard : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public event Action<BuildMenuCard> Focused;

    public int Index { get; set; }

    public void OnPointerEnter(PointerEventData eventData) => Focused?.Invoke(this);

    public void OnSelect(BaseEventData eventData) => Focused?.Invoke(this);
}
