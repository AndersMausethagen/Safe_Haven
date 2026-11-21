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
    private RectTransform tag;
    private TMP_Text tagName;
    private TMP_Text tagFact;
    private TMP_Text keys;
    private TMP_Text backLabel;
    private RectTransform reticle;
    private RectTransform canvasRect;
    private float shown;
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
            tag.gameObject.SetActive(false);
            return;
        }

        tag.gameObject.SetActive(true);
        tagName.text = NameOf(thing);
        tagFact.text = FactOf(thing);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            // Beside the pointer, kept on the screen.
            Vector2 size = canvasRect.rect.size;
            Vector2 at = local + new Vector2(24f, -12f);
            at.x = Mathf.Clamp(at.x, -size.x * 0.5f, size.x * 0.5f - tag.sizeDelta.x);
            at.y = Mathf.Clamp(at.y, -size.y * 0.5f + tag.sizeDelta.y, size.y * 0.5f);
            tag.anchoredPosition = at;
        }
    }

    /// <summary>Who or what a pick is called.</summary>
    public static string NameOf(Component thing) => thing switch
    {
        ResidentNpc person when person.Resident != null => person.Resident.DisplayName,
        BuildingObject building when building.Definition != null => building.Definition.DisplayName,
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

        return "";
    }

    private static string Doing(ResidentLife life) => life.Activity switch
    {
        ResidentActivity.Work => life.IsBuilding ? "building" : "at work",
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
        tag = Box("Tag", root, new Vector2(0.5f, 0.5f), new Vector2(330f, 64f), Vector2.zero);
        tag.pivot = new Vector2(0f, 1f);
        tag.gameObject.AddComponent<Image>().raycastTarget = false;
        tag.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Inset);
        tagName = Label("Name", tag, 12f, 6f, 306f, 28f, 20f, style.Text);
        tagName.overflowMode = TextOverflowModes.Overflow;
        tagFact = Label("Fact", tag, 12f, 34f, 306f, 24f, 16f, style.Muted);
        tagFact.overflowMode = TextOverflowModes.Overflow;
        tag.gameObject.SetActive(false);

        // The controller's reticle: what is in the middle of the screen is what A picks.
        reticle = Box("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(22f, 22f), Vector2.zero);
        var dot = reticle.gameObject.AddComponent<Image>();
        dot.color = new Color(style.Accent.r, style.Accent.g, style.Accent.b, 0.85f);
        dot.raycastTarget = false;
        reticle.gameObject.SetActive(false);

        // Back to the player's own feet.
        RectTransform corner = Box("Back", root, new Vector2(1f, 0f), new Vector2(330f, 46f), new Vector2(-24f, 24f));
        corner.pivot = new Vector2(1f, 0f);
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
            backLabel.text = $"Back to your feet ({Key(GameInput.KeeperView)})";
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
