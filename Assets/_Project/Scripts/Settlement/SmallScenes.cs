using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small scenes between friends at home (SmallSceneSettings; Docs/ColonyModeResearch_2026-11-21.md §4.9, Wildermyth): now and then, in
/// their free time, two people who get on and are both content with their homes meet at a fire or the water and laugh, sing or tell a
/// story. Their exchange is heard when the player is near, or when its mark is clicked in the Keeper's view; when it ends, everyone near
/// it is cheered for a while (a little quicker at work: <see cref="CheerOf"/>). Positive only, never a meter. One at a time, each scene
/// once a day at most; nothing saved. On the portal, added by it; only in the game that keeps the Haven.
/// </summary>
[DisallowMultipleComponent]
public sealed class SmallScenes : MonoBehaviour
{
    private const float LookSeconds = 2f;
    // Metres from their places within which the two count as there; seconds they have to get there before the scene is dropped.
    private const float ThereWithin = 2.5f;
    private const float ArriveSeconds = 60f;
    // Metres from the first one a shore may be for a scene by the water.
    private const float ShoreWithin = 60f;

    // Who is cheered, until when (Time.time).
    private static readonly Dictionary<ResidentDefinition, float> Cheered = new Dictionary<ResidentDefinition, float>();

    // The day each scene was last played (by its index), so none comes round twice a day.
    private static readonly Dictionary<int, int> PlayedOn = new Dictionary<int, int>();

    private SmallSceneSettings.Scene running;
    private Vector3 spot;
    private Vector3 side;
    private float endsAt;
    private float arriveBy;
    private bool begun;
    private float nextAt;
    private float nextLook;
    private bool told;
    private bool wantsTelling;
    private bool telling;
    private int line;

    /// <summary>A scene as soon as two who may are free (a playtest cheat: PeopleCheats), whatever was played today.</summary>
    public void Soon()
    {
        nextAt = 0f;
        nextLook = 0f;
        PlayedOn.Clear();
    }

    /// <summary>The scene under way at home now, if any.</summary>
    public static SmallScenes Current { get; private set; }

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        ForgetGame();
    }

    /// <summary>Another game loaded (GameSession.Reset): nobody in it was cheered yet.</summary>
    public static void ForgetGame()
    {
        Cheered.Clear();
        PlayedOn.Clear();
    }

    /// <summary>The work speed <paramref name="who"/> adds for being cheered by a scene nearby (0 when not), as much as their traits take
    /// from a gathering (a Loner none: ResidentTraits, SceneCheer).</summary>
    public static float CheerOf(ResidentDefinition who)
    {
        if (who == null || !Cheered.TryGetValue(who, out float until) || Time.time >= until)
        {
            return 0f;
        }

        ResidentRecord record = GameSession.Here != null ? GameSession.Here.Find(who) : null;
        return SmallSceneSettings.Current.CheerSpeed * Mathf.Max(0f, 1f + ResidentTraits.Sum(record, TraitEffectKind.SceneCheer));
    }

    /// <summary>Cheers everyone at home for <paramref name="seconds"/> (a story told at the fire: Inspirations).</summary>
    public static void CheerAll(IEnumerable<ResidentRecord> residents, float seconds)
    {
        foreach (ResidentRecord record in residents)
        {
            if (record.Resident != null && !record.IsAway)
            {
                Cheered[record.Resident] = Mathf.Max(Cheered.TryGetValue(record.Resident, out float until) ? until : 0f, Time.time + seconds);
            }
        }
    }

    /// <summary>Where <paramref name="who"/> stands for the scene under way and what they face, if they are in it.</summary>
    public static bool SpotFor(ResidentDefinition who, out Vector3 at, out Vector3 lookAt)
    {
        at = lookAt = default;
        SmallScenes scenes = Current;
        if (scenes == null || scenes.running == null || who == null)
        {
            return false;
        }

        bool first = who == scenes.running.first;
        if (!first && who != scenes.running.second)
        {
            return false;
        }

        at = scenes.spot + (first ? -scenes.side : scenes.side);
        lookAt = scenes.spot + (first ? scenes.side : -scenes.side);
        return true;
    }

    /// <summary>The scene under way: where it is (for its mark) and what it looks like ("Laughing"); false when none is.</summary>
    public static bool Running(out Vector3 at, out string doing)
    {
        at = default;
        doing = null;
        SmallScenes scenes = Current;
        if (scenes == null || scenes.running == null)
        {
            return false;
        }

        at = scenes.spot;
        doing = string.IsNullOrWhiteSpace(scenes.running.doing) ? "Talking" : scenes.running.doing;
        return true;
    }

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        // The Haven closing (a raid setting out): nobody is sent anywhere while the scene unloads.
        End(false, false);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (Time.time < nextLook)
        {
            return;
        }

        nextLook = Time.time + LookSeconds;
        if (!Place.AtHome)
        {
            End(false);
            return;
        }

        SmallSceneSettings settings = SmallSceneSettings.Current;
        if (running != null)
        {
            if (!Free(running.first) || !Free(running.second))
            {
                End(false);
            }
            else if (!begun)
            {
                // Its clock starts once both are in their places; too long on the way, and it is let go.
                if (There(running.first) && There(running.second))
                {
                    begun = true;
                    endsAt = Time.time + running.lines.Length * settings.LineSeconds + settings.LingerSeconds;
                    if (wantsTelling)
                    {
                        Tell();
                    }
                }
                else if (Time.time >= arriveBy)
                {
                    End(false);
                }
            }
            else if (Time.time >= endsAt)
            {
                End(true);
            }
            else if (!told && Player.Current != null && Flat(Player.Current.transform.position - spot) <= settings.HearReach)
            {
                Tell();
            }

            return;
        }

        if (Time.time >= nextAt)
        {
            TryStart(settings);
        }
    }

    // Two content friends, both in their free time: the first such scene not yet played today, at random.
    private void TryStart(SmallSceneSettings settings)
    {
        int today = GameSession.Home.DaysPassed;
        var ready = new List<int>();
        for (int i = 0; i < settings.Scenes.Count; i++)
        {
            SmallSceneSettings.Scene scene = settings.Scenes[i];
            if (scene != null && scene.first != null && scene.second != null && scene.lines.Length > 0
                && !(PlayedOn.TryGetValue(i, out int day) && day == today) && Ready(scene, i)
                && Free(scene.first) && Free(scene.second))
            {
                ready.Add(i);
            }
        }

        if (ready.Count == 0)
        {
            return;
        }

        int chosen = ready[Random.Range(0, ready.Count)];
        SmallSceneSettings.Scene picked = settings.Scenes[chosen];
        if (!Where(picked, out spot, out side))
        {
            return;
        }

        PlayedOn[chosen] = today;
        running = picked;
        told = false;
        wantsTelling = false;
        telling = false;
        begun = false;
        line = 0;
        arriveBy = Time.time + ArriveSeconds;
        endsAt = float.MaxValue;
        ResidentLife.Of(picked.first).Rethink();
        ResidentLife.Of(picked.second).Rethink();
    }

    // In their place for the scene, near enough.
    private static bool There(ResidentDefinition who)
    {
        ResidentLife life = ResidentLife.Of(who);
        return life != null && life.IsInScene && SpotFor(who, out Vector3 at, out _) && Flat(life.transform.position - at) <= ThereWithin;
    }

    /// <summary>Says the scene's lines in turn, as words in passing (the mark clicked in the view, or the player walking near); from
    /// the start again when asked after. Before the two are in their places it waits for them.</summary>
    public void Tell()
    {
        if (running == null || telling)
        {
            return;
        }

        if (!begun)
        {
            wantsTelling = true;
            return;
        }

        told = true;
        telling = true;
        line = 0;
        SayNext();
    }

    private void SayNext()
    {
        SmallSceneSettings.Scene scene = running;
        if (scene == null || line >= scene.lines.Length || DialogueBox.Current == null)
        {
            telling = false;
            return;
        }

        ResidentDefinition who = line % 2 == 0 ? scene.first : scene.second;
        ResidentLife life = ResidentLife.Of(who);
        string said = scene.lines[line];
        line++;
        if (life == null || string.IsNullOrWhiteSpace(said))
        {
            SayNext();
            return;
        }

        // A bark that would interrupt something more important is dropped, and the rest with it (the mark can tell it again).
        bool spoken = DialogueBox.Current.Bark(new DialogueSpeaker(who.DisplayName, life.transform, null, 0f, null, life.transform), said, () =>
        {
            if (running == scene)
            {
                SayNext();
            }
        });
        if (!spoken)
        {
            telling = false;
        }
    }

    // The scene over: everyone near it cheered when it ran its course, and the two back to their day (not while the scene unloads).
    private void End(bool ranItsCourse, bool rethink = true)
    {
        if (running == null)
        {
            return;
        }

        SmallSceneSettings.Scene scene = running;
        running = null;
        telling = false;
        SmallSceneSettings settings = SmallSceneSettings.Current;
        nextAt = Time.time + settings.RestSeconds;
        if (ranItsCourse && scene.kind == SmallSceneSettings.Kind.Rivals)
        {
            Argued(scene);
        }
        else if (ranItsCourse)
        {
            Shared(scene);
            int cheered = 0;
            float reach = settings.CheerReach;
            foreach (ResidentRecord record in GameSession.Home.Residents)
            {
                ResidentLife near = ResidentLife.Of(record.Resident);
                if (near != null && Flat(near.transform.position - spot) <= reach)
                {
                    Cheered[record.Resident] = Time.time + settings.CheerSeconds;
                    cheered++;
                }
            }

            if (told)
            {
                string doing = string.IsNullOrWhiteSpace(scene.doing) ? "talking" : scene.doing.ToLowerInvariant();
                GameMessages.Post($"{scene.first.DisplayName} and {scene.second.DisplayName}, {doing}: {cheered} nearby {(cheered == 1 ? "is" : "are")} a little " +
                    "quicker at work for a while.", MessageKind.Gain);
            }
        }

        if (!rethink)
        {
            return;
        }

        foreach (ResidentDefinition who in new[] { scene.first, scene.second })
        {
            ResidentLife life = ResidentLife.Of(who);
            if (life != null)
            {
                life.Rethink();
            }
        }
    }

    // Whether a scene may play now (Docs/PeopleResearch_2026-11-23.md §3.5): a gathering between two content at home who aren't at odds;
    // a friends' scene once, when they have become Friends; a rivals' argument while they are Cool or worse.
    private static bool Ready(SmallSceneSettings.Scene scene, int index)
    {
        ResidentRecord first = GameSession.Home.Find(scene.first);
        ResidentRecord second = GameSession.Home.Find(scene.second);
        if (first == null || second == null)
        {
            return false;
        }

        int level = PeopleStandings.LevelOf(GameSession.Home, first, second);
        switch (scene.kind)
        {
            case SmallSceneSettings.Kind.Friends:
                return level >= 2 && first.DeedOf(SeenKey(scene)) == 0;
            case SmallSceneSettings.Kind.Rivals:
                return level <= -1;
            default:
                return level >= 0 && Content(scene.first) && Content(scene.second);
        }
    }

    // By who and what, not by place in the list, so reordering the scenes neither replays nor blocks one.
    private static string SeenKey(SmallSceneSettings.Scene scene) => "scene:" + scene.first.Id + "|" + scene.second.Id + "|" + scene.kind;

    // A good scene shared: the two grow closer, a friends' scene is theirs once, and a Friend may ease a hard mark.
    private static void Shared(SmallSceneSettings.Scene scene)
    {
        SettlementState home = GameSession.Home;
        ResidentRecord first = home.Find(scene.first);
        ResidentRecord second = home.Find(scene.second);
        if (first == null || second == null)
        {
            return;
        }

        PeopleStandings.SharedAScene(home, first, second);
        if (scene.kind == SmallSceneSettings.Kind.Friends)
        {
            first.AddDeed(SeenKey(scene), 1);
            second.AddDeed(SeenKey(scene), 1);
        }

        if (PeopleStandings.LevelOf(home, first, second) >= 2)
        {
            foreach (ResidentRecord eased in new[] { first, second })
            {
                MarkDefinition mark = ResidentMarks.EasedByFriends(eased);
                if (mark != null)
                {
                    PeopleNews.Tell($"An evening with a friend eased {eased.Resident.DisplayName}'s {mark.DisplayName.ToLowerInvariant()}.", MessageKind.Gain);
                }
            }
        }
    }

    // Rivals had it out: it cleared the air or hardened (PeopleStandings.Argue), said in the first one's last line if heard.
    private void Argued(SmallSceneSettings.Scene scene)
    {
        SettlementState home = GameSession.Home;
        ResidentRecord first = home.Find(scene.first);
        ResidentRecord second = home.Find(scene.second);
        if (first == null || second == null)
        {
            return;
        }

        bool cleared = PeopleStandings.Argue(home, first, second, Random.value);
        string last = cleared ? scene.cleared : scene.hardened;
        ResidentLife life = ResidentLife.Of(scene.first);
        if (told && !string.IsNullOrWhiteSpace(last) && life != null && DialogueBox.Current != null)
        {
            DialogueBox.Current.Bark(new DialogueSpeaker(scene.first.DisplayName, life.transform, null, 0f, null, life.transform), last, null);
        }

        string said = cleared ? $"{scene.first.DisplayName} and {scene.second.DisplayName} had it out at the fire, and laughed after: the air is cleared."
            : $"{scene.first.DisplayName} and {scene.second.DisplayName} had it out at the fire. It went badly.";
        // Overheard, it is said now; else it comes as news, in its turn (PeopleNews).
        if (told)
        {
            GameMessages.Post(said, cleared ? MessageKind.Gain : MessageKind.Info);
        }
        else
        {
            PeopleNews.Tell(said, cleared ? MessageKind.Gain : MessageKind.Info);
        }
    }

    private static bool Content(ResidentDefinition who)
    {
        ResidentRecord record = GameSession.Home.Find(who);
        return record != null && HomeLife.IsContent(record);
    }

    // In their free time at the fire, not on guard, asleep, talking or away.
    private static bool Free(ResidentDefinition who)
    {
        ResidentLife life = ResidentLife.Of(who);
        return life != null && life.IsFreeForAScene;
    }

    // Where the scene is: beside the gathering fire nearest the first one's home, or on the still water's shore; the two a step either
    // side of it, facing each other.
    private static bool Where(SmallSceneSettings.Scene scene, out Vector3 at, out Vector3 apart)
    {
        at = default;
        apart = default;
        ResidentLife first = ResidentLife.Of(scene.first);
        if (first == null)
        {
            return false;
        }

        Vector3 portal = HomePortal.Current != null ? HomePortal.Current.transform.position : first.transform.position;
        Vector3 from = first.transform.position;
        if (scene.at == SmallSceneSettings.Spot.Water && Shore(from, out Vector3 shore))
        {
            at = shore;
        }
        else
        {
            BuildingDefinition fire = SettlementSettings.Current.ResidentGatheringPlace;
            BuildingObject nearest = fire != null ? Yards.GatheringFire(from, fire) : null;
            if (nearest == null && fire != null)
            {
                nearest = BuildingObject.Nearest(fire, from);
            }

            Vector3 middle = nearest != null ? nearest.transform.position : portal;
            // A few steps off the fire, toward the first one, so they don't stand in the ring round it.
            Vector3 toward = from - middle;
            toward.y = 0f;
            at = middle + (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward) * 4f;
        }

        if (!UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return false;
        }

        at = hit.position;
        Vector3 across = at - portal;
        across.y = 0f;
        apart = Vector3.Cross(Vector3.up, across.sqrMagnitude > 0.01f ? across.normalized : Vector3.forward) * 0.9f;
        return true;
    }

    // A step inland of the still water's shore nearest the first one, if one is near (else the scene is by a fire).
    internal static bool Shore(Vector3 from, out Vector3 at)
    {
        at = default;
        float best = float.MaxValue;
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.IsStill)
            {
                continue;
            }

            Vector3 shore = water.NearestPoint(from);
            float distance = Flat(shore - from);
            if (distance < best && distance < ShoreWithin)
            {
                best = distance;
                Vector3 inland = from - shore;
                inland.y = 0f;
                at = shore + (inland.sqrMagnitude > 0.01f ? inland.normalized : Vector3.forward) * 2.5f;
            }
        }

        return best < float.MaxValue;
    }

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }
}
