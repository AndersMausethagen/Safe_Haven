using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What fishing shows on screen: the swing-back bar while charging a cast, a "!" over the float with "Strike!" when a
/// fish bites, the reeling bar (the fish's icon darting in a column, the green catch zone the player lifts, the catch
/// filling beside it) and, when the Nøkken has the player, a pulsing "SPACE!" with how close they are to breaking free.
/// Built in code by PlayerFishing; it only reads PlayerFishing and NokkenEncounter.
/// </summary>
public sealed class FishingHud : MonoBehaviour
{
    private const float BarHeight = 340f;
    private static readonly Color Gold = new Color(0.91f, 0.77f, 0.42f, 1f);
    private static readonly Color Body = new Color(0.93f, 0.89f, 0.8f, 1f);

    private RectTransform root;
    private GameObject charge;
    private RectTransform chargeFill;
    private RectTransform strike;
    private GameObject reel;
    private RectTransform zone;
    private RectTransform fish;
    private Image fishIcon;
    private RectTransform catchFill;
    private GameObject struggle;
    private TMP_Text struggleText;
    private RectTransform struggleFill;

    public static FishingHud Current { get; private set; }

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
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
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

    private void Build()
    {
        // Swing-back bar above the action bar.
        RectTransform chargeBar = Panel("Charge", root, new Vector2(0.5f, 0f), new Vector2(260f, 12f), new Vector2(0f, 250f), new Color(0f, 0f, 0f, 0.6f));
        charge = chargeBar.gameObject;
        chargeFill = Fill(chargeBar, Gold);
        Label(chargeBar, "Hint", new Vector2(0.5f, 1f), new Vector2(400f, 30f), new Vector2(0f, 18f), 18f, Body).text = "Let go to cast";

        strike = Rect("Strike", root, new Vector2(0f, 0f), new Vector2(220f, 110f), Vector2.zero);
        TMP_Text bang = Label(strike, "Bang", new Vector2(0.5f, 1f), new Vector2(120f, 70f), new Vector2(0f, -30f), 64f, Gold);
        bang.text = "!";
        bang.fontStyle = FontStyles.Bold;
        Label(strike, "Strike", new Vector2(0.5f, 0f), new Vector2(220f, 30f), new Vector2(0f, 18f), 20f, Body).text = "Strike! (left click)";

        // The reeling bar, right of centre.
        RectTransform frame = Panel("Reel", root, new Vector2(0.5f, 0.5f), new Vector2(64f, BarHeight), new Vector2(300f, 40f), new Color(0.06f, 0.08f, 0.1f, 0.8f));
        reel = frame.gameObject;
        zone = Panel("Zone", frame, new Vector2(0.5f, 0f), new Vector2(56f, 80f), Vector2.zero, new Color(0.35f, 0.85f, 0.4f, 0.55f));
        zone.pivot = new Vector2(0.5f, 0f);
        fish = Rect("Fish", frame, new Vector2(0.5f, 0f), new Vector2(44f, 44f), Vector2.zero);
        fishIcon = fish.gameObject.AddComponent<Image>();
        fishIcon.raycastTarget = false;
        fishIcon.preserveAspect = true;
        RectTransform meter = Panel("Catch", frame, new Vector2(1f, 0f), new Vector2(12f, BarHeight), new Vector2(16f, BarHeight * 0.5f), new Color(0f, 0f, 0f, 0.6f));
        catchFill = Panel("Fill", meter, new Vector2(0.5f, 0f), new Vector2(12f, 0f), Vector2.zero, Gold);
        catchFill.pivot = new Vector2(0.5f, 0f);
        Label(frame, "Hint", new Vector2(0.5f, 0f), new Vector2(260f, 30f), new Vector2(0f, -24f), 18f, Body).text = "Hold left click to reel";

        RectTransform plate = Panel("Struggle", root, new Vector2(0.5f, 0.5f), new Vector2(560f, 150f), new Vector2(0f, -290f), new Color(0.03f, 0.05f, 0.06f, 0.75f));
        struggle = plate.gameObject;
        struggleText = Label(plate, "Space", new Vector2(0.5f, 1f), new Vector2(540f, 70f), new Vector2(0f, -45f), 54f, new Color(0.75f, 1f, 0.9f));
        struggleText.fontStyle = FontStyles.Bold;
        RectTransform bar = Panel("Bar", plate, new Vector2(0.5f, 0f), new Vector2(460f, 16f), new Vector2(0f, 34f), new Color(0f, 0f, 0f, 0.6f));
        struggleFill = Fill(bar, new Color(0.75f, 1f, 0.9f));
    }

    private void LateUpdate()
    {
        PlayerFishing fishing = PlayerFishing.Current;
        PlayerFishing.Phase state = fishing != null ? fishing.State : PlayerFishing.Phase.None;

        charge.SetActive(state == PlayerFishing.Phase.Charging);
        if (state == PlayerFishing.Phase.Charging)
        {
            chargeFill.anchorMax = new Vector2(fishing.Charge, 1f);
        }

        bool biting = state == PlayerFishing.Phase.Bite && fishing.Bobber != null && Camera.main != null;
        strike.gameObject.SetActive(biting);
        if (biting)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(fishing.Bobber.position + Vector3.up * 0.6f);
            float scale = root.localScale.x > 0f ? root.localScale.x : 1f;
            strike.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale + 50f);
            strike.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 4f, 0.25f));
        }

        ReelSession session = fishing != null ? fishing.Reel : null;
        reel.SetActive(state == PlayerFishing.Phase.Reeling && session != null);
        if (reel.activeSelf)
        {
            zone.sizeDelta = new Vector2(56f, session.ZoneSize * BarHeight);
            zone.anchoredPosition = new Vector2(0f, session.Zone * BarHeight);
            zone.GetComponent<Image>().color = session.InZone ? new Color(0.35f, 0.85f, 0.4f, 0.65f) : new Color(0.85f, 0.45f, 0.3f, 0.45f);
            fish.anchoredPosition = new Vector2(0f, session.Fish * BarHeight);
            fishIcon.sprite = fishing.Hooked != null && fishing.Hooked.item != null ? fishing.Hooked.item.Icon : null;
            fishIcon.color = fishIcon.sprite != null ? Color.white : Gold;
            catchFill.sizeDelta = new Vector2(12f, session.Catch * BarHeight);
        }

        NokkenEncounter nokken = NokkenEncounter.Current;
        struggle.SetActive(nokken != null);
        if (nokken != null)
        {
            struggleText.text = $"SPACE!  <size=50%><alpha=#AA>{nokken.SecondsLeft:0.0}s";
            struggleText.transform.localScale = Vector3.one * (1f + Mathf.PingPong(Time.time * 6f, 0.15f));
            struggleFill.anchorMax = new Vector2(nokken.Struggle, 1f);
        }
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
        text.fontSize = fontSize;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
}
