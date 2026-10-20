using TMPro;
using UnityEngine;

/// <summary>
/// The floating "!", "…" or "?" above a Quest Giver. The QuestGiver creates it as a child and sets its state; symbols,
/// colors and size come from the shared QuestMarkerSettings. It turns to face the camera and bobs gently.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestMarker : MonoBehaviour
{
    private TextMeshPro text;
    private QuestGiverState state = QuestGiverState.None;
    private Vector3 basePosition;
    private float phase;

    public QuestGiverState State => state;

    /// <summary>Creates a marker above the given object.</summary>
    public static QuestMarker CreateFor(Transform owner)
    {
        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        var go = new GameObject("Quest Marker");
        go.transform.SetParent(owner, false);
        go.transform.localPosition = Vector3.up * settings.Height;
        var marker = go.AddComponent<QuestMarker>();
        marker.Build(settings);
        return marker;
    }

    private void Build(QuestMarkerSettings settings)
    {
        basePosition = transform.localPosition;
        phase = Random.value * Mathf.PI * 2f;
        text = gameObject.AddComponent<TextMeshPro>();
        if (settings.Font != null)
        {
            text.font = settings.Font;
        }

        if (settings.FontMaterial != null)
        {
            text.fontSharedMaterial = settings.FontMaterial;
        }

        text.fontSize = settings.FontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
        text.outlineWidth = settings.OutlineWidth;
        text.outlineColor = Color.black;
        Show(QuestGiverState.None);
    }

    public void Show(QuestGiverState next)
    {
        state = next;
        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        bool visible = settings.TryGetLook(next, out string symbol, out Color color);
        text.enabled = visible;
        if (visible)
        {
            text.text = symbol;
            text.color = color;
        }
    }

    private void LateUpdate()
    {
        if (!text.enabled)
        {
            return;
        }

        Camera view = Camera.main;
        if (view != null)
        {
            transform.rotation = view.transform.rotation;
        }

        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * settings.BobSpeed + phase) * settings.BobHeight);
    }
}
