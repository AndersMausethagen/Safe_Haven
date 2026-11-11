using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player needs to see of a horse, low in the middle of the screen, frameless in the Hearthline way (type and
/// thin bars): in the struggle, the strain on the rope and the strength the horse has left, and what to press; worn out,
/// how long it will stand; on the breaking ride, how often it has been fed and, while it asks, its patience; riding a
/// tamed horse, its stamina. Built in code the first time a horse needs it; reads HorseTaming.Active and PlayerRiding.
/// </summary>
public sealed class HorseHud : MonoBehaviour
{
    private const float Width = 460f;
    private const float BarHeight = 6f;

    private static HorseHud current;

    private CanvasGroup group;
    private TMP_Text title;
    private TMP_Text hint;
    private Bar first;
    private Bar second;

    private sealed class Bar
    {
        public TMP_Text Label;
        public RectTransform Track;
        public Image Fill;

        public void Show(bool shown)
        {
            Label.gameObject.SetActive(shown);
            Track.gameObject.SetActive(shown);
        }

        public void Set(string label, float fraction, Color colour)
        {
            Label.text = label;
            Fill.color = colour;
            Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Makes the HUD for this scene, unless it has one.</summary>
    public static void Ensure()
    {
        if (current != null)
        {
            return;
        }

        var root = new GameObject("Horse HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 72;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        current = root.AddComponent<HorseHud>();
        current.group = root.AddComponent<CanvasGroup>();
        current.group.blocksRaycasts = false;
        current.group.alpha = 0f;
        current.Build(root.transform);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        // Above the noise meter and the skill bar.
        RectTransform panel = CodeUI.Box("Panel", root, new Vector2(0.5f, 0f), new Vector2(Width, 150f), new Vector2(0f, 340f));
        title = CodeUI.Label("Title", panel, 0f, 0f, Width, 30f, 24f, style.Text, TextAlignmentOptions.Center);
        title.font = style.BodyFont;
        first = MakeBar(panel, 40f, style);
        second = MakeBar(panel, 82f, style);
        hint = CodeUI.Label("Hint", panel, 0f, 122f, Width, 26f, 19f, style.Accent, TextAlignmentOptions.Center);
        hint.font = style.BodyFont;
    }

    private static Bar MakeBar(RectTransform panel, float y, UIStyle style)
    {
        var bar = new Bar { Label = CodeUI.Label("Label", panel, 0f, y, Width, 22f, 17f, style.Muted) };
        bar.Label.font = style.BodyFont;
        bar.Track = CodeUI.At("Track", panel, 0f, y + 26f, Width, BarHeight);
        bar.Track.gameObject.AddComponent<Image>().color = style.Track;
        RectTransform fill = CodeUI.Stretch("Fill", bar.Track);
        bar.Fill = fill.gameObject.AddComponent<Image>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        return bar;
    }

    private void Update()
    {
        UIStyle style = UIStyle.Current;
        HorseTaming taming = HorseTaming.Active;
        Player player = Player.Current;
        PlayerRiding riding = player != null && player.TryGetComponent(out PlayerRiding found) ? found : null;
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        string key = GameInput.KeyLabel(GameInput.Interact);
        bool shown = !windows && !GamePause.IsPaused;
        if (taming != null && taming.Current == HorseTaming.Phase.Struggle)
        {
            title.text = "Hold it!";
            first.Show(true);
            second.Show(true);
            first.Set("The rope", taming.Strain, taming.Strain > 0.75f ? style.Danger : style.Warning);
            second.Set("Its strength", taming.Strength, style.Accent);
            hint.text = $"Tap {key} or left click";
        }
        else if (taming != null && taming.Current == HorseTaming.Phase.GivenIn)
        {
            title.text = "It is spent";
            first.Show(true);
            second.Show(false);
            first.Set("Before it fights again", taming.MountLeft / Mathf.Max(1f, taming.Horse.Definition.MountSeconds), style.Good);
            hint.text = $"{key}: get on its back";
        }
        else if (taming != null && taming.Current == HorseTaming.Phase.Breaking)
        {
            title.text = $"Breaking it in: fed {taming.Fed} of {taming.Needed}";
            first.Show(taming.IsAsking);
            second.Show(false);
            if (taming.IsAsking)
            {
                first.Set("Its patience", taming.PatienceLeft, taming.PatienceLeft < 0.35f ? style.Danger : style.Warning);
            }

            ItemDefinition barley = taming.Horse.Definition.Barley;
            hint.text = taming.IsAsking ? $"{key}: give it {(barley != null ? barley.DisplayName.ToLowerInvariant() : "food")}" : "Stay on";
        }
        else if (riding != null && riding.IsRiding)
        {
            Horse horse = riding.Riding;
            title.text = horse.HorseName;
            first.Show(true);
            second.Show(false);
            float stamina = horse.Stats.maxStamina > 0f ? horse.Stamina / horse.Stats.maxStamina : 0f;
            first.Set("Stamina", stamina, stamina < 0.2f ? style.Warning : style.Good);
            hint.text = "";
        }
        else
        {
            shown = false;
        }

        group.alpha = shown ? 1f : 0f;
    }
}
