using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>A region that takes a drop anywhere on it (needs a raycast-target Graphic): the skill window's book, where a bar skill
/// dropped between the cards is taken off the bar.</summary>
public sealed class DropArea : MonoBehaviour, IDropHandler
{
    public event System.Action Dropped;

    public void OnDrop(PointerEventData eventData) => Dropped?.Invoke();
}
