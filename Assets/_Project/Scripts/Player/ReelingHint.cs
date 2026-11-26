using TMPro;
using UnityEngine;

/// <summary>
/// The first few times in a save (<see cref="MostShows"/>) that the enemy in focus reels (its poise worn low: a stagger is near), the
/// heavy swing's key floats over it for a moment ("Heavy [LMB]", a glyph on a pad): a heavy on a reeling creature floors it, the best
/// payoff in melee, and the 11-25 playtests show it was almost never found (Docs/CombatFeelAnalysis_2026-11-26.md §3.9). A key, not a
/// sentence (the no-text-walls rule). The shows are kept in the journal's noticed set as "hint:reeling:N", so the save needs nothing new;
/// with no character progress at all it shows once a scene. Drawn on its own screen canvas, pinned over the creature: world text sank
/// under the raid's mist veil (probe, 2026-11-26). Added to the player by Player.Awake (AddTo).
/// </summary>
[DisallowMultipleComponent]
public sealed class ReelingHint : MonoBehaviour
{
    /// <summary>How many times a save sees it.</summary>
    public const int MostShows = 3;
    /// <summary>The longest it stays up; it goes sooner when the creature stops reeling.</summary>
    public const float ShowSeconds = 2.5f;
    private const string KeyPrefix = "hint:reeling:";
    private const string Words = "Heavy [{key:Attack}]";
    // Over the creature's chest by this much: above its health bar (2.4 m on a person-sized creature).
    private const float AboveChest = 1.6f;
    // Canvas units at 1080p (the canvas scales with the screen).
    private const float FontSize = 34f;
    private const int SortingOrder = 5;

    private Player player;
    private PlayerFocusTarget focus;
    private Canvas canvas;
    private TMP_Text label;
    private MonsterAI showingOn;
    private float showUntil = -1f;
    private int keysVersion = -1;
    private bool shownThisScene;

    public static void AddTo(GameObject owner)
    {
        if (owner != null && !owner.TryGetComponent(out ReelingHint _))
        {
            owner.AddComponent<ReelingHint>();
        }
    }

    /// <summary>The noticed key of the <paramref name="index"/>th show (0-based).</summary>
    public static string ShowKey(int index) => KeyPrefix + (index + 1);

    /// <summary>How many of its shows a save's journal has had.</summary>
    public static int ShowsHad(JournalRecord journal)
    {
        if (journal == null)
        {
            return 0;
        }

        int had = 0;
        while (had < MostShows && journal.HasNoticed(ShowKey(had)))
        {
            had++;
        }

        return had;
    }

    /// <summary>Notes one show in the journal: false once it has had them all.</summary>
    public static bool NoteShow(JournalRecord journal)
    {
        int had = ShowsHad(journal);
        return journal != null && had < MostShows && journal.NoteNoticed(ShowKey(had));
    }

    // Its own lookups: it is added in Player.Awake before Player's own references are set.
    private void Awake()
    {
        player = GetComponent<Player>();
        focus = GetComponent<PlayerFocusTarget>();
    }

    private void OnDestroy()
    {
        if (canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    private void LateUpdate()
    {
        MonsterAI focused = focus != null && focus.Target != null ? focus.Target.GetComponent<MonsterAI>() : null;
        // The creature was taken away (a corpse pooled, a scene unloaded) while the hint was up.
        if (showingOn == null && label != null && label.enabled)
        {
            Hide();
        }

        // Focus starts it; once up it stays on that creature until it steadies (IsReeling is false for the dead and the staggered) or
        // the time is up, so a focus flickering to another body doesn't blink it away.
        if (showingOn != null)
        {
            if (!showingOn.IsReeling || Time.time >= showUntil)
            {
                Hide();
            }
            else
            {
                Place();
            }

            return;
        }

        if (focused == null || !focused.IsReeling || Player.Current != player || !MayShow())
        {
            return;
        }

        Show(focused);
    }

    private bool MayShow()
    {
        JournalRecord journal = GameSession.Progress != null ? GameSession.Progress.Journal : null;
        return journal != null ? ShowsHad(journal) < MostShows : !shownThisScene;
    }

    private void Show(MonsterAI creature)
    {
        JournalRecord journal = GameSession.Progress != null ? GameSession.Progress.Journal : null;
        if (journal != null)
        {
            NoteShow(journal);
        }

        shownThisScene = true;
        showingOn = creature;
        showUntil = Time.time + ShowSeconds;
        EnsureLabel();
        if (keysVersion != GameInput.KeysVersion)
        {
            keysVersion = GameInput.KeysVersion;
            label.text = GameInput.Keys(Words);
        }

        label.enabled = true;
        Place();
    }

    private void Hide()
    {
        showingOn = null;
        if (label != null)
        {
            label.enabled = false;
        }
    }

    // Over the creature on the screen; hidden while that spot is behind the camera.
    private void Place()
    {
        Camera view = Camera.main;
        Vector3 world = showingOn.transform.position + Vector3.up * (showingOn.ChestHeight + AboveChest);
        Vector3 screen = view != null ? view.WorldToScreenPoint(world) : new Vector3(0f, 0f, -1f);
        label.enabled = screen.z > 0f;
        if (label.enabled)
        {
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);
            ((RectTransform)label.transform).anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
        }
    }

    private void EnsureLabel()
    {
        if (label != null)
        {
            return;
        }

        // Its own overlay canvas, not under the player: it goes with this component.
        var holder = new GameObject("Reeling Hint", typeof(RectTransform));
        canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = holder.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Anchored at the screen's bottom left, so a screen point (over the scale) is its position.
        label = CodeUI.Label("Hint", holder.transform, 0f, 0f, 400f, 60f, FontSize, Color.white, TextAlignmentOptions.Center);
        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        label.overflowMode = TextOverflowModes.Overflow;
        UIStyle.Current.OnWorld(label, title: true);
        label.fontSize = FontSize;
        label.fontStyle = FontStyles.Bold;
        // The reeling ember colour, lifted toward white: the pure ember sank into the mist (probe, 2026-11-26).
        label.color = Color.Lerp(CombatFeelSettings.Current.ReelingBarColor, Color.white, 0.45f);
        label.enabled = false;
    }
}
