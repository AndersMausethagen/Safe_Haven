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
    private const float LookSeconds = 5f;

    // Who is cheered, until when (Time.time).
    private static readonly Dictionary<ResidentDefinition, float> Cheered = new Dictionary<ResidentDefinition, float>();

    // The day each scene was last played (by its index), so none comes round twice a day.
    private static readonly Dictionary<int, int> PlayedOn = new Dictionary<int, int>();

    private SmallSceneSettings.Scene running;
    private Vector3 spot;
    private Vector3 side;
    private float endsAt;
    private float nextAt;
    private float nextLook;
    private bool told;
    private int line;

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

    /// <summary>The work speed <paramref name="who"/> adds for being cheered by a scene nearby (0 when not).</summary>
    public static float CheerOf(ResidentDefinition who) =>
        who != null && Cheered.TryGetValue(who, out float until) && Time.time < until ? SmallSceneSettings.Current.CheerSpeed : 0f;

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
        End(false);
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
                && !(PlayedOn.TryGetValue(i, out int day) && day == today) && Content(scene.first) && Content(scene.second)
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
        line = 0;
        endsAt = Time.time + picked.lines.Length * settings.LineSeconds + settings.LingerSeconds + 20f;
        ResidentLife.Of(picked.first).Rethink();
        ResidentLife.Of(picked.second).Rethink();
    }

    /// <summary>Says the scene's lines in turn, as words in passing (the mark clicked in the view, or the player walking near).</summary>
    public void Tell()
    {
        if (running == null || told)
        {
            return;
        }

        told = true;
        line = 0;
        SayNext();
    }

    private void SayNext()
    {
        SmallSceneSettings.Scene scene = running;
        if (scene == null || line >= scene.lines.Length || DialogueBox.Current == null)
        {
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

        // A bark that would interrupt something more important is dropped, and the rest with it.
        DialogueBox.Current.Bark(new DialogueSpeaker(who.DisplayName, life.transform, null, 0f, null, life.transform), said, () =>
        {
            if (running == scene)
            {
                SayNext();
            }
        });
    }

    // The scene over: everyone near it cheered when it ran its course, and the two back to their day.
    private void End(bool ranItsCourse)
    {
        if (running == null)
        {
            return;
        }

        SmallSceneSettings.Scene scene = running;
        running = null;
        SmallSceneSettings settings = SmallSceneSettings.Current;
        nextAt = Time.time + settings.RestSeconds;
        if (ranItsCourse)
        {
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

        foreach (ResidentDefinition who in new[] { scene.first, scene.second })
        {
            ResidentLife life = ResidentLife.Of(who);
            if (life != null)
            {
                life.Rethink();
            }
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
        if (scene.at == SmallSceneSettings.Spot.Water && Shore(portal, out Vector3 shore))
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

    // A step inland of the still water's shore nearest the portal.
    private static bool Shore(Vector3 portal, out Vector3 at)
    {
        at = default;
        float best = float.MaxValue;
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.IsStill)
            {
                continue;
            }

            Vector3 shore = water.NearestPoint(portal);
            float distance = Flat(shore - portal);
            if (distance < best && distance < 190f)
            {
                best = distance;
                Vector3 inland = portal - shore;
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
