using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's health and stamina bars, the thin Focus bar (earned by fighting well, spent on skills), and the smaller
/// hunger and thirst bars (GDD §11, §18.2). Each bar has a fill that tracks the value at once and a pale trail behind it
/// that lingers and then drains, so a hit or a spend reads at a glance. Refused attacks, skills and dodges flash the
/// stamina bar and post a warning through the existing message feed. While the dodge cools down the stamina bar glows
/// red around its edge, and the glow going out says the dodge is ready. Quiet by default (GDD §18.2): with health and
/// stamina full, needs in hand and nothing changing, the bars dim until something happens (Options: Always Show Vitals).
/// <para>
/// Where it belongs: the PlayerVitalsHUD prefab, placed in every gameplay scene. It finds the player through
/// Player.Current / Player.Spawned, so it needs no scene references.
/// </para>
/// </summary>
public sealed class PlayerVitalsView : MonoBehaviour
{
    private const float StaminaMessageInterval = 1f;
    // How long everything has to stay full before the bars dim, how far, and how quickly they move.
    private const float QuietAfterSeconds = 5f;
    private const float QuietAlpha = 0.4f;
    private const float QuietFadePerSecond = 1.5f;
    private const float FullShare = 0.995f;
    // Hunger and thirst below this keep the bars lit: they are worth seeing even with nothing else going on.
    private const float NeedsInHandShare = 0.3f;

    [Serializable]
    private sealed class Bar
    {
        [Tooltip("Stretched from the left; its right anchor follows the value.")]
        public RectTransform fill;
        [Tooltip("Optional: lags behind the fill when the value drops.")]
        public RectTransform trail;
        [Tooltip("Optional: shows \"current / max\".")]
        public TMP_Text value;

        [NonSerialized] public float target = 1f;
        [NonSerialized] public float shown = 1f;
        [NonSerialized] public float holdUntil;
    }

    [SerializeField] private Bar health = new Bar();
    [SerializeField] private Bar stamina = new Bar();
    [Tooltip("Optional: Focus, from PlayerFocus.")]
    [SerializeField] private Bar focus = new Bar();
    [Tooltip("Optional: hunger, from PlayerNeeds.")]
    [SerializeField] private Bar hunger = new Bar();
    [Tooltip("Optional: thirst, from PlayerNeeds.")]
    [SerializeField] private Bar thirst = new Bar();

    [Header("Trail")]
    [Tooltip("Seconds the trail waits before draining.")]
    [SerializeField, Min(0f)] private float trailDelay = 0.45f;
    [Tooltip("Fraction of the bar the trail drains per second.")]
    [SerializeField, Min(0.01f)] private float trailSpeed = 0.9f;

    [Header("Exhausted flash")]
    [Tooltip("Tinted when an action is refused for lack of stamina.")]
    [SerializeField] private Graphic staminaFrame;
    [SerializeField] private Color exhaustedColor = new Color(0.85f, 0.32f, 0.18f, 1f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.4f;

    [Header("Dodge cooldown glow")]
    [Tooltip("The stamina bar glows this colour around its edge while the dodge is cooling down.")]
    [SerializeField] private Color dodgeCooldownGlow = new Color(0.95f, 0.16f, 0.08f, 0.95f);
    [Tooltip("How far the glow reaches out from the bar, in UI units.")]
    [SerializeField, Min(1f)] private float glowSpread = 9f;

    [Tooltip("Optional: hidden while there is no player.")]
    [SerializeField] private CanvasGroup group;

    private const int GlowTexture = 64;
    private const int GlowBorder = 24;
    // The glow fades out over the dodge's last moment of cooldown, so "ready" reads as the glow going out.
    private const float GlowFadeSeconds = 0.2f;
    private static Sprite glowSprite;

    private Image dodgeGlow;
    private PlayerDodge boundDodge;
    private Player bound;
    private Color frameColor;
    private float flashUntil = -1f;
    private float nextStaminaMessageAt = -1f;
    private PlayerFocus boundFocus;
    private float lastActiveAt;

    private void Awake()
    {
        if (staminaFrame != null)
        {
            frameColor = staminaFrame.color;
        }

        BuildDodgeGlow();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            OverheadVitalsView.Create(canvas, FillColour(health, new Color(0.78f, 0.22f, 0.2f, 1f)), FillColour(stamina, new Color(0.85f, 0.7f, 0.25f, 1f)));
        }
    }

    private static Color FillColour(Bar bar, Color fallback) =>
        bar.fill != null && bar.fill.TryGetComponent(out Graphic graphic) ? graphic.color : fallback;

    // A soft red ring laid just behind the stamina bar's frame (or its bar, without a frame), reaching out past its edge.
    private void BuildDodgeGlow()
    {
        var around = (staminaFrame != null ? staminaFrame.transform : stamina.fill != null ? stamina.fill.parent : null) as RectTransform;
        if (around == null)
        {
            return;
        }

        var glow = new GameObject("Dodge Cooldown Glow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        glow.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)glow.transform;
        rect.SetParent(around.parent, false);
        rect.SetSiblingIndex(around.GetSiblingIndex());
        rect.anchorMin = around.anchorMin;
        rect.anchorMax = around.anchorMax;
        rect.pivot = around.pivot;
        rect.anchoredPosition = around.anchoredPosition;
        rect.sizeDelta = around.sizeDelta + Vector2.one * (glowSpread * 2f);
        dodgeGlow = glow.GetComponent<Image>();
        dodgeGlow.sprite = GlowSprite();
        dodgeGlow.type = Image.Type.Sliced;
        dodgeGlow.fillCenter = false;
        dodgeGlow.pixelsPerUnitMultiplier = GlowBorder / glowSpread;
        dodgeGlow.raycastTarget = false;
        dodgeGlow.color = Color.clear;
    }

    // White, opaque at the inner edge and fading out to nothing at the outer one; sliced so only the ring is drawn.
    private static Sprite GlowSprite()
    {
        if (glowSprite != null)
        {
            return glowSprite;
        }

        var texture = new Texture2D(GlowTexture, GlowTexture, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[GlowTexture * GlowTexture];
        float inner = GlowTexture - GlowBorder;
        for (int y = 0; y < GlowTexture; y++)
        {
            for (int x = 0; x < GlowTexture; x++)
            {
                float dx = Mathf.Max(GlowBorder - (x + 0.5f), 0f, (x + 0.5f) - inner);
                float dy = Mathf.Max(GlowBorder - (y + 0.5f), 0f, (y + 0.5f) - inner);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / GlowBorder);
                float a = (1f - d) * (1f - d);
                pixels[y * GlowTexture + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        glowSprite = Sprite.Create(texture, new Rect(0, 0, GlowTexture, GlowTexture), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, Vector4.one * GlowBorder);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        return glowSprite;
    }

    private void UpdateDodgeGlow()
    {
        if (dodgeGlow == null)
        {
            return;
        }

        float remaining = boundDodge != null && boundDodge.CooldownFraction > 0f ? boundDodge.CooldownRemaining : 0f;
        float strength = Mathf.Clamp01(remaining / GlowFadeSeconds) * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f));
        Color color = dodgeCooldownGlow;
        color.a *= strength;
        dodgeGlow.color = color;
    }

    private void OnEnable()
    {
        // Binding waits for Player.Spawned or the first Update: during scene load the player's stats may not be built yet.
        Player.Spawned += Bind;
        SetVisible(false);
    }

    private void OnDisable()
    {
        Player.Spawned -= Bind;
        Unbind();
    }

    private void Bind(Player player)
    {
        if (player == bound)
        {
            return;
        }

        Unbind();
        bound = player;
        if (bound == null)
        {
            return;
        }

        bound.Health.HealthChanged += OnHealthChanged;
        if (bound.Stamina != null)
        {
            bound.Stamina.StaminaChanged += OnStaminaChanged;
            bound.Stamina.SpendRefused += OnSpendRefused;
        }

        SetTarget(health, bound.Health.CurrentHealth, bound.Health.MaxHealth, snap: true);
        if (bound.Stamina != null)
        {
            SetTarget(stamina, bound.Stamina.Current, bound.Stamina.Max, snap: true);
        }

        boundDodge = bound.GetComponent<PlayerDodge>();
        boundFocus = bound.GetComponent<PlayerFocus>();
        if (boundFocus != null)
        {
            boundFocus.FocusChanged += OnFocusChanged;
            boundFocus.SpendRefused += OnFocusRefused;
            SetTarget(focus, boundFocus.Current, boundFocus.Max, snap: true);
        }

        SetVisible(true);
    }

    private void Unbind()
    {
        // The player may already be destroyed (scene change); its events went with it.
        if (!ReferenceEquals(bound, null) && bound != null)
        {
            bound.Health.HealthChanged -= OnHealthChanged;
            if (bound.Stamina != null)
            {
                bound.Stamina.StaminaChanged -= OnStaminaChanged;
                bound.Stamina.SpendRefused -= OnSpendRefused;
            }
        }

        if (!ReferenceEquals(boundFocus, null) && boundFocus != null)
        {
            boundFocus.FocusChanged -= OnFocusChanged;
            boundFocus.SpendRefused -= OnFocusRefused;
        }

        boundFocus = null;
        boundDodge = null;
        bound = null;
        nextStaminaMessageAt = -1f;
    }

    private void OnHealthChanged(float current, float max)
    {
        lastActiveAt = Time.unscaledTime;
        SetTarget(health, current, max, snap: false);
    }

    private void OnStaminaChanged(float current, float max)
    {
        lastActiveAt = Time.unscaledTime;
        SetTarget(stamina, current, max, snap: false);
    }

    private void OnFocusChanged(float current, float max) => SetTarget(focus, current, max, snap: false);

    private void OnFocusRefused()
    {
        float now = Time.unscaledTime;
        if (now < nextStaminaMessageAt)
        {
            return;
        }

        nextStaminaMessageAt = now + StaminaMessageInterval;
        GameMessages.Post("Not enough Focus. Fight well to earn it: land hits, parry, dodge at the last moment.", MessageKind.Warning);
    }

    private void OnSpendRefused()
    {
        float now = Time.unscaledTime;
        flashUntil = now + flashDuration;
        if (now < nextStaminaMessageAt)
        {
            return;
        }

        nextStaminaMessageAt = now + StaminaMessageInterval;
        GameMessages.Post("Not enough stamina.", MessageKind.Warning);
    }

    private void SetTarget(Bar bar, float current, float max, bool snap)
    {
        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (snap || fraction >= bar.shown)
        {
            bar.shown = fraction;
        }
        else if (fraction < bar.target)
        {
            bar.holdUntil = Time.unscaledTime + trailDelay;
        }

        bar.target = fraction;
        SetAnchor(bar.fill, fraction);
        SetAnchor(bar.trail, bar.shown);

        if (bar.value != null)
        {
            bar.value.SetText("{0:0} / {1:0}", Mathf.Ceil(current), max);
        }
    }

    private void Update()
    {
        if (bound == null)
        {
            if (Player.Current != null)
            {
                Bind(Player.Current);
            }
            else if (group != null && group.alpha > 0f)
            {
                SetVisible(false);
            }
        }

        DrainTrail(health);
        DrainTrail(stamina);
        DrainTrail(focus);
        if (bound != null && bound.Needs != null)
        {
            SetTarget(hunger, bound.Needs.Hunger.Current, bound.Needs.Hunger.Max, snap: true);
            SetTarget(thirst, bound.Needs.Thirst.Current, bound.Needs.Thirst.Max, snap: true);
        }

        UpdateQuiet();
        UpdateDodgeGlow();
        if (staminaFrame != null && flashUntil >= 0f)
        {
            float t = Mathf.Clamp01((flashUntil - Time.unscaledTime) / flashDuration);
            staminaFrame.color = Color.Lerp(frameColor, exhaustedColor, t);
            if (t <= 0f)
            {
                flashUntil = -1f;
            }
        }
    }

    private void UpdateQuiet()
    {
        if (group != null && bound != null)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, IsQuiet ? QuietAlpha : 1f, QuietFadePerSecond * Time.unscaledDeltaTime);
        }
    }

    // A refused action, a cooling dodge or a fight all count as something going on.
    private bool IsQuiet => !GameOptions.AlwaysShowVitals.On
        && Time.unscaledTime >= lastActiveAt + QuietAfterSeconds && flashUntil < 0f
        && health.target >= FullShare && stamina.target >= FullShare
        && (bound.Needs == null || (hunger.target >= NeedsInHandShare && thirst.target >= NeedsInHandShare))
        && (boundDodge == null || boundDodge.CooldownFraction <= 0f)
        && !MonsterAI.AnyHuntingPlayerSide();

    private void DrainTrail(Bar bar)
    {
        if (bar.shown <= bar.target || Time.unscaledTime < bar.holdUntil)
        {
            return;
        }

        bar.shown = Mathf.MoveTowards(bar.shown, bar.target, trailSpeed * Time.unscaledDeltaTime);
        SetAnchor(bar.trail, bar.shown);
    }

    private static void SetAnchor(RectTransform rect, float fraction)
    {
        if (rect != null)
        {
            rect.anchorMax = new Vector2(fraction, rect.anchorMax.y);
        }
    }

    private void SetVisible(bool visible)
    {
        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
        }
    }
}
