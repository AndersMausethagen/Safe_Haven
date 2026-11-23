using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small glint over a building when one of its modules or extensions just made the difference (the systems review, 2026-11-21:
/// Valheim's comfort and Dragonwilds' station upgrades are felt because the game says when they fire). A card with the kit's icon and
/// a word ("Rune Shelf: +1 Plank", "Fine work", "Turned back") rises from the roof and fades, with the glint sound. Callers fire it
/// only when the bonus itself made the outcome (a roll that would have failed without the station's share), so it never claims luck
/// that was the player's own. Same building and words within <see cref="Repeat"/> seconds show once.
/// </summary>
public sealed class ModuleCues : MonoBehaviour
{
    private const float Life = 2.2f;
    private const float Rise = 46f;
    private const float Repeat = 3f;
    private const float Reach = 60f;

    private sealed class Cue
    {
        public Transform Building;
        public Vector3 Over;
        public string Text;
        public float Born;
        public RectTransform Root;
        public CanvasGroup Group;
        public TMP_Text Label;
        public Image Icon;
    }

    private static ModuleCues current;
    private readonly List<Cue> cues = new List<Cue>();
    private readonly Stack<Cue> spare = new Stack<Cue>();
    private RectTransform area;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Shows the cue over the station that owns <paramref name="queue"/> (nothing when none stands in the scene).</summary>
    public static void Fire(StationQueue queue, string text, string icon = "Star")
    {
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station != null && station.Queue == queue)
            {
                Fire(station, text, icon);
                return;
            }
        }
    }

    /// <summary>Shows the cue over <paramref name="building"/>, with the glint sound there.</summary>
    public static void Fire(Component building, string text, string icon = "Star")
    {
        if (building == null || string.IsNullOrEmpty(text) || !Application.isPlaying)
        {
            return;
        }

        if (current == null)
        {
            current = new GameObject("Module Cues").AddComponent<ModuleCues>();
        }

        current.Add(building.transform, text, icon);
    }

    private void Awake()
    {
        var canvasObject = new GameObject("Module Cues Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 106;
        UIRoot.ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());
        area = (RectTransform)canvasObject.transform;
    }

    private void Add(Transform building, string text, string icon)
    {
        foreach (Cue each in cues)
        {
            if (each.Building == building && each.Text == text && Time.unscaledTime - each.Born < Repeat)
            {
                return;
            }
        }

        Cue cue = spare.Count > 0 ? spare.Pop() : Make();
        cue.Building = building;
        cue.Over = building.position + Vector3.up * RoofOf(building);
        cue.Text = text;
        cue.Born = Time.unscaledTime;
        cue.Label.text = HearthKit.ToneNumbers(text);
        Sprite picture = UIStyle.Current.Icon(icon);
        cue.Icon.sprite = picture;
        cue.Icon.color = HearthKit.IconColour(icon);
        cue.Icon.enabled = picture != null;
        cue.Root.gameObject.SetActive(true);
        cues.Add(cue);
        AudioManager.Play(AudioManager.Sounds != null ? AudioManager.Sounds.ModuleCue : null, cue.Over);
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        for (int i = cues.Count - 1; i >= 0; i--)
        {
            Cue cue = cues[i];
            float age = Time.unscaledTime - cue.Born;
            if (age > Life || cue.Building == null)
            {
                cue.Root.gameObject.SetActive(false);
                cues.RemoveAt(i);
                spare.Push(cue);
                continue;
            }

            Vector3 screen = view != null ? view.WorldToScreenPoint(cue.Over) : Vector3.back;
            bool shown = screen.z > 0f && (view.transform.position - cue.Over).sqrMagnitude < Reach * Reach
                         && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 at)
                         && Place(cue, at, age);
            cue.Group.alpha = shown && !GamePause.HidesHud ? Fade(age) : 0f;
        }
    }

    private static bool Place(Cue cue, Vector2 at, float age)
    {
        float t = age / Life;
        cue.Root.anchoredPosition = at + new Vector2(0f, Rise * (1f - (1f - t) * (1f - t)));
        cue.Root.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(age * 6f));
        return true;
    }

    private static float Fade(float age) => Mathf.Clamp01(age * 8f) * Mathf.Clamp01((Life - age) / 0.6f);

    private Cue Make()
    {
        UIStyle style = UIStyle.Current;
        var holder = new GameObject("Cue", typeof(RectTransform));
        holder.transform.SetParent(area, false);
        var rect = (RectTransform)holder.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 34f);
        var cue = new Cue { Root = rect, Group = holder.AddComponent<CanvasGroup>() };
        cue.Group.blocksRaycasts = false;
        Image card = HearthKit.Sliced(holder.AddComponent<Image>(), style.Card, 1.2f);
        card.color = HearthKit.Faded(style.Inset, 0.9f);
        card.raycastTarget = false;
        HearthKit.Rim(rect, 0.55f);
        cue.Icon = HearthKit.Icon(rect, "Star", 8f, 5f, 24f);
        cue.Icon.raycastTarget = false;
        var labelHolder = new GameObject("Label", typeof(RectTransform));
        labelHolder.transform.SetParent(rect, false);
        var labelRect = (RectTransform)labelHolder.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(38f, 2f);
        labelRect.offsetMax = new Vector2(-10f, -2f);
        TextMeshProUGUI label = labelHolder.AddComponent<TextMeshProUGUI>();
        if (style.BodyFont != null)
        {
            label.font = style.BodyFont;
        }

        label.fontSize = 16f;
        label.color = style.Text;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate; // Not Ellipsis: the fonts have no "…" (TMP warned and truncated anyway), and the UI shows none.
        label.raycastTarget = false;
        cue.Label = label;
        return cue;
    }

    // The top of what is drawn, above its foot.
    private static float RoofOf(Transform building)
    {
        bool any = false;
        Bounds drawn = default;
        foreach (Renderer part in building.GetComponentsInChildren<Renderer>())
        {
            if (!part.enabled || part is ParticleSystemRenderer)
            {
                continue;
            }

            if (any)
            {
                drawn.Encapsulate(part.bounds);
            }
            else
            {
                drawn = part.bounds;
                any = true;
            }
        }

        return any ? drawn.max.y - building.position.y + 1.2f : 3.5f;
    }
}
