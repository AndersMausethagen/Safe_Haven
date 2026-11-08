using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What fishing shows on screen: the swing-back bar while charging a cast, a "!" over the float with "Strike!" when a
/// fish bites, and with a fish on the fight (FishFight): the line's tension on a tall meter (safe in the middle, the
/// breaking point at the top, flashing as it strains), the fish's strength draining beside it, the line out, one hint
/// at a time for what the fish is doing, and on the water an arrow by the float that shows which way to lean the rod
/// when it runs. When the Nøkken has the player, a pulsing "SPACE!" with how close they are to breaking free. Built in
/// code by PlayerFishing; it only reads PlayerFishing and NokkenEncounter. Hearthline: type and thin lines, UIStyle's
/// colours.
/// </summary>
public sealed class FishingHud : MonoBehaviour
{
    private const float MeterHeight = 300f;
    private const float SafeLow = 0.2f;
    private const float SafeHigh = 0.85f;
    private static Color Gold => UIStyle.Current.Accent;
    private static Color Body => UIStyle.Current.Text;

    private RectTransform root;
    private GameObject charge;
    private RectTransform chargeFill;
    private RectTransform strike;
    private GameObject fightPanel;
    private RectTransform needle;
    private Image needleImage;
    private Image dangerImage;
    private RectTransform staminaFill;
    private Image fishIcon;
    private TMP_Text lineText;
    private TMP_Text hintText;
    private RectTransform lean;
    private Image leanImage;
    private RectTransform warn;
    private GameObject struggle;
    private TMP_Text struggleText;
    private RectTransform struggleFill;
    private int writtenTenths = -1;
    private int writtenMetres = -1;
    private string writtenHint;

    public static FishingHud Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public static FishingHud Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var go = new GameObject("Fishing HUD");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        UIRoot.ConfigureScaler(go.AddComponent<CanvasScaler>());
        var hud = go.AddComponent<FishingHud>();
        hud.root = (RectTransform)go.transform;
        hud.Build();
        return hud;
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>The hint for a moment of the fight, or null for none (tests read it too).</summary>
    public static string HintFor(FishFight fight, bool reeling)
    {
        if (fight == null)
        {
            return null;
        }

        if (fight.Spent)
        {
            return "It's tired. Reel it in!";
        }

        if (fight.Strain > 0.15f || fight.Tension >= 0.95f)
        {
            return "The line's straining! Let go of the reel";
        }

        switch (fight.Current)
        {
            case FishFight.Mood.Warning:
                return "It's about to run...";
            case FishFight.Mood.Running:
                return fight.RunSide != 0 ? "It runs! Lean the rod against it" : "It runs! Give it line";
            default:
                return fight.Slack > 0.25f ? "The line's slack! Reel in" : reeling ? "Keep reeling" : "Hold left click to reel in";
        }
    }

    private void Build()
    {
        UIStyle style = UIStyle.Current;
        // Swing-back bar above the action bar.
        RectTransform chargeBar = Panel("Charge", root, new Vector2(0.5f, 0f), new Vector2(260f, 6f), new Vector2(0f, 250f), style.Track);
        charge = chargeBar.gameObject;
        chargeFill = Fill(chargeBar, Gold);
        Label(chargeBar, "Hint", new Vector2(0.5f, 1f), new Vector2(400f, 30f), new Vector2(0f, 18f), 18f, Body).text = "Let go to cast";

        strike = Rect("Strike", root, new Vector2(0f, 0f), new Vector2(220f, 110f), Vector2.zero);
        TMP_Text bang = Label(strike, "Bang", new Vector2(0.5f, 1f), new Vector2(120f, 70f), new Vector2(0f, -30f), 64f, Gold);
        bang.text = "!";
        bang.fontStyle = FontStyles.Bold;
        Label(strike, "Strike", new Vector2(0.5f, 0f), new Vector2(220f, 30f), new Vector2(0f, 18f), 20f, Body).text = "Strike! (left click)";

        // The fight, right of centre: the line's tension and the fish's strength, side by side.
        RectTransform panel = Rect("Fight", root, new Vector2(0.5f, 0.5f), new Vector2(120f, MeterHeight), new Vector2(330f, 40f));
        fightPanel = panel.gameObject;
        RectTransform meter = Panel("Tension", panel, new Vector2(0f, 0.5f), new Vector2(10f, MeterHeight), new Vector2(20f, 0f), style.Track);
        RectTransform safe = Panel("Safe", meter, new Vector2(0.5f, 0f), new Vector2(10f, (SafeHigh - SafeLow) * MeterHeight), new Vector2(0f, SafeLow * MeterHeight), WithAlpha(style.Good, 0.55f));
        safe.pivot = new Vector2(0.5f, 0f);
        RectTransform danger = Panel("Danger", meter, new Vector2(0.5f, 0f), new Vector2(10f, (1f - 0.92f) * MeterHeight), new Vector2(0f, 0.92f * MeterHeight), style.Danger);
        danger.pivot = new Vector2(0.5f, 0f);
        dangerImage = danger.GetComponent<Image>();
        needle = Panel("Needle", meter, new Vector2(0.5f, 0f), new Vector2(26f, 4f), Vector2.zero, Gold);
        needleImage = needle.GetComponent<Image>();
        Label(meter, "Label", new Vector2(0.5f, 0f), new Vector2(80f, 24f), new Vector2(0f, -18f), 16f, style.Muted).text = "Line";

        RectTransform strength = Panel("Strength", panel, new Vector2(0f, 0.5f), new Vector2(6f, MeterHeight - 50f), new Vector2(70f, -25f), style.Track);
        staminaFill = Panel("Fill", strength, new Vector2(0.5f, 0f), new Vector2(6f, 0f), Vector2.zero, Gold);
        staminaFill.pivot = new Vector2(0.5f, 0f);
        RectTransform icon = Rect("Fish", panel, new Vector2(0f, 1f), new Vector2(40f, 40f), new Vector2(70f, -20f));
        fishIcon = icon.gameObject.AddComponent<Image>();
        fishIcon.raycastTarget = false;
        fishIcon.preserveAspect = true;
        Label(strength, "Label", new Vector2(0.5f, 0f), new Vector2(80f, 24f), new Vector2(0f, -18f), 16f, style.Muted).text = "Fish";

        lineText = Label(panel, "Line Out", new Vector2(0f, 0f), new Vector2(120f, 26f), new Vector2(45f, -48f), 18f, Body);
        hintText = Label(root, "Hint", new Vector2(0.5f, 0f), new Vector2(700f, 34f), new Vector2(0f, 290f), 22f, Body);

        // On the water: which way to lean the rod, and the warning before a run.
        lean = Rect("Lean", root, Vector2.zero, new Vector2(46f, 46f), Vector2.zero);
        leanImage = lean.gameObject.AddComponent<Image>();
        leanImage.sprite = Arrow();
        leanImage.color = Gold;
        leanImage.raycastTarget = false;
        warn = Rect("Warn", root, Vector2.zero, new Vector2(80f, 80f), Vector2.zero);
        TMP_Text mark = Label(warn, "Mark", new Vector2(0.5f, 0.5f), new Vector2(80f, 80f), Vector2.zero, 52f, Gold);
        mark.text = "!";
        mark.fontStyle = FontStyles.Bold;

        RectTransform plate = Panel("Struggle", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 150f), new Vector2(0f, -290f), WithAlpha(style.Panel, 0.85f));
        struggle = plate.gameObject;
        struggleText = Label(plate, "Space", new Vector2(0.5f, 1f), new Vector2(540f, 70f), new Vector2(0f, -45f), 54f, new Color(0.75f, 1f, 0.9f));
        struggleText.fontStyle = FontStyles.Bold;
        RectTransform bar = Panel("Bar", plate, new Vector2(0.5f, 0f), new Vector2(460f, 16f), new Vector2(0f, 34f), style.Track);
        struggleFill = Fill(bar, new Color(0.75f, 1f, 0.9f));
    }

    private void LateUpdate()
    {
        PlayerFishing fishing = PlayerFishing.Current;
        PlayerFishing.Phase state = fishing != null ? fishing.State : PlayerFishing.Phase.None;
        Camera view = Camera.main;

        charge.SetActive(state == PlayerFishing.Phase.Charging);
        if (state == PlayerFishing.Phase.Charging)
        {
            chargeFill.anchorMax = new Vector2(fishing.Charge, 1f);
        }

        bool biting = state == PlayerFishing.Phase.Bite && fishing.Bobber != null && view != null;
        strike.gameObject.SetActive(biting);
        if (biting)
        {
            strike.anchoredPosition = OnScreen(view, fishing.Bobber.position + Vector3.up * 0.6f) + new Vector2(0f, 50f);
            strike.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 4f, 0.25f));
        }

        FishFight fight = state == PlayerFishing.Phase.Reeling ? fishing.Fight : null;
        fightPanel.SetActive(fight != null);
        hintText.gameObject.SetActive(fight != null);
        if (fight != null)
        {
            ShowFight(fishing, fight, view);
        }
        else
        {
            lean.gameObject.SetActive(false);
            warn.gameObject.SetActive(false);
        }

        NokkenEncounter nokken = NokkenEncounter.Current;
        struggle.SetActive(nokken != null);
        if (nokken != null)
        {
            int tenths = Mathf.RoundToInt(nokken.SecondsLeft * 10f);
            if (tenths != writtenTenths)
            {
                writtenTenths = tenths;
                struggleText.text = $"SPACE!  <size=50%><alpha=#AA>{nokken.SecondsLeft:0.0}s";
            }

            struggleText.transform.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 6f, 0.15f));
            struggleFill.anchorMax = new Vector2(nokken.Struggle, 1f);
        }
    }

    private void ShowFight(PlayerFishing fishing, FishFight fight, Camera view)
    {
        UIStyle style = UIStyle.Current;
        float tension = Mathf.Clamp01(fight.Tension);
        needle.anchoredPosition = new Vector2(0f, tension * MeterHeight);
        bool straining = fight.Tension >= 0.95f || fight.Strain > 0.05f;
        float flash = straining ? Mathf.PingPong(Time.time * 6f, 1f) : 0f;
        needleImage.color = tension < SafeLow ? style.Muted : tension > SafeHigh ? Color.Lerp(style.Warning, style.Danger, flash) : Gold;
        dangerImage.color = WithAlpha(style.Danger, 0.45f + 0.55f * flash);

        staminaFill.sizeDelta = new Vector2(6f, fight.Strength * (MeterHeight - 50f));
        fishIcon.sprite = fishing.Hooked != null && fishing.Hooked.item != null ? fishing.Hooked.item.Icon : null;
        fishIcon.color = fishIcon.sprite != null ? Color.Lerp(new Color(1f, 1f, 1f, 0.45f), Color.white, fight.Strength) : Gold;

        int metres = Mathf.RoundToInt(fight.Distance);
        if (metres != writtenMetres)
        {
            writtenMetres = metres;
            lineText.text = $"{metres} m";
        }

        string hint = HintFor(fight, fishing.ReelHeld);
        if (hint != writtenHint)
        {
            writtenHint = hint;
            hintText.text = hint;
        }

        hintText.color = straining ? style.Danger : fight.Spent ? style.Good : Body;

        Vector3 counter = fishing.CounterDirection;
        bool leaning = counter.sqrMagnitude > 0.01f && view != null;
        lean.gameObject.SetActive(leaning);
        if (leaning)
        {
            // The arrow sits on the water beside the float, on the side the rod should go, pointing that way.
            Vector2 at = OnScreen(view, fishing.FishPosition);
            Vector2 toward = OnScreen(view, fishing.FishPosition + counter * 2f) - at;
            toward = toward.sqrMagnitude > 1f ? toward.normalized : Vector2.right;
            float pulse = 1f + Mathf.PingPong(Time.time * 3f, 0.2f);
            lean.anchoredPosition = at + toward * (70f + 12f * pulse);
            lean.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg);
            lean.localScale = Vector3.one * pulse;
            // Brighter once the rod is held the right way.
            leanImage.color = fight.Leverage > 0.5f ? style.Good : Gold;
        }

        bool warning = fight.Current == FishFight.Mood.Warning && view != null;
        warn.gameObject.SetActive(warning);
        if (warning)
        {
            warn.anchoredPosition = OnScreen(view, fishing.FishPosition + Vector3.up * 0.5f) + new Vector2(0f, 40f);
            warn.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 5f, 0.3f));
        }
    }

    // A world point in this canvas' units (its anchors at the bottom left).
    private Vector2 OnScreen(Camera view, Vector3 world)
    {
        Vector3 screen = view.WorldToScreenPoint(world);
        float scale = root.localScale.x > 0f ? root.localScale.x : 1f;
        return new Vector2(screen.x / scale, screen.y / scale);
    }

    private static Sprite arrow;

    // A plain solid triangle pointing right, made once.
    private static Sprite Arrow()
    {
        if (arrow != null)
        {
            return arrow;
        }

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Fishing Arrow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                float edge = (1f - u) - v;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge * size * 0.5f)));
            }
        }

        texture.Apply();
        arrow = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return arrow;
    }

    private static Color WithAlpha(Color colour, float alpha)
    {
        colour.a = alpha;
        return colour;
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

    private static RectTransform Panel(string name, Transform parent, Vector2 anchorAt, Vector2 size, Vector2 position, Color colour)
    {
        RectTransform rect = Rect(name, parent, anchorAt, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform Fill(RectTransform bar, Color colour)
    {
        RectTransform fill = Panel("Fill", bar, Vector2.zero, Vector2.zero, Vector2.zero, colour);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        return fill;
    }

    private static TMP_Text Label(Transform parent, string name, Vector2 anchorAt, Vector2 size, Vector2 position, float fontSize, Color colour)
    {
        var text = Rect(name, parent, anchorAt, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        if (UIStyle.Current.BodyFont != null)
        {
            text.font = UIStyle.Current.BodyFont;
        }

        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
