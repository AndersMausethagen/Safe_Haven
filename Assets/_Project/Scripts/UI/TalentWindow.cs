using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The talent screen (N): the player's level, experience and unspent talent points above the four trees (Ranger,
/// Soldier, Runemaster, Tinkerer) side by side. Each talent shows its rank and what it gives; click to buy a rank. Rows
/// open as points go into their tree. Talents can be reset for free at home (beside the Heart Tree). Built in code for
/// every scene with a player (ExperienceTracker); it only reads and changes GameSession.Progress.
/// </summary>
public sealed class TalentWindow : MonoBehaviour
{
    private static readonly Color Panel = new Color(0.075f, 0.062f, 0.05f, 0.96f);
    private static readonly Color Rim = new Color(0.64f, 0.49f, 0.29f, 0.9f);
    private static readonly Color Title = new Color(0.91f, 0.77f, 0.42f, 1f);
    private static readonly Color Body = new Color(0.93f, 0.89f, 0.8f, 1f);
    private static readonly Color Card = new Color(0.22f, 0.17f, 0.12f, 1f);
    private static readonly Color CardOwned = new Color(0.3f, 0.26f, 0.12f, 1f);
    private static readonly Color CardLocked = new Color(0.12f, 0.1f, 0.08f, 1f);

    private static readonly string[] TreeLines =
    {
        "Bows, speed and foraging.",
        "Health, armour and the blade.",
        "Spells, wards and resistances.",
        "Tools, work and endurance.",
    };

    private UIWindow window;
    private TMP_Text header;
    private Button resetButton;
    private readonly List<(TalentDefinition talent, Image card, TMP_Text text)> cards = new List<(TalentDefinition, Image, TMP_Text)>();
    private readonly TMP_Text[] treeHeaders = new TMP_Text[4];
    private CharacterProgress watched;

    public static TalentWindow Current { get; private set; }

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
        RectTransform panel = Rect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(1380f, 820f), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = Panel;
        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = Rim;
        rim.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = Text("Title", panel, 34f, Title, new Vector2(0.5f, 1f), new Vector2(600f, 50f), new Vector2(0f, -36f), TextAlignmentOptions.Center);
        title.text = "Talents";
        title.fontStyle = FontStyles.Bold;
        header = Text("Header", panel, 22f, Body, new Vector2(0.5f, 1f), new Vector2(1200f, 36f), new Vector2(0f, -78f), TextAlignmentOptions.Center);

        LevelingSettings settings = LevelingSettings.Current;
        for (int t = 0; t < 4; t++)
        {
            var tree = (TalentTree)t;
            float x = -510f + t * 340f;
            treeHeaders[t] = Text(tree + " Header", panel, 26f, Title, new Vector2(0.5f, 1f), new Vector2(320f, 64f), new Vector2(x, -136f), TextAlignmentOptions.Center);
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

        resetButton = AddButton(panel, "Reset Talents", new Vector2(-110f, 40f), ResetTalents);
        AddButton(panel, "Close", new Vector2(110f, 40f), () => window.Close());

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
        TMP_Text text = Text("Text", card, 17f, Body, new Vector2(0.5f, 0.5f), new Vector2(296f, 92f), Vector2.zero, TextAlignmentOptions.TopLeft);
        cards.Add((talent, image, text));
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
            GameMessages.Post("Talents can only be reset at home, beside the Heart Tree.", MessageKind.Warning);
            return;
        }

        GameSession.Progress.ResetTalents();
        GameMessages.Post("Your talent points are yours to spend again.");
    }

    // Free, but only at home: a change of mind needs a quiet place.
    private static bool CanReset => HeartTree.Current != null;

    private void Refresh()
    {
        if (header == null)
        {
            return;
        }

        LevelingSettings settings = LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int points = progress.UnspentPoints(settings);
        string experience = progress.IsMaxLevel(settings) ? "highest level" : $"{progress.Experience} / {settings.ExperienceToNext(progress.Level)} experience";
        header.text = $"Level {progress.Level}   ·   {experience}   ·   <color=#E8C46A>{points} talent point{(points == 1 ? "" : "s")}</color>";
        for (int t = 0; t < 4; t++)
        {
            var tree = (TalentTree)t;
            treeHeaders[t].text = $"{tree}  <size=70%><alpha=#99>{progress.SpentIn(tree, settings)}<alpha=#FF></size>\n<size=60%><alpha=#AA>{TreeLines[t]}</size>";
        }

        foreach ((TalentDefinition talent, Image card, TMP_Text text) in cards)
        {
            int rank = progress.RankOf(talent);
            bool open = progress.SpentIn(talent.Tree, settings) >= talent.Row * settings.PointsPerRow;
            card.color = rank > 0 ? CardOwned : open ? Card : CardLocked;
            string bonuses = talent.DescribeBonuses(Mathf.Max(1, rank));
            string next = rank > 0 && rank < talent.MaxRank ? $"  <alpha=#88>(next: {talent.DescribeBonuses(rank + 1)})<alpha=#FF>" : "";
            string locked = open ? "" : $"\n<color=#C07A5A>Needs {talent.Row * settings.PointsPerRow} points in {talent.Tree}</color>";
            text.text = $"<b><color=#E8C46A>{talent.DisplayName}</color></b>  <alpha=#CC>{rank}/{talent.MaxRank}<alpha=#FF>\n" +
                        $"<size=85%>{talent.Description}</size>\n<size=85%><color=#8BE08B>{bonuses}</color>{next}</size>{locked}";
        }

        resetButton.interactable = CanReset && progress.PointsSpent > 0;
    }

    private Button AddButton(RectTransform panel, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = Rect(label, panel, new Vector2(0.5f, 0f), new Vector2(200f, 46f), position);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Card;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        TMP_Text text = Text("Label", rect, 20f, Body, new Vector2(0.5f, 0.5f), new Vector2(190f, 40f), Vector2.zero, TextAlignmentOptions.Center);
        text.text = label;
        return button;
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
