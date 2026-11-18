using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's festivals (<see cref="FestivalDefinition"/>): on a festival's day and hours a bonfire is lit beside the
/// gathering fire (or the portal), its music plays there, the residents gather round it (ResidentLife) and speak its
/// lines (ResidentAsides), and the player who joins them gets its boon, once a festival. It ends by itself.
/// <para>Where it belongs: once in the Haven scene, beside the Seasons (set up by AgentScripts/BuildSeasonalLife.cs).</para>
/// </summary>
public sealed class Festivals : MonoBehaviour
{
    [SerializeField] private List<FestivalDefinition> festivals = new List<FestivalDefinition>();
    [SerializeField] private GameObject bonfire;
    [Tooltip("Metres from the bonfire within which the player joins in (and gets the boon).")]
    [SerializeField, Min(2f)] private float joinWithin = 12f;

    private GameObject lit;
    private AudioSource music;
    private int boonGivenOn = -1;
    private float nextCheck;

    /// <summary>The festival going on now, if any.</summary>
    public static FestivalDefinition Now { get; private set; }

    /// <summary>Where it is held (the bonfire), while one is on.</summary>
    public static Vector3 Place { get; private set; }

    public void Configure(List<FestivalDefinition> year, GameObject fire)
    {
        festivals = year;
        bonfire = fire;
    }

    /// <summary>The festival on at a day of the year and hour, if any.</summary>
    public static FestivalDefinition On(IReadOnlyList<FestivalDefinition> festivals, int day, float hour)
    {
        if (festivals == null)
        {
            return null;
        }

        foreach (FestivalDefinition festival in festivals)
        {
            if (festival != null && festival.IsOn(day, hour))
            {
                return festival;
            }
        }

        return null;
    }

    private void OnDisable()
    {
        End();
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + 1f;
        Seasons seasons = Seasons.Active;
        int year = seasons != null && seasons.Cycle != null ? seasons.Cycle.YearDays : 0;
        int day = year > 0 ? (int)Mathf.Repeat(Mathf.Floor(seasons.Day), year) : -1;
        FestivalDefinition on = day >= 0 ? On(festivals, day, AtmosphereConditions.Hour) : null;
        if (on != Now)
        {
            End();
            if (on != null)
            {
                Begin(on);
            }
        }

        if (Now != null)
        {
            Join(GameSession.Here.DaysPassed);
        }
    }

    private void Begin(FestivalDefinition festival)
    {
        Now = festival;
        Place = Where();
        if (bonfire != null)
        {
            lit = Instantiate(bonfire, Place, Quaternion.identity);
            lit.name = festival.DisplayName + " Bonfire";
        }

        AudioClip clip = festival.Music != null && festival.Music.Clips.Count > 0 ? festival.Music.Clips[0] : null;
        if (clip != null)
        {
            var go = new GameObject(festival.DisplayName + " Music");
            go.transform.position = Place + Vector3.up * 1.5f;
            music = go.AddComponent<AudioSource>();
            AudioManager.Configure(music, festival.Music);
            music.clip = clip;
            music.loop = true;
            music.spatialBlend = 1f;
            music.volume = AudioManager.SteadyVolume(festival.Music);
            music.Play();
        }

        if (!string.IsNullOrEmpty(festival.Announcement))
        {
            GameMessages.Post(festival.Announcement);
        }
    }

    private void End()
    {
        Now = null;
        if (lit != null)
        {
            Destroy(lit);
        }

        if (music != null)
        {
            Destroy(music.gameObject);
        }

        lit = null;
        music = null;
    }

    private void Join(int today)
    {
        Player player = Player.Current;
        // Once a festival day, kept in the save (the journal's notes): a field on this component started again with every
        // homecoming, and the boon was handed out again each time the player came back during a festival.
        string joined = $"festival:{today}";
        JournalRecord journal = GameSession.Progress.Journal;
        if (player == null || boonGivenOn == today || journal.HasTried(joined) || (player.transform.position - Place).sqrMagnitude > joinWithin * joinWithin)
        {
            return;
        }

        boonGivenOn = today;
        journal.NoteTried(joined);
        PlayerBoons boons = player.GetComponent<PlayerBoons>();
        if (boons != null && Now.Boon.Count > 0)
        {
            boons.Add("festival-" + Now.name, Now.BoonName, new List<StatBonus>(Now.Boon), Now.BoonSeconds, false);
            GameMessages.Post($"You join the {Now.DisplayName}. {Now.BoonName}.");
        }
    }

    // Beside the gathering fire if there is one, else in front of the portal, on the navmesh.
    private static Vector3 Where()
    {
        SettlementSettings settings = SettlementSettings.Current;
        BuildingObject fire = settings != null && settings.ResidentGatheringPlace != null ? BuildingObject.Find(settings.ResidentGatheringPlace) : null;
        Vector3 at = fire != null ? fire.transform.position + new Vector3(3.5f, 0f, -3.5f)
            : HomePortal.Current != null ? HomePortal.Current.transform.position + new Vector3(0f, 0f, -9f) : Vector3.zero;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            at = hit.position;
        }

        return at;
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Place = Vector3.zero;
    }
}
