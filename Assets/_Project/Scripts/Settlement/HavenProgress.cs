using UnityEngine;

/// <summary>
/// Watches Safe Haven grow: adds up its renown (HavenRenown) whenever something is built, upgraded or cleared, shows
/// the gain, and celebrates each new rank once: a banner, a sound, experience, and the ward widening (HavenSettings), so
/// the moss spreads out to the new edge and there is new ground to build on. Clearing a quarter, half, three quarters
/// and all of the overgrowth is announced too. Added to the Heart Tree when the scene starts; makes the Haven HUD (the
/// overview is a page of the Heart Tree's menu, H: HeartTree.OpenHaven).
/// </summary>
[DisallowMultipleComponent]
public sealed class HavenProgress : MonoBehaviour
{
    private const float RecountSeconds = 2f;

    private int renown = -1;
    private int clearedQuarter = -1;
    private float nextCount;

    public static HavenProgress Current { get; private set; }
    public int Renown => Mathf.Max(0, renown);

    private void OnEnable()
    {
        Current = this;
        Construction.Finished += OnChanged;
        Overgrowth.Cleared += OnCleared;
    }

    private void OnDisable()
    {
        Construction.Finished -= OnChanged;
        Overgrowth.Cleared -= OnCleared;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Start()
    {
        HavenHud.Create();
        clearedQuarter = Mathf.FloorToInt(Overgrowth.ClearedShare * 4f + 0.001f);
        Count(false);
    }

    private void Update()
    {
        if (Time.time >= nextCount)
        {
            Count(true);
        }
    }

    private void OnChanged(Construction _) => Count(true);

    private void OnCleared(Overgrowth _)
    {
        int quarter = Mathf.FloorToInt(Overgrowth.ClearedShare * 4f + 0.001f);
        if (quarter > clearedQuarter)
        {
            clearedQuarter = quarter;
            GameMessages.Post(quarter >= 4 ? "The clearing is free of the overgrowth. The whole glade breathes again."
                : $"The clearing breathes again: {quarter * 25}% cleared.", MessageKind.Gain);
            ExperienceTracker.Grant(25 * quarter);
        }

        Count(true);
    }

    /// <summary>Adds the renown up again; a gain is shown, a new rank celebrated.</summary>
    public void Count(bool announce)
    {
        nextCount = Time.time + RecountSeconds;
        int now = HavenRenown.Compute();
        if (announce && renown >= 0 && now > renown)
        {
            HavenHud.Current?.ShowGain(now - renown);
        }

        renown = now;
        HavenSettings settings = HavenSettings.Current;
        int rank = settings.RankFor(renown);
        if (GameSession.Settlement.ReachHavenRank(rank))
        {
            HavenSettings.Rank reached = settings.Ranks[rank];
            HavenHud.Current?.ShowRank(reached.name, reached.line);
            AudioManager.Play(settings.RankSound, transform.position);
            CameraShake.Add(0.15f);
            ExperienceTracker.Grant(settings.RankExperience);
            GameMessages.Post($"Safe Haven is now a {reached.name}! The ward reaches {HeartTree.Current?.WardRadius:0} m.", MessageKind.Gain);
        }
    }
}
