using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Focus circles above the middle of the skill bar (the user, 2026-11-24): one for each circle that can be kept (three). A full
/// circle glows and breathes softly and stays lit until an ability, a spell or a weapon art spends it; the one still filling shows a
/// faint sweep of how far it has come. A circle that fills flares once; one that is spent fades out with a short flash; a refused
/// spend pulses the empty rims red. The thin Focus bar on the vitals HUD shows the same filling circle.
/// <para>
/// Where it belongs: on the SkillHUD prefab's Skill Bar, so it scales with the Skill Bar Size option and hides with the bar. It
/// builds its own parts and finds the player through Player.Current / Player.Spawned.
/// </para>
/// </summary>
public sealed class FocusCirclesView : MonoBehaviour
{
    [SerializeField, Min(8f)] private float size = 17f;
    [SerializeField, Min(0f)] private float spacing = 7f;
    [Tooltip("Height of the circles' centres above the top of the bar.")]
    [SerializeField] private float above = 18f;
    [Tooltip("The Focus colour: the vitals HUD's Focus bar.")]
    [SerializeField] private Color lit = new Color(0.85f, 0.655f, 0.353f, 1f);
    [SerializeField] private Color rim = new Color(0.08f, 0.07f, 0.06f, 0.75f);
    [SerializeField] private Color refused = new Color(0.85f, 0.18f, 0.12f, 0.9f);

    private sealed class Circle
    {
        public RectTransform root;
        public Image rim;
        public Image filling;
        public Image full;
        public Image glow;
        public float flareAt = float.NegativeInfinity;
        public float spentAt = float.NegativeInfinity;
    }

    private const float FlareSeconds = 0.6f;
    private const float SpentSeconds = 0.35f;
    private const float RefusedSeconds = 0.45f;

    private Circle[] circles = new Circle[0];
    private PlayerFocus focus;
    private int shownCircles;
    private float refusedAt = float.NegativeInfinity;

    private void OnEnable()
    {
        Player.Spawned += Bind;
        if (Player.Current != null)
        {
            Bind(Player.Current);
        }
    }

    private void OnDisable()
    {
        Player.Spawned -= Bind;
        Unbind();
    }

    private void Bind(Player player)
    {
        Unbind();
        focus = player != null ? player.GetComponent<PlayerFocus>() : null;
        if (focus == null)
        {
            return;
        }

        Build(focus.MaxCircles);
        focus.SpendRefused += OnRefused;
        shownCircles = focus.Circles;
    }

    private void Unbind()
    {
        if (!ReferenceEquals(focus, null) && focus != null)
        {
            focus.SpendRefused -= OnRefused;
        }

        focus = null;
    }

    private void OnRefused() => refusedAt = Time.unscaledTime;

    private void Build(int count)
    {
        if (circles.Length == count)
        {
            return;
        }

        foreach (Circle old in circles)
        {
            if (old.root != null)
            {
                Destroy(old.root.gameObject);
            }
        }

        circles = new Circle[count];
        float width = count * size + (count - 1) * spacing;
        for (int i = 0; i < count; i++)
        {
            var circle = new Circle();
            circle.root = CodeUI.Box($"Focus Circle {i + 1}", transform, new Vector2(0.5f, 1f), new Vector2(size, size),
                new Vector2(-width * 0.5f + size * 0.5f + i * (size + spacing), above));
            circle.glow = Part(circle.root, "Glow", UISprites.Glow, size * 2.6f);
            circle.rim = Part(circle.root, "Rim", UISprites.Disc, size);
            circle.filling = Part(circle.root, "Filling", UISprites.Disc, size - 4f);
            circle.filling.type = Image.Type.Filled;
            circle.filling.fillMethod = Image.FillMethod.Radial360;
            circle.filling.fillOrigin = (int)Image.Origin360.Top;
            circle.filling.fillClockwise = true;
            circle.full = Part(circle.root, "Full", UISprites.Disc, size - 4f);
            circles[i] = circle;
        }
    }

    private static Image Part(RectTransform parent, string name, Sprite sprite, float diameter)
    {
        RectTransform rect = CodeUI.Box(name, parent, new Vector2(0.5f, 0.5f), new Vector2(diameter, diameter), Vector2.zero);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private void Update()
    {
        if (focus == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        int full = focus.Circles;
        // A circle that just filled flares; one just spent fades out with a flash (the highest lit ones go first).
        for (int i = shownCircles; i < full && i < circles.Length; i++)
        {
            circles[i].flareAt = now;
        }

        for (int i = full; i < shownCircles && i < circles.Length; i++)
        {
            circles[i].spentAt = now;
        }

        shownCircles = full;
        float filling = focus.Filling;
        float breath = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
        float refusal = Mathf.Clamp01(1f - (now - refusedAt) / RefusedSeconds);
        for (int i = 0; i < circles.Length; i++)
        {
            Circle circle = circles[i];
            bool isFull = i < full;
            float flare = Mathf.Clamp01(1f - (now - circle.flareAt) / FlareSeconds);
            float spent = Mathf.Clamp01(1f - (now - circle.spentAt) / SpentSeconds);

            circle.rim.color = isFull ? Color.Lerp(rim, lit * 0.6f, 0.5f) : Color.Lerp(rim, refused, refusal);
            circle.full.color = isFull ? Color.Lerp(lit, Color.white, flare * 0.6f)
                : new Color(1f, 0.95f, 0.85f, spent * 0.9f);
            circle.filling.fillAmount = i == full ? filling : 0f;
            circle.filling.color = new Color(lit.r, lit.g, lit.b, 0.45f);

            float glow = isFull ? 0.35f + 0.2f * breath + 0.6f * flare : spent * 0.5f;
            circle.glow.color = new Color(lit.r, lit.g, lit.b, glow);
            circle.root.localScale = Vector3.one * (1f + 0.25f * flare * (1f - flare) * 4f * 0.5f);
        }
    }
}
