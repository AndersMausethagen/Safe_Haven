using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's health and stamina bars, and the smaller hunger and thirst bars (GDD §11, §18.2). Each bar has a fill that tracks the value at once and a pale trail behind it
/// that lingers and then drains, so a hit or a spend reads at a glance. Refused attacks, skills and dodges flash the
/// stamina bar and post a warning through the existing message feed.
/// <para>
/// Where it belongs: the PlayerVitalsHUD prefab, placed in every gameplay scene. It finds the player through
/// Player.Current / Player.Spawned, so it needs no scene references.
/// </para>
/// </summary>
public sealed class PlayerVitalsView : MonoBehaviour
{
    private const float StaminaMessageInterval = 1f;

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

    [Tooltip("Optional: hidden while there is no player.")]
    [SerializeField] private CanvasGroup group;

    private Player bound;
    private Color frameColor;
    private float flashUntil = -1f;
    private float nextStaminaMessageAt = -1f;

    private void Awake()
    {
        if (staminaFrame != null)
        {
            frameColor = staminaFrame.color;
        }
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

        bound = null;
        nextStaminaMessageAt = -1f;
    }

    private void OnHealthChanged(float current, float max) => SetTarget(health, current, max, snap: false);

    private void OnStaminaChanged(float current, float max) => SetTarget(stamina, current, max, snap: false);

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
        if (bound != null && bound.Needs != null)
        {
            SetTarget(hunger, bound.Needs.Hunger.Current, bound.Needs.Hunger.Max, snap: true);
            SetTarget(thirst, bound.Needs.Thirst.Current, bound.Needs.Thirst.Max, snap: true);
        }

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
