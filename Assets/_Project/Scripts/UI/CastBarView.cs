using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cast bar (the user, 2026-11-25: "all channeled spells should show a bar of how long it's progressed and how long it can be
/// channeled"): while a channel runs (PlayerSkills.Channeling), a thin bar above the Focus circles fills in the skill's school colour from
/// the cast to its last strike, with the skill's icon and name on the left and the seconds run against the most it can run on the right.
/// Let go early, it fades out where it stopped; run to the end, it fades out full.
/// <para>
/// Where it belongs: on the SkillHUD prefab's Skill Bar, beside FocusCirclesView, so it scales and hides with the bar. It builds its own
/// parts and finds the player through Player.Current / Player.Spawned. Shown only while a channel runs, whatever the HUD density.
/// </para>
/// </summary>
public sealed class CastBarView : MonoBehaviour
{
    [SerializeField, Min(40f)] private float width = 260f;
    [SerializeField, Min(2f)] private float barHeight = 8f;
    [Tooltip("Height of the bar's bottom edge above the top of the skill bar: over the Focus circles.")]
    [SerializeField] private float above = 46f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;

    private const float IconSize = 22f;
    private const float FontSize = 18f;

    private PlayerSkills skills;
    private RectTransform root;
    private CanvasGroup group;
    private RectTransform fill;
    private Image fillImage;
    private Image icon;
    private TMP_Text title;
    private TMP_Text time;
    private SkillDefinition shown;
    private float shownElapsed = -1f;
    private float shownLength = -1f;
    private float endedAt = float.NegativeInfinity;

    private void OnEnable()
    {
        Player.Spawned += Bind;
        if (Player.Current != null)
        {
            Bind(Player.Current);
        }
    }

    private void OnDisable() => Player.Spawned -= Bind;

    private void Bind(Player player)
    {
        skills = player != null ? player.Skills : null;
        shown = null;
        Build();
        group.alpha = 0f;
    }

    private void Build()
    {
        if (root != null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        root = CodeUI.Box("Cast Bar", transform, new Vector2(0.5f, 1f), new Vector2(width, barHeight + IconSize + 4f), new Vector2(0f, above));
        root.pivot = new Vector2(0.5f, 0f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        RectTransform track = CodeUI.Box("Track", root, new Vector2(0f, 0f), new Vector2(0f, barHeight), Vector2.zero);
        track.anchorMax = new Vector2(1f, 0f);
        track.pivot = new Vector2(0.5f, 0f);
        track.sizeDelta = new Vector2(0f, barHeight);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = style.Track;
        trackImage.raycastTarget = false;

        fill = CodeUI.Box("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.raycastTarget = false;

        RectTransform iconRect = CodeUI.Box("Icon", root, new Vector2(0f, 1f), new Vector2(IconSize, IconSize), Vector2.zero);
        iconRect.pivot = new Vector2(0f, 1f);
        icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        title = CodeUI.Label("Name", root, IconSize + 6f, 0f, width * 0.65f, IconSize, FontSize, style.Text, TextAlignmentOptions.BottomLeft);
        style.OnWorld(title);
        time = CodeUI.Label("Time", root, width * 0.6f, 0f, width * 0.4f, IconSize, FontSize, style.Text, TextAlignmentOptions.BottomRight);
        style.OnWorld(time);
    }

    private void Update()
    {
        if (root == null)
        {
            return;
        }

        SkillDefinition channel = skills != null ? skills.Channeling : null;
        if (channel != null)
        {
            if (channel != shown)
            {
                shown = channel;
                icon.sprite = channel.Icon;
                icon.enabled = channel.Icon != null;
                title.text = channel.DisplayName;
                fillImage.color = SkillSchools.ColorOf(channel.School);
            }

            Show(skills.ChannelElapsed, skills.ChannelLength);
            group.alpha = 1f;
            endedAt = Time.unscaledTime;
            return;
        }

        // Ended (let go, run out, broken off): it fades out where it stopped.
        group.alpha = Mathf.Clamp01(1f - (Time.unscaledTime - endedAt) / fadeSeconds);
        if (group.alpha <= 0f)
        {
            shown = null;
        }
    }

    private void Show(float elapsed, float length)
    {
        float share = length > 0f ? Mathf.Clamp01(elapsed / length) : 1f;
        KitBar.Drive(fill, share, steady: true); // as every bar moves (the kit bar's shader); steady, as it moves every frame
        // To a tenth of a second, so the label is rebuilt ten times a second at most.
        elapsed = Mathf.Min(Mathf.Floor(elapsed * 10f) / 10f, length);
        if (!Mathf.Approximately(elapsed, shownElapsed) || !Mathf.Approximately(length, shownLength))
        {
            shownElapsed = elapsed;
            shownLength = length;
            time.SetText("{0:1} / {1:1} s", elapsed, length);
        }
    }
}
