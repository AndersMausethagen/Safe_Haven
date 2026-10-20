using UnityEngine;

/// <summary>
/// The colors of Safe Haven's windows (GDD §19: warm amber, wood and gold at home rather than cold grey): window
/// backgrounds and rims, buttons, titles and body text. Edit the colors, then run Safe Haven > UI > Apply UI Theme to
/// recolor every UI prefab. Editor-only data: the game reads the colors baked into the prefabs.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "Safe Haven/UI/UI Theme")]
public sealed class UITheme : ScriptableObject
{
    [Header("Windows")]
    [Tooltip("Background of window panels.")]
    public Color panel = new Color(0.075f, 0.062f, 0.05f, 0.95f);
    [Tooltip("The gold rim around window panels.")]
    public Color panelRim = new Color(0.64f, 0.49f, 0.29f, 0.9f);

    [Header("Buttons")]
    [Tooltip("Ordinary buttons. Buttons with their own strong color (green Enter, red) keep it.")]
    public Color button = new Color(0.3f, 0.235f, 0.17f, 1f);
    public Color buttonText = new Color(0.95f, 0.9f, 0.8f, 1f);

    [Header("Text")]
    [Tooltip("Text objects named Title.")]
    public Color title = new Color(0.91f, 0.77f, 0.42f, 1f);
    [Tooltip("Plain white text becomes this parchment color.")]
    public Color body = new Color(0.93f, 0.89f, 0.8f, 1f);
}
