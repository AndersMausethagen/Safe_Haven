using UnityEngine;

/// <summary>
/// Watches Safe Haven grow: adds up its renown (HavenRenown) whenever something is built, upgraded or cleared, shows
/// the gain, and celebrates each new rank once: a banner, a sound, experience, and the ward widening (HavenSettings), so
/// the moss spreads out to the new edge and there is new ground to build on. Clearing a quarter, half, three quarters
/// and all of the overgrowth is announced too. Added to the Heart Tree when the scene starts; makes the Haven HUD and the
/// Haven window (H).
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
        HavenWindow.Create();
        KeepTrackOfBuildings();
        TellWhatWorkersMade();
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
            ExperienceTracker.Grant(HavenSettings.Current.ClearingExperiencePerQuarter * quarter);
        }

        Count(true);
    }

    /// <summary>Adds the renown up again; a gain is shown, a new rank celebrated.</summary>
    public void Count(bool announce)
    {
        nextCount = Time.time + RecountSeconds;
        KeepTrackOfBuildings();
        int now = HavenRenown.Compute();
        if (announce && renown >= 0 && now > renown)
        {
            if (HavenHud.Current != null)
            {
                HavenHud.Current.ShowGain(now - renown);
            }
        }

        renown = now;
        HavenSettings settings = HavenSettings.Current;
        int rank = settings.RankFor(renown);
        if (GameSession.Settlement.ReachHavenRank(rank))
        {
            HavenSettings.Rank reached = settings.Ranks[rank];
            if (HavenHud.Current != null)
            {
                HavenHud.Current.ShowRank(reached.name, reached.line);
            }
            AudioManager.Play(settings.RankSound, transform.position);
            CameraShake.Add(0.15f);
            ExperienceTracker.Grant(settings.RankExperience);
            GameMessages.Post($"Safe Haven is now a {reached.name}! The ward reaches {(HeartTree.Current != null ? HeartTree.Current.WardRadius : 0f):0} m.", MessageKind.Gain);
        }

        RaiseRankEvents(settings);
    }

    // Raids can't see the base, so what they go by is noted here; touching a staffed station's queue keeps its tier and speed
    // current, and creates it, so its worker can start.
    private static void KeepTrackOfBuildings()
    {
        GameSession.Settlement.NoteBuildings(System.Linq.Enumerable.Where(BuildingObject.Active, IsStanding));
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station.HasWorker && station.Usable)
            {
                _ = station.Queue;
            }
        }
    }

    private static bool IsStanding(BuildingObject building) => !building.TryGetComponent(out Construction construction) || construction.IsStanding;

    private static void TellWhatWorkersMade()
    {
        var made = GameSession.Settlement.MadeByWorkers;
        if (made.Count == 0)
        {
            return;
        }

        var lines = new System.Collections.Generic.List<string>();
        foreach (var entry in made)
        {
            lines.Add($"{entry.Value} {entry.Key.DisplayName}");
        }

        made.Clear();
        GameMessages.Post($"While you were gone your people made {string.Join(", ", lines)}.", MessageKind.Gain);
    }

    // Every rank reached counts, so a quest about a rank also finishes for a haven that got there before the event existed.
    private static void RaiseRankEvents(HavenSettings settings)
    {
        for (int i = 0; i <= GameSession.Settlement.HavenRank && i < settings.Ranks.Count; i++)
        {
            GameEventDefinition reachedEvent = settings.Ranks[i].reachedEvent;
            if (reachedEvent != null && !GameSession.Quests.HasHappened(reachedEvent))
            {
                GameEvents.Raise(reachedEvent, Player.Current != null ? Player.Current.gameObject : null);
            }
        }
    }
}
