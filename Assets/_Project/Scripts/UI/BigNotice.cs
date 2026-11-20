using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The big notice across the upper middle of the screen, for news the player must not miss (the user, 2026-11-20): a building
/// learned ("a large notification on the screen, with the building's picture, name and where to find it", from PlayerCrafting's
/// BuildingLearned) and hunger ("tell the player that his character is hungry and must find something to eat", from PlayerNeeds'
/// Alerted). A kit card with the picture on the left, a kicker, the name in the title face, a line, and where to go. Several take
/// their turn. A notice waits while a conversation or a window covers the screen, so a building taught in a talk shows after it.
/// Runs by itself from the first scene and only listens.
/// </summary>
public sealed class BigNotice : MonoBehaviour
{
    private const float Hold = 6f;
    private const float FadeIn = 0.35f;
    private const float FadeOut = 0.6f;
    private const float Width = 820f;
    private const float Height = 214f;
    private const float Picture = 172f;

    /// <summary>One notice: a picture (or a kit icon by name), a kicker, a title, a line and where to find it.</summary>
    public readonly struct Notice
    {
        public readonly Sprite Picture;
        public readonly string Icon;
        public readonly string Kicker;
        public readonly string Title;
        public readonly string Line;
        public readonly string Where;
        public readonly bool Warning;

        public Notice(Sprite picture, string icon, string kicker, string title, string line, string where, bool warning = false)
        {
            Picture = picture;
            Icon = icon;
            Kicker = kicker;
            Title = title;
            Line = line;
            Where = where;
            Warning = warning;
        }
    }

    private static BigNotice current;
    private readonly Queue<Notice> waiting = new Queue<Notice>();
    private RectTransform card;
    private CanvasGroup group;
    private Image picture;
    private Image icon;
    private TMP_Text kicker;
    private TMP_Text title;
    private TMP_Text line;
    private TMP_Text where;
    private Image rim;
    private float shownFor = -1f;

    /// <summary>The notice up now, or null: for tests and probes.</summary>
    public static string ShownTitle => current != null && current.shownFor >= 0f ? current.title.text : null;

    /// <summary>How many wait their turn.</summary>
    public static int Waiting => current != null ? current.waiting.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (current != null)
        {
            return;
        }

        var root = new GameObject("Big Notice");
        root.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 214;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        current = root.AddComponent<BigNotice>();
        current.Build(root.transform);
    }

    // Entering Play doesn't reload scripts in this project: the last session's notice goes with it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>Shows <paramref name="notice"/> now, or after those before it.</summary>
    public static void Show(Notice notice)
    {
        if (current == null)
        {
            Install();
        }

        current.waiting.Enqueue(notice);
    }

    private void OnEnable()
    {
        PlayerCrafting.BuildingLearned += OnBuildingLearned;
        PlayerNeeds.Alerted += OnNeedAlert;
    }

    private void OnDisable()
    {
        PlayerCrafting.BuildingLearned -= OnBuildingLearned;
        PlayerNeeds.Alerted -= OnNeedAlert;
    }

    private static void OnBuildingLearned(BuildingDefinition building)
    {
        if (building != null)
        {
            Show(ForBuilding(building));
        }
    }

    /// <summary>What the notice says of a building just learned: its picture, name, what it is for, and the build menu tab it is under.</summary>
    public static Notice ForBuilding(BuildingDefinition building)
    {
        string category = BuildCategories.Name(BuildCategories.Of(building));
        return new Notice(building.Icon, "House", "New building unlocked", building.DisplayName, FirstSentence(building.Description),
            GameInput.WithRebinds($"Find it in the build menu ({{key:Build}}), under {category}."));
    }

    private static void OnNeedAlert(PlayerNeeds needs, NeedAlert alert)
    {
        // Only the player at this screen: a co-op friend's puppet has needs too.
        if (needs == null || Player.Current == null || needs.gameObject != Player.Current.gameObject)
        {
            return;
        }

        switch (alert)
        {
            case NeedAlert.Hungry:
                Show(new Notice(null, "Pot", "You are hungry", "Find something to eat",
                    "Eat soon: berries, mushrooms, fish or a cooked meal. Use food from your bag or the skill bar.",
                    "Run out of food and your stamina weakens.", true));
                break;
            case NeedAlert.Starving:
                Show(new Notice(null, "Pot", "You are starving", "Eat something now",
                    "Your stamina is weakened until you eat. Anything edible helps: forage, fish or cook.",
                    "Hunger never kills, but a starving fighter tires fast.", true));
                break;
        }
    }

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        int end = text.IndexOf(". ", System.StringComparison.Ordinal);
        return end > 0 && end < 160 ? text.Substring(0, end + 1) : text.Length <= 160 ? text : text.Substring(0, 157) + "...";
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        card = CodeUI.Box("Card", root, new Vector2(0.5f, 1f), new Vector2(Width, Height), new Vector2(0f, -250f));
        card.pivot = new Vector2(0.5f, 0.5f);
        Image face = HearthKit.Sliced(card.gameObject.AddComponent<Image>(), style.Card, 1.3f);
        face.color = style.Panel;
        face.raycastTarget = false;
        rim = HearthKit.Rim(card, 0.95f, 1.3f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // The picture in a framed inset; a kit icon in a gold ring when there is no picture.
        RectTransform frame = HearthKit.Card(card, "Picture Frame", 22f, (Height - Picture) * 0.5f, Picture, Picture, 0.6f);
        picture = CodeUI.Stretch("Picture", frame).gameObject.AddComponent<Image>();
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        icon = HearthKit.Icon(frame, "House", (Picture - 96f) * 0.5f, (Picture - 96f) * 0.5f, 96f, style.AccentBright);

        float x = 22f + Picture + 26f;
        float width = Width - x - 26f;
        kicker = CodeUI.Label("Kicker", card, x, 24f, width, 26f, 18f, style.Accent);
        style.Title(kicker);
        kicker.characterSpacing = 8f;
        title = CodeUI.Label("Title", card, x, 52f, width, 56f, 44f, style.Text);
        style.Title(title);
        title.fontStyle = FontStyles.Bold;
        line = CodeUI.Paragraph("Line", card, x, 112f, width, 52f, 19f, style.Text);
        where = CodeUI.Label("Where", card, x, 168f, width, 26f, 18f, style.Accent);
        HearthKit.Rule(card, x, 162f, width, 0.3f);
    }

    private void Update()
    {
        bool covered = DialogueBox.IsOpen || (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen) || GamePause.IsPaused;
        if (shownFor < 0f)
        {
            if (waiting.Count == 0 || covered)
            {
                group.alpha = 0f;
                return;
            }

            Present(waiting.Dequeue());
        }

        // Time only runs while it can be seen; a window opened over it hides it until it closes.
        if (!covered)
        {
            shownFor += Time.unscaledDeltaTime;
        }

        float alpha = covered ? 0f : shownFor < FadeIn ? shownFor / FadeIn : shownFor < Hold ? 1f : 1f - Mathf.Clamp01((shownFor - Hold) / FadeOut);
        group.alpha = alpha;
        if (shownFor >= Hold + FadeOut)
        {
            shownFor = -1f;
        }
    }

    private void Present(Notice notice)
    {
        UIStyle style = UIStyle.Current;
        shownFor = 0f;
        picture.sprite = notice.Picture;
        picture.enabled = notice.Picture != null;
        icon.sprite = style.Icon(string.IsNullOrEmpty(notice.Icon) ? "Star" : notice.Icon);
        icon.enabled = notice.Picture == null && icon.sprite != null;
        icon.color = notice.Warning ? style.Danger : style.AccentBright;
        kicker.text = (notice.Kicker ?? "").ToUpperInvariant();
        kicker.color = notice.Warning ? style.Danger : style.Accent;
        rim.color = HearthKit.Faded(notice.Warning ? style.Danger : style.Accent, 0.95f);
        title.text = notice.Title ?? "";
        line.text = HearthKit.ToneNumbers(notice.Line ?? "");
        where.text = notice.Where ?? "";
        UIMotion.Pop(card, 0.85f, 0.35f);
    }
}
