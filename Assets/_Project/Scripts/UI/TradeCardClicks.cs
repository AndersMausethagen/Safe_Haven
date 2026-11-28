using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A card in the trade window that answers both buttons: left (or a pad's press, through its Button) adds to the deal, right takes back.
/// Built in code by TradeWindow.
/// </summary>
public sealed class TradeCardClicks : MonoBehaviour, IPointerClickHandler
{
    public Action Right;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Right?.Invoke();
        }
    }
}
