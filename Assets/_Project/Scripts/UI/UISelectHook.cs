using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tells code when a UI element is selected, as moving to it with a stick does: what a mouse's hover does for a row, for a controller.</summary>
public sealed class UISelectHook : MonoBehaviour, ISelectHandler
{
    public Action Selected { get; set; }

    public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
}
