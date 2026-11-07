using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The talent screen (N): the player's level, experience and unspent talent points above the four trees (Ranger,
/// Soldier, Runemaster, Tinkerer) side by side. Each talent shows its rank and what it gives; click to buy a rank. Rows
/// open as points go into their tree. Talents can be reset for free at home (beside the portal). Built in code for
/// every scene with a player (ExperienceTracker); it only reads and changes GameSession.Progress.
/// </summary>
public sealed class TalentWindow : MonoBehaviour
{
    private const float Width = 1380f;
    private const float Height = 820f;
    private const float ScreenMargin = 24f;

    private static readonly string[] TreeLines =
    {
        "Bows, speed and foraging.",
        "Health, armour and the blade.",
        "Spells, wards and resistances.",
        "Tools, work and endurance.",
    };

    private UIWindow window;
    private RectTransform panel;
    private TMP_Text header;
    private Button resetButton;
    private readonly List<(TalentDefinition talent, Image card, Image owned, TMP_Text text)> cards = new List<(TalentDefinition, Image, Image, TMP_Text)>();
    private readonly TMP_Text[] treeHeaders = new TMP_Text[4];
    private CharacterProgress watched;

    public static TalentWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static TalentWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Talent Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<TalentWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDestroy()
    {
        if (watched != null)
        {
            watched.Changed -= Refresh;
        }
    }

    private void Update()
    {
        if (watched != GameSession.Progress)
        {
            if (watched != null)
            {
                watched.Changed -= Refresh;
            }

            watched = GameSession.Progress;
            watched.Changed += Refresh;
            Refresh();
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        panel = Rect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text title = Text("Title", panel, 38f, style.Text, new Vector2(0.5f, 1f), new Vector2(600f, 54f), new Vector2(0f, -38f), TextAlignmentOptions.Center);
        title.text = "Talents";
        style.Title(title);
        header = Text("Header", panel, 22f, style.Text, new Vector2(0.5f, 1f), new Vector2(1200f, 36f), new Vector2(0f, -78f), TextAlignmentOptions.Center);

        LevelingSettings settings = LevelingSettings.Current;
        for (int t = 0; t < 4; t++)
        {
            var tree = (TalentTree)t;
            float x = -510f + t * 340f;
            treeHeaders[t] = Text(tree + " Header", panel, 26f, style.Accent, new Vector2(0.5f, 1f), new Vector2(320f, 64f), new Vector2(x, -136f), TextAlignmentOptions.Center);
            var talents = new List<TalentDefinition>();
            foreach (TalentDefinition talent in settings.Talents)
            {
                if (talent != null && talent.Tree == tree)
                {
                    talents.Add(talent);
                }
            }

            talents.Sort((a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
            for (int i = 0; i < talents.Count; i++)
            {
                AddCard(panel, talents[i], new Vector2(x, -228f - i * 108f));
            }
        }

        CodeUI.AddButton("Reset Talents", panel, Width * 0.5f - 210f, Height - 63f, 200f, UIRole.Kind.Button, ResetTalents, out resetButton).text = "Reset Talents";
        CodeUI.AddButton("Close", panel, Width * 0.5f + 10f, Height - 63f, 200f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Talents, panel.gameObject);
        window.OpenChanged += open =>
        {
            if (open)
            {
                Refresh();
            }
        };
    }

    private void AddCard(RectTransform panel, TalentDefinition talent, Vector2 position)
    {
        RectTransform card = Rect(talent.DisplayName, panel, new Vector2(0.5f, 1f), new Vector2(316f, 100f), position);
        var image = card.gameObject.AddComponent<Image>();
        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => Raise(talent));
        TMP_Text text = Text("Text", card, 17f, UIStyle.Current.Text, new Vector2(0.5f, 0.5f), new Vector2(296f, 92f), Vector2.zero, TextAlignmentOptions.TopLeft);
        // Amber down the edge marks what is yours: a bright card would wash out the bonuses written on it.
        var owned = CodeUI.At("Owned", card, 0f, 0f, 4f, 100f).gameObject.AddComponent<Image>();
        owned.color = UIStyle.Current.Accent;
        owned.raycastTarget = false;
        card.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Button);
        cards.Add((talent, image, owned, text));
    }

    private void Raise(TalentDefinition talent)
    {
        LevelingSettings settings = LevelingSettings.Current;
        if (!GameSession.Progress.CanRaise(talent, settings, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        GameSession.Progress.Raise(talent, settings);
    }

    private void ResetTalents()
    {
        if (!CanReset)
        {
            GameMessages.Post("Talents can only be reset at home, beside the portal.", MessageKind.Warning);
            return;
        }

        GameSession.Progress.ResetTalents();
        GameMessages.Post("Your talent points are yours to spend again.");
    }

    // Free, but only at home: a change of mind needs a quiet place.
    private static bool CanReset => HomePortal.Current != null;

    private void Refresh()
    {
        if (header == null)
        {
            return;
        }

        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);

        UIStyle style = UIStyle.Current;
        string accent = UIStyle.Hex(style.Accent);
        string muted = UIStyle.Hex(style.Muted);
        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int points = progress.UnspentPoints(settings);
        string experience = progress.IsMaxLevel(settings) ? "highest level" : $"{progress.Experience} / {settings.ExperienceToNext(progress.Level)} experience";
        header.text = $"Level {progress.Level}   ·   {experience}   ·   <color={accent}>{points} talent point{(points == 1 ? "" : "s")}</color>";
        for (int t = 0; t < 4; t++)
        {
            var tree = (TalentTree)t;
            treeHeaders[t].text = $"{tree}  <size=70%><color={muted}>{progress.SpentIn(tree, settings)}</color></size>\n<size=60%><color={muted}>{TreeLines[t]}</color></size>";
        }

        foreach ((TalentDefinition talent, Image card, Image owned, TMP_Text text) in cards)
        {
            int rank = progress.RankOf(talent);
            bool open = progress.SpentIn(talent.Tree, settings) >= talent.Row * settings.PointsPerRow;
            // The lit colour: the button role tints it down at rest, which leaves a locked card a step below an open one.
            card.color = open ? style.ControlLit : style.Filled;
            owned.enabled = rank > 0;
            string bonuses = talent.DescribeBonuses(Mathf.Max(1, rank));
            string next = rank > 0 && rank < talent.MaxRank ? $"  <color={muted}>(next: {talent.DescribeBonuses(rank + 1)})</color>" : "";
            string locked = open ? "" : $"\n<color={UIStyle.Hex(style.Warning)}>Needs {talent.Row * settings.PointsPerRow} points in {talent.Tree}</color>";
            text.text = $"<b><color={accent}>{talent.DisplayName}</color></b>  <color={muted}>{rank}/{talent.MaxRank}</color>\n" +
                        $"<size=85%>{talent.Description}</size>\n<size=85%><color={UIStyle.Hex(style.Good)}>{bonuses}</color>{next}</size>{locked}";
        }

        resetButton.interactable = CanReset && progress.PointsSpent > 0;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchorAt;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static TMP_Text Text(string name, Transform parent, float size, Color colour, Vector2 anchorAt, Vector2 box, Vector2 position,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = Rect(name, parent, anchorAt, box, position);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
