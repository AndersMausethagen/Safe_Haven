using TMPro;
using UnityEngine;

/// <summary>
/// How Quest Givers mark their state for every giver at once: the floating symbol and color for a quest you can take
/// ("!"), one you are on ("…") and one ready to hand in ("?"), plus size, height and bob. Lives in
/// Resources/QuestMarkerSettings; edit it in the Systems Editor (Quests page).
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Quest Marker Settings", fileName = "QuestMarkerSettings")]
public sealed class QuestMarkerSettings : GameSettings<QuestMarkerSettings>
{
    [Header("Quest available")]
    [SerializeField] private string availableSymbol = "!";
    [SerializeField] private Color availableColor = new Color(1f, 0.84f, 0.2f, 1f);

    [Header("Quest in progress")]
    [Tooltip("Shown while the player is on the giver's quest. Empty hides the marker meanwhile.")]
    [SerializeField] private string activeSymbol = "…";
    [SerializeField] private Color activeColor = new Color(0.7f, 0.72f, 0.76f, 1f);

    [Header("Ready to hand in")]
    [SerializeField] private string readySymbol = "?";
    [SerializeField] private Color readyColor = new Color(1f, 0.84f, 0.2f, 1f);

    [Header("Look")]
    [Tooltip("Meters above the giver's pivot.")]
    [SerializeField, Min(0f)] private float height = 2.4f;
    [SerializeField, Min(0.1f)] private float fontSize = 8f;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [Tooltip("Meters the marker bobs up and down. 0 keeps it still.")]
    [SerializeField, Min(0f)] private float bobHeight = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.5f;
    [Tooltip("Optional. Empty uses TextMesh Pro's default font.")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Optional font material, e.g. an always-on-top one so trees never hide the marker.")]
    [SerializeField] private Material fontMaterial;

    public float Height => height;
    public float FontSize => fontSize;
    public float OutlineWidth => outlineWidth;
    public float BobHeight => bobHeight;
    public float BobSpeed => bobSpeed;
    public TMP_FontAsset Font => font;
    public Material FontMaterial => fontMaterial;

    /// <summary>The symbol and color for a giver state; false when that state shows no marker.</summary>
    public bool TryGetLook(QuestGiverState state, out string symbol, out Color color)
    {
        switch (state)
        {
            case QuestGiverState.Available:
                symbol = availableSymbol;
                color = availableColor;
                break;
            case QuestGiverState.InProgress:
                symbol = activeSymbol;
                color = activeColor;
                break;
            case QuestGiverState.ReadyToTurnIn:
                symbol = readySymbol;
                color = readyColor;
                break;
            default:
                symbol = null;
                color = Color.clear;
                break;
        }

        return !string.IsNullOrEmpty(symbol);
    }
}
