using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Safe Haven's standing at the top of the screen while at home: its rank, renown towards the next rank on a thin bar,
/// "+6 Renown" rising when something is built, upgraded or cleared, and a banner across the screen when a new rank is
/// reached. Built in code by HavenProgress; it only reads it and GameSession.Here.
/// <para>
/// Also the store prompt (the user, 2026-11-20, "the same way Palworld does it"): in the bottom-right corner, above the minimap, the key
/// that sends the bag to the chests (StashQuickStack) shows whenever the player is inside the ward with something the chests would take.
/// </para>
/// </summary>
public sealed class HavenHud : MonoBehaviour
{
    private static Color Gold => UIStyle.Current.Accent;
    private static Color Body => UIStyle.Current.Text;
    private const float GainSeconds = 2f;
    private const float BannerSeconds = 5f;
    private const float BadgeWidth = 390f;
    private const float GainY = -14f;
    private const float StandingSeconds = 0.5f;
    private const float StoreCheckSeconds = 0.4f;
    private const float StoreWidth = 300f;
    private const float StoreHeight = 62f;
    private const float StoreMargin = 26f;

    private TMP_Text badge;
    private TMP_Text standing;
    private RectTransform fill;
    private TMP_Text gain;
    private CanvasGroup banner;
    private TMP_Text bannerTitle;
    private TMP_Text bannerLine;
    private CanvasGroup group;
    private float gainAt = -10f;
    private float bannerAt = -10f;
    private float nextStanding;
    private int shownGain;
    private int writtenGain = -1;
    private RectTransform storePrompt;
    private RectTransform lineCard;
    private TMP_Text lineText;
    private CanvasGroup lineGroup;
    private CanvasGroup storeGroup;
    private TMP_Text storeKey;
    private TMP_Text storeCount;
    private float nextStoreCheck;
    private bool storeWanted;

    public static HavenHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public static HavenHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Haven HUD");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        var hud = root.AddComponent<HavenHud>();
        hud.group = root.AddComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false;
        hud.Build(root.transform);
        return hud;
    }

    private void Build(Transform root)
    {
        // Top left, under the vitals' margin: the day and weather panel has the top centre.
        RectTransform top = Rect("Badge", root, new Vector2(0f, 1f), new Vector2(BadgeWidth, 52f), new Vector2(30f + BadgeWidth * 0.5f, -40f));
        const float markSpace = 38f;
        HearthlineArt.AddIcon(top, "Hearthmark", UIStyle.Current.Hearthmark, new Vector2(0f, 1f), Vector2.one * 30f,
            new Vector2(15f, -18f), Gold, true);
        badge = Label(top, "Rank", new Vector2(0f, 1f), new Vector2(BadgeWidth - markSpace, 32f),
            new Vector2((BadgeWidth + markSpace) * 0.5f, -18f), 24f, Gold, TextAlignmentOptions.Left, true);
        standing = Label(top, "Standing", new Vector2(1f, 1f), new Vector2(BadgeWidth, 32f), new Vector2(BadgeWidth * -0.5f, -20f), 17f, Body, TextAlignmentOptions.Right);
        RectTransform bar = Rect("Bar", top, new Vector2(0.5f, 0f), new Vector2(BadgeWidth, 2f), new Vector2(0f, 12f));
        bar.gameObject.AddComponent<Image>().color = UIStyle.Current.Track;
        fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = Gold;
        gain = Label(top, "Gain", new Vector2(0f, 0f), new Vector2(BadgeWidth, 30f), new Vector2(BadgeWidth * 0.5f, GainY), 20f, UIStyle.Current.Good, TextAlignmentOptions.Left);

        RectTransform plate = Rect("Rank Banner", root, new Vector2(0.5f, 0.5f), new Vector2(1100f, 190f), new Vector2(0f, 190f));
        banner = plate.gameObject.AddComponent<CanvasGroup>();
        banner.alpha = 0f;
        HearthlineArt.AddIcon(plate, "Hearthmark", UIStyle.Current.Hearthmark, new Vector2(0.5f, 1f), Vector2.one * 34f,
            Vector2.zero, Gold, true);
        TMP_Text kicker = Label(plate, "Kicker", new Vector2(0.5f, 1f), new Vector2(1000f, 30f), new Vector2(0f, -30f), 20f, Body);
        kicker.text = "SAFE HAVEN HAS GROWN";
        kicker.characterSpacing = 12f;
        bannerTitle = Label(plate, "Title", new Vector2(0.5f, 0.5f), new Vector2(1000f, 76f), new Vector2(0f, 8f), 60f, Gold, TextAlignmentOptions.Center, true);
        bannerLine = Label(plate, "Line", new Vector2(0.5f, 0f), new Vector2(1000f, 34f), new Vector2(0f, 34f), 24f, Body);
        foreach (float y in new[] { 0f, 1f })
        {
            RectTransform line = Rect("Line", plate, new Vector2(0.5f, y), new Vector2(460f, 1f), Vector2.zero);
            line.gameObject.AddComponent<Image>().color = Gold;
        }

        BuildStorePrompt(root);
        BuildLineCard(root);
    }

    // A dark card with a gold rim: the key in a keycap, the chest's painting, what it does and how much would go.
    private void BuildStorePrompt(Transform root)
    {
        UIStyle style = UIStyle.Current;
        storePrompt = Rect("Store Prompt", root, new Vector2(1f, 0f), new Vector2(StoreWidth, StoreHeight), Vector2.zero);
        storePrompt.pivot = new Vector2(1f, 0f);
        storeGroup = storePrompt.gameObject.AddComponent<CanvasGroup>();
        storeGroup.alpha = 0f;
        storeGroup.blocksRaycasts = false;
        Image card = HearthKit.Sliced(storePrompt.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        card.color = HearthKit.Faded(style.Inset, 0.92f);
        card.raycastTarget = false;
        HearthKit.Rim(storePrompt, 0.45f);
        RectTransform cap = Rect("Key", storePrompt, new Vector2(0f, 0.5f), new Vector2(46f, 30f), new Vector2(31f, 0f));
        Image capFace = HearthKit.Sliced(cap.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        capFace.color = HearthKit.Faded(Gold, 0.18f);
        capFace.raycastTarget = false;
        HearthKit.Rim(cap, 0.8f);
        storeKey = Label(cap, "Key Label", new Vector2(0.5f, 0.5f), new Vector2(46f, 30f), Vector2.zero, 18f, Gold, TextAlignmentOptions.Center, true);
        RectTransform chest = Rect("Chest", storePrompt, new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(86f, 0f));
        Image picture = chest.gameObject.AddComponent<Image>();
        picture.sprite = ChestPicture();
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        picture.enabled = picture.sprite != null;
        const float textX = 118f;
        float textWidth = StoreWidth - textX - 12f;
        TMP_Text what = Label(storePrompt, "Label", new Vector2(0f, 0.5f), new Vector2(textWidth, 26f), new Vector2(textX + textWidth * 0.5f, 10f), 18f, Body, TextAlignmentOptions.Left);
        what.text = "Store in chests";
        storeCount = Label(storePrompt, "Count", new Vector2(0f, 0.5f), new Vector2(textWidth, 22f), new Vector2(textX + textWidth * 0.5f, -13f), 14f, HearthKit.Faded(Body, 0.7f), TextAlignmentOptions.Left);
    }

    // While a wall or fence is drawn: its pieces and their whole cost, above the skill bar (BuildPlacer.LineSummary).
    private void BuildLineCard(Transform root)
    {
        UIStyle style = UIStyle.Current;
        lineCard = Rect("Line Cost", root, new Vector2(0.5f, 0f), new Vector2(720f, 46f), new Vector2(0f, 150f));
        lineGroup = lineCard.gameObject.AddComponent<CanvasGroup>();
        lineGroup.alpha = 0f;
        lineGroup.blocksRaycasts = false;
        Image card = HearthKit.Sliced(lineCard.gameObject.AddComponent<Image>(), style.Card, 1.2f);
        card.color = HearthKit.Faded(style.Inset, 0.92f);
        card.raycastTarget = false;
        HearthKit.Rim(lineCard, 0.45f);
        Image icon = HearthKit.Icon(lineCard, "Fence", 12f, 9f, 28f);
        icon.raycastTarget = false;
        lineText = Label(lineCard, "Text", new Vector2(0f, 0.5f), new Vector2(660f, 34f), new Vector2(50f + 330f, 0f), 17f, Body, TextAlignmentOptions.Left);
        lineText.richText = true;
    }

    private string shownLine;

    private void UpdateLineCard()
    {
        if (lineCard == null)
        {
            return;
        }

        string summary = BuildPlacer.LineSummary;
        if (summary.Length > 0 && summary != shownLine)
        {
            shownLine = summary;
            lineText.text = HearthKit.ToneNumbers(summary);
        }

        lineGroup.alpha = Mathf.MoveTowards(lineGroup.alpha, summary.Length > 0 ? 1f : 0f, Time.unscaledDeltaTime * 6f);
    }

    // The Small Chest's painting (its build-menu icon); none if it can't be found.
    private static Sprite ChestPicture()
    {
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (BuildingDefinition building in content != null ? content.Buildings : (IReadOnlyList<BuildingDefinition>)System.Array.Empty<BuildingDefinition>())
        {
            if (building != null && building.name == "SmallChest")
            {
                return building.Icon;
            }
        }

        return null;
    }

    // Shown while the bag holds something the chests would take (StashQuickStack.HasSomethingToStore), above the minimap so the two
    // never cover each other whatever the HUD size option makes the map.
    private void UpdateStorePrompt(bool windows)
    {
        if (storePrompt == null)
        {
            return;
        }

        if (Time.unscaledTime >= nextStoreCheck)
        {
            nextStoreCheck = Time.unscaledTime + StoreCheckSeconds;
            Player player = Player.Current;
            int toStore = player != null ? StashQuickStack.CountToStore(player.gameObject) : 0;
            storeWanted = toStore > 0;
            if (storeWanted)
            {
                storeKey.text = GameInput.KeyLabel(GameInput.StackOnStored);
                storeCount.text = $"{toStore} thing{(toStore == 1 ? "" : "s")} from your bag";
                float map = MinimapSettings.Current != null ? MinimapSettings.Current.Size * HudRules.MinimapScale : 220f;
                // Clear of the place name the minimap shows above itself (Haven Meadows).
                storePrompt.anchoredPosition = new Vector2(-StoreMargin, StoreMargin + map + 62f);
            }
        }

        float target = storeWanted && !windows && !BuildPlacer.IsBusy ? 1f : 0f;
        storeGroup.alpha = Mathf.MoveTowards(storeGroup.alpha, target, Time.unscaledDeltaTime * 4f);
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public void ShowGain(int amount)
    {
        shownGain = Time.unscaledTime - gainAt < GainSeconds * 0.6f ? shownGain + amount : amount;
        gainAt = Time.unscaledTime;
    }

    public void ShowRank(string name, string line)
    {
        bannerTitle.text = name;
        bannerLine.text = line;
        bannerAt = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        bool windows = UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen;
        group.alpha = GamePause.HidesHud ? 0f : 1f;
        badge.transform.parent.gameObject.SetActive(!windows);
        UpdateStorePrompt(windows);
        UpdateLineCard();
        if (Time.unscaledTime >= nextStanding)
        {
            DrawStanding();
        }

        float age = Time.unscaledTime - gainAt;
        gain.gameObject.SetActive(age < GainSeconds);
        if (age < GainSeconds)
        {
            if (shownGain != writtenGain)
            {
                writtenGain = shownGain;
                gain.text = $"+{shownGain} Renown";
            }

            gain.alpha = 1f - Mathf.Clamp01((age - GainSeconds * 0.5f) / (GainSeconds * 0.5f));
            gain.rectTransform.anchoredPosition = new Vector2(BadgeWidth * 0.5f, GainY - age * 10f);
        }

        float shown = Time.unscaledTime - bannerAt;
        banner.alpha = shown < 0.4f ? shown / 0.4f : shown < BannerSeconds - 1f ? 1f : Mathf.Clamp01(BannerSeconds - shown);
        float pop = shown < 0.4f ? Mathf.Lerp(1.15f, 1f, shown / 0.4f) : 1f;
        banner.transform.localScale = Vector3.one * pop;
    }

    // Not every frame: renown is only counted every two seconds, and the key's label walks its bindings.
    private void DrawStanding()
    {
        nextStanding = Time.unscaledTime + StandingSeconds;
        HavenSettings settings = HavenSettings.Current;
        int renown = HavenProgress.Current != null ? HavenProgress.Current.Renown : 0;
        int rank = Mathf.Max(GameSession.Here.HavenRank, settings.RankFor(renown));
        bool last = rank >= settings.Ranks.Count - 1;
        int from = settings.Ranks.Count > 0 ? settings.Ranks[rank].renown : 0;
        int to = last ? from : settings.Ranks[rank + 1].renown;
        badge.text = settings.Ranks.Count == 0 ? "" : settings.Ranks[rank].name;
        string key = GameInput.KeyLabel(GameInput.Haven);
        standing.text = last ? $"{renown} renown  <alpha=#99>{key}" : $"{renown} / {to} renown  <alpha=#99>{key}";
        fill.anchorMax = new Vector2(last ? 1f : Mathf.Clamp01((renown - from) / (float)Mathf.Max(1, to - from)), 1f);
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

    private static TMP_Text Label(Transform parent, string name, Vector2 anchorAt, Vector2 size, Vector2 position, float fontSize, Color colour,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool title = false)
    {
        var text = Rect(name, parent, anchorAt, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        UIStyle.Current.OnWorld(text, title);
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
