using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Important sounds written on the screen, with the side they came from (GDD §18.5): for those who can't hear them, or are playing
/// quietly. A sound has one when its SoundDefinition gives it a Caption, so which sounds matter is data; the Sound Captions option
/// turns them on. A persistent system with its own small canvas, as sounds are heard in every scene.
/// </summary>
public sealed class SoundCaptions : MonoBehaviour, IGameSystem
{
    private const int MaxLines = 4;
    private const float ShowSeconds = 3f;
    private const float FadeSeconds = 0.6f;
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float RightMargin = 36f;
    private const float BottomMargin = 290f;
    private const float LineHeight = 30f;

    private sealed class Line
    {
        public string Text;
        public float ShownAt;
        public TMP_Text Label;
    }

    private static SoundCaptions instance;

    private readonly List<Line> lines = new List<Line>();
    private RectTransform column;

    public void Initialise()
    {
        instance = this;
        var root = new GameObject("Sound Captions");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        // The UI's own scaler, so "UI and Text Size" applies from the start, not only after it is changed in a session.
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        column = new GameObject("Captions", typeof(RectTransform)).GetComponent<RectTransform>();
        column.SetParent(root.transform, false);
        column.anchorMin = column.anchorMax = column.pivot = new Vector2(1f, 0f);
        column.anchoredPosition = new Vector2(-RightMargin, BottomMargin);
        column.sizeDelta = new Vector2(620f, MaxLines * LineHeight);
    }

    public void OnSceneEntered(UnityEngine.SceneManagement.Scene scene)
    {
    }

    /// <summary>Called for every sound that plays.</summary>
    public static void Heard(SoundDefinition sound, Vector3 position)
    {
        if (instance == null || string.IsNullOrEmpty(sound.Caption) || !GameOptions.SoundCaptions.On)
        {
            return;
        }

        Vector3 listener = Player.Current != null ? Player.Current.transform.position : position;
        if (sound.Positional && (position - listener).sqrMagnitude > sound.AudibleDistance * sound.AudibleDistance)
        {
            return;
        }

        Camera view = Camera.main;
        string side = sound.Positional && view != null ? SideOf(position - listener, view.transform.forward) : "";
        instance.Add(string.IsNullOrEmpty(side) ? sound.Caption : $"{sound.Caption}, {side}");
    }

    /// <summary>Which way something is from the listener, in words, seen along where the camera looks. Empty when it is right here.</summary>
    public static string SideOf(Vector3 toSound, Vector3 viewForward)
    {
        toSound.y = 0f;
        viewForward.y = 0f;
        if (toSound.sqrMagnitude < 4f || viewForward.sqrMagnitude < 0.0001f)
        {
            return "";
        }

        float angle = Vector3.SignedAngle(viewForward, toSound, Vector3.up);
        if (Mathf.Abs(angle) <= 45f)
        {
            return "ahead";
        }

        if (Mathf.Abs(angle) >= 135f)
        {
            return "behind";
        }

        return angle > 0f ? "to the right" : "to the left";
    }

    private void Add(string text)
    {
        foreach (Line line in lines)
        {
            if (line.Text == text)
            {
                line.ShownAt = Time.unscaledTime;
                return;
            }
        }

        if (lines.Count >= MaxLines)
        {
            Remove(0);
        }

        var label = new GameObject("Caption", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(column, false);
        UIStyle.Current.OnWorld(label);
        label.alignment = TextAlignmentOptions.Right;
        label.fontSize = 22f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = text;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1f, 0f);
        label.rectTransform.sizeDelta = new Vector2(620f, LineHeight);
        lines.Add(new Line { Text = text, ShownAt = Time.unscaledTime, Label = label });
    }

    private void Remove(int index)
    {
        Destroy(lines[index].Label.gameObject);
        lines.RemoveAt(index);
    }

    private void Update()
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - lines[i].ShownAt;
            if (age >= ShowSeconds + FadeSeconds)
            {
                Remove(i);
            }
        }

        for (int i = 0; i < lines.Count; i++)
        {
            float age = Time.unscaledTime - lines[i].ShownAt;
            lines[i].Label.alpha = age <= ShowSeconds ? 1f : 1f - (age - ShowSeconds) / FadeSeconds;
            lines[i].Label.rectTransform.anchoredPosition = new Vector2(0f, (lines.Count - 1 - i) * LineHeight);
        }
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        instance = null;
    }
}
