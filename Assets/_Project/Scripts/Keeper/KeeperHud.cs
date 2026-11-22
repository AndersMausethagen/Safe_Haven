using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// What the Keeper's view shows on screen (Docs/ColonyModeResearch_2026-11-21.md §4.4; laid out the way Gord, Manor Lords, Frostpunk
/// and Against the Storm keep a settlement in hand, the user's ask of 2026-11-22): the stores, the day and the Haven's standing along
/// the top left (KeeperStores), what needs attention under them (KeeperAlerts), the Haven's people down the right (KeeperPeople), the
/// orders in a slim bar along the bottom (KeeperCommandBar), and over the ground a card beside the pointer naming what is under it,
/// chips over the buildings with work ready, a field gone dry or a plan waiting, and the mark of a small scene. The view's name shows a
/// moment as it opens and fades; the keys sit small over the bar. Pictures and a word, the sentences on hover (KeeperTip). The
/// player's own HUD gives way meanwhile (HudGivesWay). Under the game's windows, so a station or a person's page opened from here
/// covers it. Built in code; one per scene.
/// </summary>
public sealed class KeeperHud : MonoBehaviour
{
    private const float TitleSeconds = 3.2f;
    private const float TagWidth = 360f;
    private const float TagHeight = 66f;

    private CanvasGroup group;
    private RectTransform canvasRect;
    // The chips over the ground (buildings' marks, a scene's), under every panel of the HUD.
    private RectTransform worldLayer;
    private KeeperTip tip;
    private KeeperCommandBar bar;
    private KeeperPeople people;
    private KeeperStores stores;
    private KeeperAlerts alerts;
    private TMP_Text title;
    private float openedAt = -100f;
    private RectTransform tagBox;
    private Image tagPicture;
    private TMP_Text tagInitial;
    private TMP_Text tagName;
    private TMP_Text tagFact;
    private TMP_Text keys;
    private RectTransform reticle;
    private RectTransform sceneChip;
    private TMP_Text sceneLabel;
    private float shown;
    private Component tagged;
    private float nextTagRead;
    private float nextBarRead;
    private float nextSideRead;
    private bool wanted;
    private int keysChanges = -1;

    /// <summary>The parts of the HUD that take clicks, for the overlap audit and probes.</summary>
    public IEnumerable<RectTransform> Parts
    {
        get
        {
            yield return bar.Root;
            yield return people.Root;
            yield return stores.Root;
            yield return alerts.Root;
        }
    }

    public static KeeperHud Create(Transform owner)
    {
        var root = new GameObject("Keeper's View HUD");
        root.transform.SetParent(owner, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var hud = root.AddComponent<KeeperHud>();
        hud.Build(root.transform);
        return hud;
    }

    /// <summary>Fades the HUD in or out; the view's name shows a moment as it opens.</summary>
    public void Show(bool show)
    {
        if (show && !wanted)
        {
            openedAt = Time.unscaledTime;
        }

        wanted = show;
        if (show)
        {
            gameObject.SetActive(true);
        }
        else
        {
            tip.Hide();
        }
    }

    /// <summary>The tag at the pointer: <paramref name="thing"/>'s picture, name and one fact, at <paramref name="screen"/>; none for null.</summary>
    public void Point(Component thing, Vector2 screen, bool reticleShown)
    {
        reticle.gameObject.SetActive(reticleShown);
        if (thing == null)
        {
            tagBox.gameObject.SetActive(false);
            return;
        }

        tagBox.gameObject.SetActive(true);
        // Its words a few times a second, not every frame.
        if (thing != tagged || Time.unscaledTime >= nextTagRead)
        {
            tagged = thing;
            nextTagRead = Time.unscaledTime + 0.25f;
            FillTag(thing);
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            // Beside the pointer, kept on the screen.
            Vector2 size = canvasRect.rect.size;
            Vector2 at = local + new Vector2(24f, -12f);
            at.x = Mathf.Clamp(at.x, -size.x * 0.5f, size.x * 0.5f - tagBox.sizeDelta.x);
            at.y = Mathf.Clamp(at.y, -size.y * 0.5f + tagBox.sizeDelta.y, size.y * 0.5f);
            tagBox.anchoredPosition = at;
        }
    }

    // The tag's picture (a portrait, a building's icon, else an initial), name and fact; as wide as its words.
    private void FillTag(Component thing)
    {
        Sprite picture = thing is ResidentNpc person && person.Resident != null ? person.Resident.Portrait
            : thing is BuildingObject building && building.Definition != null ? building.Definition.Icon : null;
        tagPicture.sprite = picture;
        tagPicture.enabled = picture != null;
        string name = TitleOf(thing);
        tagInitial.text = picture == null && name.Length > 0 ? name.Substring(0, 1) : "";
        tagName.text = name;
        tagFact.text = FactOf(thing);
        float words = Mathf.Max(tagName.GetPreferredValues(tagName.text).x, tagFact.GetPreferredValues(tagFact.text).x);
        float width = Mathf.Clamp(76f + Mathf.Ceil(words) + 16f, 220f, 560f);
        tagBox.sizeDelta = new Vector2(width, TagHeight);
        tagName.rectTransform.sizeDelta = new Vector2(width - 88f, 26f);
        tagFact.rectTransform.sizeDelta = new Vector2(width - 88f, 22f);
    }

    // ----- Chips over the buildings: work ready at a station, a field gone dry, a plan waiting for its materials (§4.4) -----

    private const int MostMarks = 12;
    private const float MarkLift = 4.5f;

    private sealed class Mark
    {
        public RectTransform Rect;
        public Image Rim;
        public Image Icon;
        public TMP_Text Words;
        public string Shown = "-";
    }

    private readonly List<Mark> marks = new List<Mark>();
    // The buildings the marks stand over, in order (refreshed with the text).
    private readonly List<BuildingObject> marked = new List<BuildingObject>();
    private float nextMarks;

    /// <summary>Puts a small chip over each station with work ready, each dry field and each plan in view (a few times a second).</summary>
    public void ShowMarks(Camera camera, bool show)
    {
        ShowScene(camera, show);
        if (!show || camera == null)
        {
            foreach (Mark mark in marks)
            {
                mark.Rect.gameObject.SetActive(false);
            }

            marked.Clear();
            return;
        }

        if (Time.unscaledTime >= nextMarks)
        {
            nextMarks = Time.unscaledTime + 0.25f;
            marked.Clear();
            foreach (BuildingObject building in BuildingObject.Active)
            {
                if (marked.Count >= MostMarks)
                {
                    break;
                }

                if (building != null && OnScreen(camera, building, out _) && MarkFor(building, out string icon, out string words, out bool warn))
                {
                    SetMark(MarkAt(marked.Count), icon, words, warn);
                    marked.Add(building);
                }
            }

            for (int i = marked.Count; i < marks.Count; i++)
            {
                marks[i].Rect.gameObject.SetActive(false);
            }
        }

        // Every frame, so a chip keeps over its building as the view pans and zooms.
        for (int i = 0; i < marked.Count; i++)
        {
            bool shownHere = false;
            if (marked[i] != null && OnScreen(camera, marked[i], out Vector3 viewport)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, camera.ViewportToScreenPoint(viewport), null, out Vector2 local))
            {
                marks[i].Rect.anchoredPosition = local;
                shownHere = true;
            }

            marks[i].Rect.gameObject.SetActive(shownHere);
        }
    }

    // The small scene's chip (SmallScenes) over the two friends, every frame so it keeps over them as the view moves; clicked, it is heard.
    private void ShowScene(Camera camera, bool show)
    {
        bool shownHere = false;
        if (show && camera != null && SmallScenes.Running(out Vector3 at, out string doing))
        {
            Vector3 viewport = camera.WorldToViewportPoint(at + Vector3.up * 2.6f);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, camera.ViewportToScreenPoint(viewport), null, out Vector2 local))
            {
                sceneChip.anchoredPosition = local;
                if (sceneLabel.text != doing)
                {
                    sceneLabel.text = doing;
                    float width = 44f + Mathf.Ceil(sceneLabel.GetPreferredValues(doing).x) + 14f;
                    sceneChip.sizeDelta = new Vector2(width, sceneChip.sizeDelta.y);
                    sceneLabel.rectTransform.sizeDelta = new Vector2(width - 50f, sceneChip.sizeDelta.y);
                }

                shownHere = true;
            }
        }

        if (sceneChip.gameObject.activeSelf != shownHere)
        {
            sceneChip.gameObject.SetActive(shownHere);
        }
    }

    private static bool OnScreen(Camera camera, BuildingObject building, out Vector3 viewport)
    {
        viewport = camera.WorldToViewportPoint(building.transform.position + Vector3.up * MarkLift);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // What a building's chip says, if anything: a plan, work ready, a dry field.
    private static bool MarkFor(BuildingObject building, out string icon, out string words, out bool warn)
    {
        icon = words = null;
        warn = false;
        if (building == null || !building.TryGetComponent(out Construction construction))
        {
            return false;
        }

        if (construction.IsPlan)
        {
            icon = "Hammer";
            words = "Plan";
            return true;
        }

        // Found, never made by looking (StationQueue's getter creates one).
        StationQueue queue = !construction.IsBuilding && building.TryGetComponent(out CraftingStation station) ? GameSession.Here.Workshops.Find(station.Id) : null;
        if (queue != null && queue.OutputAmount > 0)
        {
            icon = "Star";
            words = $"{queue.OutputAmount} ready";
            return true;
        }

        // A field whose soil has dried (Fields): its crop slows until someone waters it.
        if (Fields.Grows(queue) && !queue.IsIdle && queue.Water <= 0f)
        {
            icon = "Drop";
            words = "Dry";
            warn = true;
            return true;
        }

        return false;
    }

    /// <summary>The chip's words over <paramref name="building"/> ("3 ready", "Dry", "Plan"), or null.</summary>
    public static string MarkFor(BuildingObject building) => MarkFor(building, out _, out string words, out _) ? words : null;

    private Mark MarkAt(int index)
    {
        while (marks.Count <= index)
        {
            UIStyle style = UIStyle.Current;
            var mark = new Mark();
            mark.Rect = HearthKit.Card(worldLayer, "Mark", 0f, 0f, 120f, 30f, 0.6f, style.Panel, 2f);
            mark.Rect.anchorMin = mark.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            mark.Rect.pivot = new Vector2(0.5f, 0f);
            mark.Rim = mark.Rect.Find("Rim").GetComponent<Image>();
            mark.Icon = HearthKit.Icon(mark.Rect, "Mark", 8f, 5f, 20f);
            mark.Words = Label("Words", mark.Rect, 32f, 0f, 80f, 30f, 15f, style.Text, TextAlignmentOptions.MidlineLeft);
            mark.Words.overflowMode = TextOverflowModes.Overflow;
            marks.Add(mark);
        }

        marks[index].Rect.gameObject.SetActive(true);
        return marks[index];
    }

    private static void SetMark(Mark mark, string icon, string words, bool warn)
    {
        string state = icon + words + warn;
        if (state == mark.Shown)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        mark.Shown = state;
        mark.Icon.sprite = style.Icon(icon);
        mark.Icon.enabled = mark.Icon.sprite != null;
        mark.Icon.color = warn ? Color.Lerp(style.Warning, style.Accent, 0.2f) : HearthKit.IconColour(icon);
        mark.Words.text = words;
        mark.Words.color = warn ? Color.Lerp(style.Warning, style.Text, 0.3f) : style.Text;
        mark.Rim.color = HearthKit.Faded(warn ? style.Warning : style.Accent, warn ? 0.85f : 0.6f);
        float width = 32f + Mathf.Ceil(mark.Words.GetPreferredValues(words).x) + 12f;
        mark.Rect.sizeDelta = new Vector2(width, 30f);
        mark.Words.rectTransform.sizeDelta = new Vector2(width - 36f, 30f);
    }

    // ----- What a pick is -----

    /// <summary>The tag's first line: the name, and a wish for a home still waiting (HomeWishes).</summary>
    public static string TitleOf(Component thing)
    {
        string wish = thing is ResidentNpc person && person.Resident != null ? HomeWishes.WishOf(GameSession.Here.Find(person.Resident)) : null;
        return wish != null ? $"{NameOf(thing)} · wishes: {wish}" : NameOf(thing);
    }

    /// <summary>Who or what a pick is called.</summary>
    public static string NameOf(Component thing) => thing switch
    {
        ResidentNpc person when person.Resident != null => person.Resident.DisplayName,
        BuildingObject building when building.Definition != null => building.Definition.DisplayName,
        HomePortal _ => "The Portal",
        _ => thing != null ? thing.name : "",
    };

    /// <summary>One thing worth knowing at a glance: what a person is about, how far a scaffold has come, what a station is making.</summary>
    public static string FactOf(Component thing)
    {
        if (thing is ResidentNpc person)
        {
            // Their rank in their work, as a badge before the job (SettlementSettings › Worker Ranks).
            ResidentRecord record = person.Resident != null ? GameSession.Here.Find(person.Resident) : null;
            string rank = record != null && person.Job != null ? SettlementSettings.Current.RankFor(record.WorkDone).name : "";
            string job = person.Job != null ? (string.IsNullOrEmpty(rank) ? person.Job.DisplayName : $"{rank} {person.Job.DisplayName}") : "No work yet";
            return person.TryGetComponent(out ResidentLife life) ? $"{job} · {Doing(life)}" : job;
        }

        if (thing is BuildingObject building)
        {
            if (building.TryGetComponent(out Construction construction))
            {
                if (construction.IsPlan)
                {
                    return "Plan · waiting for its materials";
                }

                if (construction.IsBuilding)
                {
                    return $"Scaffold · {Mathf.RoundToInt(construction.Share * 100f)}% built";
                }

                // A house: who lives there.
                List<ResidentRecord> living = HavenHomes.LivingIn(building);
                if (living.Count > 0 && !building.TryGetComponent(out CraftingStation _))
                {
                    var names = new List<string>();
                    foreach (ResidentRecord one in living)
                    {
                        names.Add(one.Resident.DisplayName);
                    }

                    string who = names.Count <= 2 ? string.Join(" and ", names) : $"{names[0]}, {names[1]} and {names.Count - 2} more";
                    return $"Tier {Tiers.ToRoman(construction.Level)} · home of {who}";
                }

                // Kept for someone lost on a raid (EmptyHouses): dark until a rumour leads to them.
                if (EmptyHouses.StandsEmpty(building))
                {
                    return EmptyHouses.Tell(EmptyHouses.WaitingFor(GameSession.Here, building.Id));
                }

                if (building.TryGetComponent(out CraftingStation station) && station.Queue != null)
                {
                    StationQueue queue = station.Queue;
                    string making = queue.OutputAmount > 0 ? $"{queue.OutputAmount} ready" : queue.QueuedCount > 0 ? $"{queue.QueuedCount} queued" : "Idle";
                    return $"Tier {Tiers.ToRoman(construction.Level)} · {making}";
                }

                return $"Tier {Tiers.ToRoman(construction.Level)}";
            }

            return "Click to look closer";
        }

        if (thing is HomePortal)
        {
            return "Raids, the ward and the portal's tier";
        }

        return "";
    }

    private static string Doing(ResidentLife life) => life.Activity switch
    {
        ResidentActivity.Work => life.IsBuilding ? "building" : life.IsQuarrying ? "quarrying stone" : life.IsFelling ? "felling the ring" : life.IsTakingWorkBack ? "taking work to the chests" : life.IsFetchingWater ? "fetching water" : life.IsHauling ? "fetching for work" : "at work",
        ResidentActivity.Meal => "eating",
        ResidentActivity.Fire => life.IsInScene ? "with a friend" : "by the fire",
        ResidentActivity.Sleep => life.IsIndoors ? "asleep at home" : "going to bed",
        ResidentActivity.Shelter => "sheltering",
        ResidentActivity.Outing => "out for a walk",
        _ => "",
    };

    // ----- Building it -----

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        canvasRect = (RectTransform)root;
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        worldLayer = Stretch("World Marks", root);
        tip = new KeeperTip(canvasRect);

        // The view's name, carved over the ground for a moment as it opens (the display face, as a window's title).
        title = Label("Title", root, 0f, 0f, 760f, 64f, 40f, style.Text, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        // Below the strips along the top, where an area's name comes up.
        title.rectTransform.anchoredPosition = new Vector2(0f, -150f);
        title.overflowMode = TextOverflowModes.Overflow;
        title.text = "The Keeper's View";
        style.Display(title);

        stores = new KeeperStores(canvasRect, tip);
        alerts = new KeeperAlerts(canvasRect, tip);
        people = new KeeperPeople(canvasRect, tip);
        bar = new KeeperCommandBar(canvasRect, tip, Commands());

        // The keys, small, over the bar.
        keys = Label("Keys", root, 0f, 0f, 1100f, 24f, 15f, style.Text, TextAlignmentOptions.Center);
        keys.rectTransform.anchorMin = keys.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        keys.rectTransform.pivot = new Vector2(0.5f, 0f);
        keys.rectTransform.anchoredPosition = new Vector2(0f, bar.Root.anchoredPosition.y + bar.Root.sizeDelta.y + 8f);
        keys.overflowMode = TextOverflowModes.Overflow;
        style.OnWorld(keys);

        // The card beside the pointer: a picture, the name and one fact.
        tagBox = HearthKit.Card(root, "Tag", 0f, 0f, TagWidth, TagHeight, 0.55f, style.Panel);
        tagBox.anchorMin = tagBox.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform frame = HearthKit.Card(tagBox, "Frame", 8f, 8f, 50f, 50f, 0.6f, style.Inset);
        tagPicture = At("Picture", frame, 2f, 2f, 46f, 46f).gameObject.AddComponent<Image>();
        tagPicture.preserveAspect = true;
        tagPicture.raycastTarget = false;
        frame.Find("Rim").SetAsLastSibling();
        tagInitial = Label("Initial", frame, 0f, 0f, 50f, 50f, 26f, style.Accent, TextAlignmentOptions.Center);
        tagInitial.overflowMode = TextOverflowModes.Overflow;
        if (style.TitleFont != null)
        {
            tagInitial.font = style.TitleFont;
        }

        tagName = Label("Name", tagBox, 70f, 8f, TagWidth - 88f, 26f, 19f, style.Text);
        tagName.overflowMode = TextOverflowModes.Overflow;
        tagFact = Label("Fact", tagBox, 70f, 36f, TagWidth - 88f, 22f, 15f, style.Muted);
        tagFact.overflowMode = TextOverflowModes.Overflow;
        tagBox.gameObject.SetActive(false);

        // A small scene's chip over the two, clicked to hear them (SmallScenes).
        sceneChip = HearthKit.Card(worldLayer, "Scene Mark", 0f, 0f, 160f, 34f, 0.8f, style.ControlOn, 2f);
        sceneChip.anchorMin = sceneChip.anchorMax = new Vector2(0.5f, 0.5f);
        sceneChip.pivot = new Vector2(0.5f, 0f);
        Image sceneHit = sceneChip.GetComponent<Image>();
        sceneHit.raycastTarget = true;
        var sceneButton = sceneChip.gameObject.AddComponent<Button>();
        sceneButton.targetGraphic = sceneHit;
        HearthKit.Tint(sceneButton);
        UIHighlight.On(sceneChip.gameObject);
        sceneButton.onClick.AddListener(() =>
        {
            if (SmallScenes.Current != null)
            {
                SmallScenes.Current.Tell();
            }
        });
        var sceneHover = sceneChip.gameObject.AddComponent<UIHover>();
        sceneHover.Entered = () => tip.Show("A small scene", "Two friends in their free time. Click to hear them; those near are cheered when it ends.", sceneChip);
        sceneHover.Exited = tip.Hide;
        HearthKit.Icon(sceneChip, "Spark", 12f, 6f, 22f);
        sceneLabel = Label("Doing", sceneChip, 42f, 0f, 110f, 34f, 16f, style.AccentBright, TextAlignmentOptions.MidlineLeft);
        sceneLabel.overflowMode = TextOverflowModes.Overflow;
        sceneChip.gameObject.SetActive(false);

        // The controller's reticle: what is in the middle of the screen is what A picks.
        reticle = Box("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(22f, 22f), Vector2.zero);
        var dot = reticle.gameObject.AddComponent<Image>();
        dot.color = new Color(style.Accent.r, style.Accent.g, style.Accent.b, 0.85f);
        dot.raycastTarget = false;
        reticle.gameObject.SetActive(false);
    }

    // The orders, in their groups: the Haven's work, its defence, and the Haven itself.
    private static List<(string, KeeperCommandBar.Command[])> Commands()
    {
        KeeperView View() => KeeperView.Current;
        string Visiting(string what) => HavenVisit.IsVisiting ? $"{what} your own Haven's, not a friend's." : null;
        return new List<(string, KeeperCommandBar.Command[])>
        {
            ("Work", new[]
            {
                new KeeperCommandBar.Command
                {
                    Icon = "Axe", Caption = "Fell", Title = () => "Fell a ring of trees",
                    Why = () => KeeperView.WhyNoRing(RingKind.Fell), Lit = () => View() != null && View().IsDrawing(RingKind.Fell),
                    Count = () => KeeperView.RingsOf(RingKind.Fell),
                    Words = () => "Draw a ring on the ground: the woodcutters fell its grown trees when their bench has nothing to do, take the " +
                                  "logs to the chests and plant saplings behind.",
                    Run = () => Toggle(View(), RingKind.Fell),
                },
                new KeeperCommandBar.Command
                {
                    Icon = "Pickaxe", Caption = "Quarry", Title = () => "Quarry a ring of stone",
                    Why = () => KeeperView.WhyNoRing(RingKind.Quarry), Lit = () => View() != null && View().IsDrawing(RingKind.Quarry),
                    Count = () => KeeperView.RingsOf(RingKind.Quarry),
                    Words = () => "Draw a ring round boulders or an outcrop: the masons break what a Tier I pickaxe can and carry the stone to the " +
                                  "chests. Stone that grows back keeps the quarry open.",
                    Run = () => Toggle(View(), RingKind.Quarry),
                },
                new KeeperCommandBar.Command
                {
                    Icon = "Cross", Caption = "Clear", Title = () => "Clear the rings",
                    Why = () => Visiting("Rings are") ?? (GameSession.Here.Rings.Count == 0 ? "No rings are drawn." : null),
                    Count = () => GameSession.Here.Rings.Count,
                    Words = () => "Every ring goes; the woodcutters and masons go back to their own work.",
                    Run = () => View()?.ClearRings(),
                },
            }),
            ("Defence", new[]
            {
                new KeeperCommandBar.Command
                {
                    Icon = "Flag", Caption = "Post", Title = () => "Set a guard post",
                    Why = KeeperView.WhyNoPost, Lit = () => View() != null && View().IsSettingPost, Count = () => GameSession.Here.Posts.Count,
                    Words = () => $"Where a guard stands when called out: a breach, a corruption night or the horn. Up to {GuardPosts.Cap(DefenceSettings.Current)} " +
                                  "while the defences built hold that many.",
                    Run = () =>
                    {
                        KeeperView view = View();
                        if (view != null && view.IsSettingPost)
                        {
                            view.StopPlacing();
                        }
                        else
                        {
                            view?.BeginPost();
                        }
                    },
                },
                new KeeperCommandBar.Command
                {
                    Icon = "Horn", Caption = "Horn", Title = () => HavenHorn.IsSounding ? "Stand them down" : "Sound the horn",
                    Why = () => HavenHorn.IsSounding ? null : HavenHorn.WhyNot(), Lit = () => HavenHorn.IsSounding,
                    Words = () => HavenHorn.IsSounding
                        ? "Sound it again, twice, and everyone goes back to their day."
                        : $"For {Mathf.RoundToInt(DefenceSettings.Current.HornSeconds)} seconds the guards go to their posts and everyone else to the portal.",
                    Run = () => View()?.SoundHorn(),
                },
                new KeeperCommandBar.Command
                {
                    Icon = "Cross", Caption = "Clear", Title = () => "Clear the posts",
                    Why = () => Visiting("Posts are") ?? (GameSession.Here.Posts.Count == 0 ? "No posts are set." : null),
                    Count = () => GameSession.Here.Posts.Count,
                    Words = () => "Every post goes; called out, the guards stand where they did before posts.",
                    Run = () => View()?.ClearPosts(),
                },
            }),
            ("Haven", new[]
            {
                new KeeperCommandBar.Command
                {
                    Icon = "Hammer", Caption = "Build", Title = () => "Build",
                    Why = () => Visiting("Building is"),
                    Words = () => "The build menu: place a building here, or lay out a plan the builders raise once the chests hold its materials.",
                    Run = () => View()?.OpenBuildMenu(),
                },
                new KeeperCommandBar.Command
                {
                    Icon = "House", Caption = "Haven", Title = () => "The Haven",
                    Words = () => $"Its people, buildings, lands and stores ({GameInput.KeyLabel(GameInput.Haven)}).",
                    Run = () =>
                    {
                        if (HavenWindow.Current != null)
                        {
                            HavenWindow.Current.Open();
                        }
                    },
                },
                new KeeperCommandBar.Command
                {
                    Icon = "Return", Caption = "Back", Title = () => "Back to your feet",
                    Words = () => $"The camera comes back down to your character ({GameInput.KeyLabel(InputDevices.UsingGamepad ? GameInput.Back : GameInput.KeeperView)}).",
                    Run = () => View()?.Close(null),
                },
            }),
        };
    }

    // A ring's button: begins drawing one, or stops when it is already being drawn.
    private static void Toggle(KeeperView view, RingKind kind)
    {
        if (view == null)
        {
            return;
        }

        if (view.IsDrawing(kind))
        {
            view.StopPlacing();
        }
        else
        {
            view.BeginRing(kind);
        }
    }

    private void Update()
    {
        shown = Mathf.MoveTowards(shown, wanted ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        group.alpha = shown;
        group.blocksRaycasts = shown > 0.5f;
        if (!wanted && shown <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        // The view's name, a moment as it opens, then away.
        float since = Time.unscaledTime - openedAt;
        title.alpha = Mathf.Clamp01(1f - (since - TitleSeconds) / 0.8f);

        if (Time.unscaledTime >= nextBarRead)
        {
            nextBarRead = Time.unscaledTime + 0.25f;
            bar.Refresh();
        }

        if (Time.unscaledTime >= nextSideRead)
        {
            nextSideRead = Time.unscaledTime + 0.5f;
            stores.Refresh();
            alerts.Refresh();
            people.Refresh();
        }

        // Key names follow a rebind and the device in hand.
        int changes = InputRebinds.Changes * 2 + (InputDevices.UsingGamepad ? 1 : 0);
        if (changes != keysChanges)
        {
            keysChanges = changes;
            keys.text = InputDevices.UsingGamepad
                ? $"{Key(GameInput.KeeperPan)} pan   ·   {Key(GameInput.CameraLookStick)} turn and zoom   ·   {Key(GameInput.KeeperSelect)} pick   ·   " +
                  $"{Key(GameInput.KeeperFollow)} follow   ·   {Key(GameInput.KeeperFindMe)} your character   ·   {Key(GameInput.Back)} back"
                : $"{Key(GameInput.KeeperPan)} pan   ·   {Key(GameInput.KeeperTurn)} turn   ·   wheel zoom   ·   {Key(GameInput.KeeperSelect)} pick   ·   " +
                  $"{Key(GameInput.KeeperFollow)} follow   ·   {Key(GameInput.KeeperFindMe)} your character";
        }
    }

    private static string Key(UnityEngine.InputSystem.InputAction action)
    {
        if (action == null)
        {
            return "";
        }

        string single = GameInput.KeyLabel(action);
        return single.Length > 0 || InputDevices.UsingGamepad ? single : CompositeLabel(action);
    }

    // Keys in a composite (WASD, Q/E), as the keyboard shows them and after any rebind: GameInput.KeyLabel names single keys only.
    private static string CompositeLabel(UnityEngine.InputSystem.InputAction action)
    {
        var parts = new List<string>();
        UnityEngine.InputSystem.InputBinding group = UnityEngine.InputSystem.InputBinding.MaskByGroup("Keyboard&Mouse");
        bool inFirst = false;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            UnityEngine.InputSystem.InputBinding binding = action.bindings[i];
            if (binding.isComposite)
            {
                if (inFirst)
                {
                    break;
                }

                continue;
            }

            if (binding.isPartOfComposite && group.Matches(binding))
            {
                inFirst = true;
                parts.Add(action.GetBindingDisplayString(i, UnityEngine.InputSystem.InputBinding.DisplayStringOptions.DontIncludeInteractions));
            }
        }

        // Up, down, left, right read as W S A D: in the order players know (W A S D).
        if (parts.Count == 4)
        {
            parts = new List<string> { parts[0], parts[2], parts[1], parts[3] };
        }

        bool letters = parts.TrueForAll(part => part.Length == 1);
        return string.Join(letters && parts.Count == 4 ? "" : "/", parts);
    }
}
