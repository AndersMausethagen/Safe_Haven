using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The Camera section of the Options page. One button switches the style the camera key (V) toggles; one per
/// situation cycles Your choice → Top-down → Action, fixing the style there whatever the toggle says.
/// Everything is stored in <see cref="CameraPreferences"/>, so the camera changes at once.
/// </summary>
public sealed class CameraOptionsPanel : MonoBehaviour
{
    [SerializeField] private Button styleButton;
    [SerializeField] private TMP_Text styleLabel;
    [Tooltip("One per CameraContext, in its order: Exploration, Combat, Building, Raid.")]
    [SerializeField] private Button[] overrideButtons = new Button[CameraStyleRules.ContextCount];
    [SerializeField] private TMP_Text[] overrideLabels = new TMP_Text[CameraStyleRules.ContextCount];

    private UnityAction[] overrideClicks;

    private void OnEnable()
    {
        if (styleButton != null)
        {
            styleButton.onClick.AddListener(ToggleStyle);
        }

        overrideClicks = new UnityAction[overrideButtons.Length];
        for (int i = 0; i < overrideButtons.Length; i++)
        {
            var context = (CameraContext)i;
            overrideClicks[i] = () => CycleOverride(context);
            if (overrideButtons[i] != null)
            {
                overrideButtons[i].onClick.AddListener(overrideClicks[i]);
            }
        }

        CameraPreferences.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (styleButton != null)
        {
            styleButton.onClick.RemoveListener(ToggleStyle);
        }

        for (int i = 0; i < overrideButtons.Length; i++)
        {
            if (overrideButtons[i] != null)
            {
                overrideButtons[i].onClick.RemoveListener(overrideClicks[i]);
            }
        }

        CameraPreferences.Changed -= Refresh;
    }

    private static void ToggleStyle()
    {
        CameraPreferences.Chosen = CameraPreferences.Chosen == CameraStyle.Action ? CameraStyle.TopDown : CameraStyle.Action;
    }

    private static void CycleOverride(CameraContext context)
    {
        var next = (CameraStyleOverride)(((int)CameraPreferences.GetOverride(context) + 1) % 3);
        CameraPreferences.SetOverride(context, next);
    }

    private void Refresh()
    {
        if (styleLabel != null)
        {
            styleLabel.text = $"Camera (V): <b>{CameraStyleRules.Label(CameraPreferences.Chosen)}</b>";
        }

        for (int i = 0; i < overrideLabels.Length; i++)
        {
            if (overrideLabels[i] != null)
            {
                var context = (CameraContext)i;
                overrideLabels[i].text = $"{context}: <b>{CameraStyleRules.Label(CameraPreferences.GetOverride(context))}</b>";
            }
        }
    }
}
