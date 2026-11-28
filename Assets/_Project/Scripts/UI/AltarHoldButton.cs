using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>The altar's apply button held rather than clicked (AltarStage): press and hold, let go to stop; a pad's press runs it.</summary>
public sealed class AltarHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler
{
    public AltarStage Stage;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            Stage.BeginHold(false);
        }
    }

    public void OnPointerUp(PointerEventData eventData) => Stage.EndHold();

    public void OnPointerExit(PointerEventData eventData) => Stage.EndHold();

    public void OnSubmit(BaseEventData eventData) => Stage.BeginHold(true);
}
