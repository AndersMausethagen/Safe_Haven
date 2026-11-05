using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small health and stamina bars above the player's head while they fight (Options › Accessibility › Vitals Over
/// Player), so a fight can be read without looking down at the HUD. "Fighting" is the combat music's rule: a monster
/// is engaging the player, or they were hurt in the last few seconds; the bars linger a moment after and fade.
/// Drawn on the HUD's screen canvas at the head's screen point, so they stay crisp and the same size at any distance.
/// Made by PlayerVitalsView, with the HUD bars' colours.
/// </summary>
public sealed class OverheadVitalsView : MonoBehaviour
{
    private const float Width = 96f;
    private const float HealthHeight = 7f;
    private const float StaminaHeight = 4f;
    private const float Gap = 2f;
    private const float HeadHeight = 1.9f; // metres above the player's feet
    private const float ScreenLift = 6f;   // canvas units above the head's screen point
    private const float HurtSeconds = 4f;
    private const float LingerSeconds = 2.5f;
    private const float FadePerSecond = 4f;
    private const float EngageCheckInterval = 0.25f;

    private RectTransform root;
    private RectTransform canvasRect;
    private CanvasGroup group;
    private RectTransform healthFill;
    private RectTransform staminaFill;
    // Companions get a health bar over their heads in the same fights (who needs help, at a glance).
    private const float CompanionWidth = 70f;
    private const float CompanionHeight = 5f;
    private RectTransform companionLayer;
    private CanvasGroup companionGroup;
    private Color healthColour;
    private readonly List<(RectTransform root, RectTransform fill, Image back)> companionBars = new List<(RectTransform, RectTransform, Image)>();
    private Damageable watched;
    private float lastHurt = float.NegativeInfinity;
    private float fightingUntil = float.NegativeInfinity;
    private float nextEngageCheck;
    private bool engaged;

    /// <summary>Builds the bars on the HUD's canvas.</summary>
    public static OverheadVitalsView Create(Canvas hud, Color health, Color stamina)
    {
        Canvas canvas = hud.rootCanvas;
        var go = new GameObject("Overhead Vitals", typeof(RectTransform), typeof(CanvasGroup), typeof(OverheadVitalsView));
        go.transform.SetParent(canvas.transform, false);
        var view = go.GetComponent<OverheadVitalsView>();
        view.Build((RectTransform)canvas.transform, health, stamina);
        return view;
    }

    private void Build(RectTransform canvas, Color health, Color stamina)
    {
        canvasRect = canvas;
        root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(Width, HealthHeight + Gap + StaminaHeight);
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        Color track = UIStyle.Current.Track;
        healthFill = Bar("Health", StaminaHeight + Gap, HealthHeight, track, health);
        staminaFill = Bar("Stamina", 0f, StaminaHeight, track, stamina);
        healthColour = health;
        var layer = new GameObject("Companion Vitals", typeof(RectTransform), typeof(CanvasGroup));
        companionLayer = (RectTransform)layer.transform;
        companionLayer.SetParent(canvas, false);
        companionLayer.anchorMin = companionLayer.anchorMax = new Vector2(0.5f, 0.5f);
        companionLayer.sizeDelta = Vector2.zero;
        companionGroup = layer.GetComponent<CanvasGroup>();
        companionGroup.alpha = 0f;
        companionGroup.interactable = false;
        companionGroup.blocksRaycasts = false;
    }

    // A track with a fill whose right edge is anchored to the value.
    private RectTransform Bar(string name, float bottom, float height, Color track, Color colour)
    {
        var back = new GameObject(name, typeof(RectTransform), typeof(Image));
        var backRect = (RectTransform)back.transform;
        backRect.SetParent(root, false);
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(0f, bottom);
        backRect.sizeDelta = new Vector2(0f, height);
        Image backImage = back.GetComponent<Image>();
        backImage.color = track;
        backImage.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRect = (RectTransform)fill.transform;
        fillRect.SetParent(backRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.one;
        fillRect.offsetMax = -Vector2.one;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = colour;
        fillImage.raycastTarget = false;
        return fillRect;
    }

    private void OnDisable() => Watch(null);

    private void OnDestroy()
    {
        if (companionLayer != null) Destroy(companionLayer.gameObject);
    }

    // Health bars over the companions with the player, shown and faded with the player's own.
    private void PlaceCompanionBars(Camera view)
    {
        companionGroup.alpha = group.alpha;
        int used = 0;
        if (group.alpha > 0f && view != null)
        {
            foreach (Companion companion in Companion.Active)
            {
                if (companion == null || companion.IsLost || !companion.TryGetComponent(out Damageable life)) continue;
                Vector3 screen = view.WorldToScreenPoint(companion.transform.position + Vector3.up * HeadHeight);
                if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local)) continue;
                if (used == companionBars.Count) companionBars.Add(CompanionBar());
                var (root, fill, back) = companionBars[used++];
                root.gameObject.SetActive(true);
                root.anchoredPosition = local + Vector2.up * ScreenLift;
                SetFill(fill, companion.IsDowned ? 0f : life.HealthFraction);
                back.color = companion.IsDowned ? new Color(0.45f, 0.08f, 0.06f, 0.9f) : UIStyle.Current.Track;
            }
        }

        for (int i = used; i < companionBars.Count; i++) companionBars[i].root.gameObject.SetActive(false);
    }

    private (RectTransform, RectTransform, Image) CompanionBar()
    {
        var holder = new GameObject("Companion", typeof(RectTransform));
        var root = (RectTransform)holder.transform;
        root.SetParent(companionLayer, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(CompanionWidth, CompanionHeight);
        RectTransform saved = this.root;
        this.root = root; // Bar() builds under root
        RectTransform fill = Bar("Health", 0f, CompanionHeight, UIStyle.Current.Track, healthColour);
        this.root = saved;
        return (root, fill, fill.parent.GetComponent<Image>());
    }

    private void LateUpdate()
    {
        Player player = Player.Current;
        Watch(player != null ? player.Health : null);
        bool wanted = GameOptions.VitalsOverPlayer.On && player != null && watched != null && watched.IsAlive && Fighting();
        float target = wanted ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, FadePerSecond * Time.unscaledDeltaTime);
        Camera view = Camera.main;
        PlaceCompanionBars(view);
        if (group.alpha <= 0f)
        {
            return;
        }

        Vector3 head = player.transform.position + Vector3.up * HeadHeight;
        Vector3 screen = view != null ? view.WorldToScreenPoint(head) : Vector3.back;
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
        {
            group.alpha = 0f;
            return;
        }

        root.anchoredPosition = local + Vector2.up * ScreenLift;
        SetFill(healthFill, watched.HealthFraction);
        SetFill(staminaFill, player.Stamina != null ? player.Stamina.Fraction : 0f);
    }

    private bool Fighting()
    {
        if (Time.unscaledTime >= nextEngageCheck)
        {
            nextEngageCheck = Time.unscaledTime + EngageCheckInterval;
            engaged = false;
            foreach (MonsterAI monster in MonsterAI.Active)
            {
                if (monster != null && monster.IsEngaging(watched))
                {
                    engaged = true;
                    break;
                }
            }
        }

        if (engaged || Time.unscaledTime - lastHurt < HurtSeconds)
        {
            fightingUntil = Time.unscaledTime + LingerSeconds;
        }

        return Time.unscaledTime < fightingUntil;
    }

    private static void SetFill(RectTransform fill, float fraction)
    {
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(fraction);
        fill.anchorMax = max;
    }

    private void Watch(Damageable next)
    {
        if (next == watched)
        {
            return;
        }

        if (watched != null)
        {
            watched.Damaged -= OnDamaged;
        }

        watched = next;
        if (watched != null)
        {
            watched.Damaged += OnDamaged;
        }
    }

    private void OnDamaged(DamageInfo info, DamageResult result) => lastHurt = Time.unscaledTime;
}
