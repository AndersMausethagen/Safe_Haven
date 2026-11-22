using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// What the Keeper's view shows on screen (Docs/ColonyModeResearch_2026-11-21.md §4.4): a slim strip at the top naming the view
/// and its keys, a tag beside the pointer with the name of the person or building under it and one fact about it, a reticle in
/// the middle on a controller, and a button back to the player's feet. The player's own HUD gives way meanwhile (HudGivesWay).
/// Under the game's windows, so a station or a person's page opened from here covers it. Built in code; one per scene.
/// </summary>
public sealed class KeeperHud : MonoBehaviour
{
    private const float StripWidth = 900f;
    private const float StripHeight = 76f;

    private CanvasGroup group;
    private RectTransform tagBox;
    private TMP_Text tagName;
    private TMP_Text tagFact;
    private TMP_Text keys;
    private TMP_Text backLabel;
    private TMP_Text ringLabel;
    private string shownNoRing = "-";
    private int shownRings = -1;
    private TMP_Text clearLabel;
    private Button clearButton;
    private RectTransform reticle;
    private RectTransform canvasRect;
    private float shown;
    private Component tagged;
    private float nextTagRead;
    private bool wanted;
    private int keysChanges = -1;

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

    /// <summary>Fades the HUD in or out.</summary>
    public void Show(bool show)
    {
        wanted = show;
        if (show)
        {
            gameObject.SetActive(true);
        }
    }

    /// <summary>The tag at the pointer: <paramref name="thing"/>'s name and one fact, at <paramref name="screen"/>; none for null.</summary>
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
            tagName.text = NameOf(thing);
            tagFact.text = FactOf(thing);
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

    // ----- Marks over the buildings: work ready at a station, a plan waiting for its materials (Docs/ColonyModeResearch §4.4) -----

    private const int MostMarks = 12;
    private const float MarkLift = 4.5f;
    private readonly System.Collections.Generic.List<TMP_Text> marks = new System.Collections.Generic.List<TMP_Text>();
    // The buildings the marks stand over, in order (refreshed with the text).
    private readonly System.Collections.Generic.List<BuildingObject> marked = new System.Collections.Generic.List<BuildingObject>();
    private float nextMarks;

    /// <summary>Puts a small label over each station with work ready and each plan in view (a few times a second).</summary>
    public void ShowMarks(Camera camera, bool show)
    {
        if (!show || camera == null)
        {
            foreach (TMP_Text mark in marks)
            {
                mark.transform.parent.gameObject.SetActive(false);
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

                string text = building != null && OnScreen(camera, building, out _) ? MarkFor(building) : null;
                if (text != null)
                {
                    Mark(marked.Count).text = text;
                    marked.Add(building);
                }
            }

            for (int i = marked.Count; i < marks.Count; i++)
            {
                marks[i].transform.parent.gameObject.SetActive(false);
            }
        }

        // Every frame, so a mark keeps over its building as the view pans and zooms.
        for (int i = 0; i < marked.Count; i++)
        {
            GameObject box = marks[i].transform.parent.gameObject;
            bool shown = marked[i] != null && OnScreen(camera, marked[i], out Vector3 viewport)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, camera.ViewportToScreenPoint(viewport), null, out Vector2 local)
                && PutAt(marks[i], local);
            box.SetActive(shown);
        }
    }

    private static bool OnScreen(Camera camera, BuildingObject building, out Vector3 viewport)
    {
        viewport = camera.WorldToViewportPoint(building.transform.position + Vector3.up * MarkLift);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private static bool PutAt(TMP_Text label, Vector2 local)
    {
        ((RectTransform)label.transform.parent).anchoredPosition = local;
        return true;
    }

    private static string MarkFor(BuildingObject building)
    {
        if (building == null || !building.TryGetComponent(out Construction construction))
        {
            return null;
        }

        if (construction.IsPlan)
        {
            return "Plan";
        }

        // Found, never made by looking (StationQueue's getter creates one).
        StationQueue queue = !construction.IsBuilding && building.TryGetComponent(out CraftingStation station) ? GameSession.Here.Workshops.Find(station.Id) : null;
        if (queue != null && queue.OutputAmount > 0)
        {
            return $"{queue.OutputAmount} ready";
        }

        // A field whose soil has dried (Fields): its crop slows until someone waters it.
        if (Fields.Grows(queue) && !queue.IsIdle && queue.Water <= 0f)
        {
            return "Dry";
        }

        return null;
    }

    private TMP_Text Mark(int index)
    {
        while (marks.Count <= index)
        {
            UIStyle style = UIStyle.Current;
            RectTransform at = Box("Mark", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(160f, 30f), Vector2.zero);
            at.gameObject.AddComponent<Image>().raycastTarget = false;
            at.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Inset);
            TMP_Text text = Label("Text", at, 0f, 3f, 160f, 24f, 16f, style.Accent, TextAlignmentOptions.Center);
            text.overflowMode = TextOverflowModes.Overflow;
            marks.Add(text);
        }

        TMP_Text mark = marks[index];
        mark.transform.parent.gameObject.SetActive(true);
        mark.gameObject.SetActive(true);
        return mark;
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
            string job = person.Job != null ? person.Job.DisplayName : "No work yet";
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
        ResidentActivity.Work => life.IsBuilding ? "building" : life.IsFelling ? "felling the ring" : life.IsTakingWorkBack ? "taking work to the chests" : life.IsHauling ? "fetching for work" : "at work",
        ResidentActivity.Meal => "eating",
        ResidentActivity.Fire => "by the fire",
        ResidentActivity.Sleep => life.IsIndoors ? "asleep at home" : "going to bed",
        ResidentActivity.Shelter => "sheltering",
        ResidentActivity.Outing => "out for a walk",
        _ => "",
    };

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        canvasRect = (RectTransform)root;
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // The strip along the top: the view's name and its keys.
        RectTransform strip = Box("Strip", root, new Vector2(0.5f, 1f), new Vector2(StripWidth, StripHeight), new Vector2(0f, -18f));
        strip.pivot = new Vector2(0.5f, 1f);
        strip.gameObject.AddComponent<Image>();
        strip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        TMP_Text title = Label("Title", strip, 0f, 8f, StripWidth, 32f, 24f, style.Accent, TextAlignmentOptions.Center);
        title.font = style.TitleFont != null ? style.TitleFont : title.font;
        title.text = "The Keeper's View";
        keys = Label("Keys", strip, 0f, 42f, StripWidth, 26f, 16f, style.Muted, TextAlignmentOptions.Center);
        keys.overflowMode = TextOverflowModes.Overflow;

        // The tag beside the pointer.
        tagBox = Box("Tag", root, new Vector2(0.5f, 0.5f), new Vector2(330f, 64f), Vector2.zero);
        tagBox.pivot = new Vector2(0f, 1f);
        tagBox.gameObject.AddComponent<Image>().raycastTarget = false;
        tagBox.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Inset);
        tagName = Label("Name", tagBox, 12f, 6f, 306f, 28f, 20f, style.Text);
        tagName.overflowMode = TextOverflowModes.Overflow;
        tagFact = Label("Fact", tagBox, 12f, 34f, 306f, 24f, 16f, style.Muted);
        tagFact.overflowMode = TextOverflowModes.Overflow;
        tagBox.gameObject.SetActive(false);

        // The controller's reticle: what is in the middle of the screen is what A picks.
        reticle = Box("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(22f, 22f), Vector2.zero);
        var dot = reticle.gameObject.AddComponent<Image>();
        dot.color = new Color(style.Accent.r, style.Accent.g, style.Accent.b, 0.85f);
        dot.raycastTarget = false;
        reticle.gameObject.SetActive(false);

        // Back to the player's own feet.
        RectTransform corner = Box("Back", root, new Vector2(1f, 0f), new Vector2(330f, 46f), new Vector2(-24f, 24f));
        corner.pivot = new Vector2(1f, 0f);
        // The woodcutters' rings (RingWork): draw one, or clear them.
        RectTransform ringCorner = Box("Rings", root, new Vector2(1f, 0f), new Vector2(330f, 102f), new Vector2(-24f, 78f));
        ringCorner.pivot = new Vector2(1f, 0f);
        ringLabel = AddButton("Ring Button", ringCorner, 0f, 0f, 330f, UIRole.Kind.Button, () =>
        {
            if (KeeperView.Current != null)
            {
                KeeperView.Current.BeginRing();
            }
        }, out _);
        clearLabel = AddButton("Clear Rings Button", ringCorner, 0f, 52f, 330f, UIRole.Kind.Button, () =>
        {
            if (KeeperView.Current != null)
            {
                KeeperView.Current.ClearRings();
            }
        }, out clearButton);

        backLabel = AddButton("Back Button", corner, 0f, 0f, 330f, UIRole.Kind.Button, () =>
        {
            if (KeeperView.Current != null)
            {
                KeeperView.Current.Close(null);
            }
        }, out _);
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

        // The ring buttons, written again only when what they say changes.
        string noRing = KeeperView.WhyNoRing();
        int rings = GameSession.Here.Rings.Count;
        if (noRing != shownNoRing || rings != shownRings)
        {
            shownNoRing = noRing;
            shownRings = rings;
            ringLabel.text = noRing == null ? "Fell a ring of trees" : $"Fell a ring ({KeeperView.ShortWhyNoRing()})";
            clearButton.gameObject.SetActive(rings > 0 && !HavenVisit.IsVisiting);
            clearLabel.text = $"Clear the ring{(rings == 1 ? "" : "s")} ({rings})";
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
            backLabel.text = $"Back to your feet ({Key(InputDevices.UsingGamepad ? GameInput.Back : GameInput.KeeperView)})";
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
        var parts = new System.Collections.Generic.List<string>();
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
            parts = new System.Collections.Generic.List<string> { parts[0], parts[2], parts[1], parts[3] };
        }

        bool letters = parts.TrueForAll(part => part.Length == 1);
        return string.Join(letters && parts.Count == 4 ? "" : "/", parts);
    }
}
