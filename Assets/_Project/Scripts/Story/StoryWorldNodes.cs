using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Story graph nodes in the "World" menu. Story nodes that change the world: creatures (spawn, despawn, hunt), objects
// (show, move, make usable, glow), the ground, weather, time of day and look, a walking party, and any scene event.
// Each is small: settings, a Title and Category for the graph's Add menu, and Run (or Listen for a trigger). See
// StoryNode.

[Serializable]
public sealed class SpawnEnemiesNode : StoryNode, IStoryWait
{
    [Tooltip("The zone fills itself now. Create one with the + button and draw its area in the scene.")]
    public MobSpawnZone zone;
    [Tooltip("Carry on only once everything it spawned is dead.")]
    public bool waitUntilDefeated;
    [Tooltip("With Wait Until Defeated: or after this many seconds, so a creature nobody can reach can't hold the story for good.")]
    [Min(1f)] public float orAfterSeconds = 300f;

    public float MostSeconds => waitUntilDefeated ? orAfterSeconds : 0f;

    public override bool IsFight => true;

    public override string Title => "Spawn Enemies";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (zone == null)
        {
            yield break;
        }

        // Nothing made (no room for them, or another game makes the creatures): no fight to wait out.
        int made = zone.Spawn();
        float giveUpAt = Time.time + orAfterSeconds;
        while (waitUntilDefeated && made > 0 && !zone.IsCleared && Time.time < giveUpAt)
        {
            yield return null;
        }
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

[Serializable]
public sealed class DespawnEnemiesNode : StoryNode
{
    [Tooltip("Everything this zone spawned goes away, and it stops respawning: the hunt is over, the party has moved on.")]
    public MobSpawnZone zone;

    public override string Title => "Remove Enemies";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (zone != null)
        {
            zone.Despawn();
        }

        yield break;
    }

    public override string FindProblem() => zone == null ? "choose the Mob Spawn Zone" : null;
}

[Serializable]
public sealed class SetActiveNode : StoryNode
{
    public GameObject target;
    public bool active = true;

    public override string Title => active ? "Show Object" : "Hide Object";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target != null)
        {
            target.SetActive(active);
        }

        yield break;
    }

    public override string FindProblem() => target == null ? "choose the object" : null;
}

/// <summary>
/// Wakes a creature placed as a story puppet (its MonsterAI and NavMeshAgent switched off) so it walks to a place by
/// itself and fights whoever it meets there, with its usual chase and leash; or puts it back to sleep so Move Object
/// can lead it again. E.g. the prologue's old troll heads for the camp fire.
/// </summary>
[Serializable]
public sealed class CreatureHuntsNode : StoryNode
{
    [Tooltip("The creature (switched off in the scene while it is a puppet).")]
    public MonsterAI creature;
    [Tooltip("On: it wakes and walks here, fighting anyone on the way or there. Off: it goes back to being a puppet.")]
    public bool hunt = true;
    [Tooltip("Where it heads when it wakes.")]
    public Transform towards;
    [Tooltip("How fast it walks there, as a share of its own speed: 1 as fast as it hunts, about a third a slow, heavy walk.")]
    [Range(0.1f, 1f)] public float walkSpeed = 1f;
    [Tooltip("Once there it stays: it fights anyone within this many metres of the place and chases no further, and what " +
             "it meets on the way doesn't stop it. 0: it hunts as it likes from wherever it finds someone.")]
    [Min(0f)] public float stayWithin;

    public override bool IsFight => true;

    public override string Title => hunt ? "Creature Hunts" : "Creature Stops Hunting";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (creature == null)
        {
            yield break;
        }

        UnityEngine.AI.NavMeshAgent agent = creature.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (!hunt)
        {
            creature.enabled = false;
            if (agent != null) agent.enabled = false;
            yield break;
        }

        if (agent != null)
        {
            agent.enabled = true;
            if (UnityEngine.AI.NavMesh.SamplePosition(creature.transform.position, out UnityEngine.AI.NavMeshHit ground, 6f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(ground.position);
            }
        }

        creature.enabled = true;
        // A frame for the creature to set itself up before it is sent on its way.
        yield return null;
        if (towards != null)
        {
            creature.HoldsHome = stayWithin > 0f;
            creature.LeashOverride = stayWithin;
            creature.RoamSpeedScale = walkSpeed;
            creature.Roam(towards.position);
        }
    }

    public override string FindProblem() => creature == null ? "choose the creature" : hunt && towards == null ? "choose where it heads" : null;
}

[Serializable]
public sealed class MoveObjectNode : StoryNode
{
    [Tooltip("What moves (a creature walks: its Animator's Speed is set while it moves).")]
    public Transform target;
    [Tooltip("Where it goes; it turns to face the way it is going.")]
    public Transform destination;
    [Min(0f)] public float seconds = 4f;
    [Tooltip("Animation playback multiplier while moving. Lower values give large creatures a slower, heavier gait.")]
    [Min(0.05f)] public float animationSpeed = 1f;
    [Tooltip("Carry on only once it has arrived.")]
    public bool waitUntilArrived;
    [Tooltip("Keeps it on the ground over hills and hollows (a creature walking). Off: a straight line between the two points (something flying).")]
    public bool followGround;

    private static readonly int Speed = Animator.StringToHash("Speed");

    public override string Title => "Move Object";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target == null || destination == null)
        {
            yield break;
        }

        IEnumerator moving = Move(target, destination.position, seconds, animationSpeed, followGround);
        if (waitUntilArrived)
        {
            yield return moving;
        }
        else
        {
            context.Script.StartCoroutine(moving);
        }
    }

    private static IEnumerator Move(Transform mover, Vector3 to, float seconds, float animationSpeed, bool followGround)
    {
        Vector3 from = mover.position;
        Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
        if (flat.sqrMagnitude > 0.01f)
        {
            mover.rotation = Quaternion.LookRotation(flat);
        }

        Animator animator = mover.GetComponentInChildren<Animator>();
        bool walks = animator != null && HasSpeed(animator);
        float previousSpeed = animator != null ? animator.speed : 1f;
        float speed = seconds > 0f ? Vector3.Distance(from, to) / seconds : 0f;
        try
        {
            if (walks) animator.speed = previousSpeed * animationSpeed;
            for (float t = 0f; t < seconds && mover != null; t += Time.deltaTime)
            {
                mover.position = OnGround(Vector3.Lerp(from, to, t / seconds), followGround);
                if (walks) animator.SetFloat(Speed, speed);

                yield return null;
            }
            if (mover != null) mover.position = OnGround(to, followGround);
        }
        finally
        {
            if (walks && animator != null)
            {
                animator.SetFloat(Speed, 0f);
                animator.speed = previousSpeed;
            }
        }
    }

    // The terrain's height under the point, when it stands over one.
    private static Vector3 OnGround(Vector3 point, bool followGround)
    {
        if (!followGround)
        {
            return point;
        }

        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 local = point - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x >= 0f && local.z >= 0f && local.x <= size.x && local.z <= size.z)
            {
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                return point;
            }
        }

        return point;
    }

    private static bool HasSpeed(Animator animator)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }

    public override string FindProblem() => target == null ? "choose what moves" : destination == null ? "choose where it goes" :
        animationSpeed <= 0f ? "animation speed must be positive" : null;
}

/// <summary>Plays one of a creature's or person's animations by its state's name in their Animator (the troll's Roar, a
/// creature's Hop), cross-fading into it; the Animator's own transitions take it back. For a cinematic beat.</summary>
[Serializable]
public sealed class PlayAnimationNode : StoryNode, IStoryWait
{
    [Tooltip("Who plays it: the object with the Animator, or its parent.")]
    public Transform target;
    [Tooltip("The state's name in the Animator's base layer, e.g. Roar.")]
    public string state = "Roar";
    [Tooltip("Seconds of blending into it.")]
    [Min(0f)] public float crossFade = 0.2f;
    [Tooltip("Carry on after this many seconds (how long it plays, for what comes after). 0: at once.")]
    [Min(0f)] public float waitSeconds;

    public float MostSeconds => Mathf.Max(0.1f, waitSeconds);

    public override string Title => "Play Animation";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        Animator animator = target != null ? target.GetComponentInChildren<Animator>() : null;
        if (animator == null || string.IsNullOrEmpty(state))
        {
            yield break;
        }

        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning($"Play Animation: {animator.name} has no state \"{state}\" in its base layer.", context.Script);
            yield break;
        }

        animator.CrossFadeInFixedTime(hash, crossFade, 0);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    public override string FindProblem() => target == null ? "choose who plays it" : string.IsNullOrEmpty(state) ? "name the animation state" : null;
}

[Serializable]
public sealed class SetUsableNode : StoryNode
{
    [Tooltip("Whether the player can use this object (its Interactable): off when its part in the story is over.")]
    public Interactable target;
    public bool usable;

    public override string Title => usable ? "Make Usable" : "Stop Being Usable";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target != null)
        {
            target.enabled = usable;
        }

        yield break;
    }

    public override string FindProblem() => target == null ? "choose the object" : null;
}

[Serializable]
public sealed class GlowNode : StoryNode
{
    [Tooltip("What glows: a light appears at it (and fades out again when Glow is off). It lifts the dark around it, so it is seen at night.")]
    public Transform target;
    public bool glow = true;
    public Color colour = new Color(0.55f, 0.85f, 1f);
    [Min(0f)] public float intensity = 4f;
    [Min(0.1f)] public float range = 6f;
    [Min(0f)] public float fadeSeconds = 1.5f;

    public override string Title => glow ? "Start Glowing" : "Stop Glowing";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (target == null)
        {
            yield break;
        }

        Transform holder = target.Find("Story Glow");
        Light light = holder != null ? holder.GetComponent<Light>() : null;
        if (light == null)
        {
            var go = new GameObject("Story Glow");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.up;
            light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
        }

        if (!light.TryGetComponent(out VisibilitySource sight))
        {
            sight = light.gameObject.AddComponent<VisibilitySource>();
        }

        sight.Configure(range * 0.6f, 0.5f, true, range * 0.6f);
        sight.enabled = glow;

        light.color = colour;
        light.range = range;
        float from = light.intensity;
        float to = glow ? intensity : 0f;
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            light.intensity = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }

        light.intensity = to;
    }

    public override string FindProblem() => target == null ? "choose what glows" : null;
}

[Serializable]
public sealed class WitherGroundNode : StoryNode
{
    [Tooltip("The grass around this dies and turns brown (or comes back when Wither is off).")]
    public Transform centre;
    public bool wither = true;
    [Min(0.5f)] public float radius = 6f;
    [Min(0f)] public float seconds = 3f;

    public override string Title => wither ? "Wither Ground" : "Restore Ground";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (centre == null)
        {
            yield break;
        }

        if (!centre.TryGetComponent(out WitherSpot spot))
        {
            spot = centre.gameObject.AddComponent<WitherSpot>();
            spot.Amount = 0f;
        }

        spot.Radius = radius;
        float from = spot.Amount;
        float to = wither ? 1f : 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            spot.Amount = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        spot.Amount = to;
    }

    public override string FindProblem() => centre == null ? "choose where" : null;
}

[Serializable]
public sealed class ChangeWeatherNode : StoryNode
{
    public WeatherDefinition weather;
    [Min(0f)] public float transitionSeconds = 4f;

    public override string Title => "Change Weather";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        WeatherSystem system = UnityEngine.Object.FindAnyObjectByType<WeatherSystem>();
        if (system != null)
        {
            system.SetWeather(weather, transitionSeconds);
        }
        else
        {
            Debug.LogWarning("Change Weather: the scene has no WeatherSystem (World Tools › Sky & Weather).", context.Script);
        }

        yield break;
    }

    public override string FindProblem() => weather == null ? "choose the weather" : null;
}

[Serializable]
[WaitsForThePlayer]
public sealed class ChangeTimeOfDayNode : StoryNode
{
    [Tooltip("The hour the clock moves forward to (7.5 is early morning, 19 sunset, 23 night).")]
    [Range(0f, 24f)] public float hour = 21f;
    [Min(0f)] public float seconds = 20f;
    [Tooltip("Instead of Seconds, it follows the party's walk (Walk The Party): the hour arrives as they do, and the sun waits when they wait.")]
    public bool followWalk;
    [Tooltip("Blends straight to that hour's look over Seconds, rather than running the clock there, so shadows don't race over the ground. Several of these along a walk make a day go by.")]
    public bool fadeStraightThere;

    public override string Title => "Change Time of Day";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        DayNightCycle cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            Debug.LogWarning("Change Time of Day: the scene has no DayNightCycle (World Tools › Sky & Weather).", context.Script);
            yield break;
        }

        if (fadeStraightThere)
        {
            cycle.FadeToHour(hour, seconds);
            yield break;
        }

        if (!followWalk)
        {
            cycle.GoToHour(hour, seconds);
            yield break;
        }

        // A frame first: the walk may have started beside this.
        yield return null;
        PartyWalk walk = PartyWalk.Current;
        float from = cycle.Hour;
        float to = DayNightCycle.ForwardTo(from, hour);
        while (walk != null && !walk.IsDone)
        {
            cycle.Hour = Mathf.Lerp(from, to, walk.Progress);
            yield return null;
        }

        cycle.Hour = hour;
    }
}

[Serializable]
public sealed class ChangeLookNode : StoryNode
{
    [Tooltip("The scene's Scene Look: a second set of post-processing volumes and day and night lighting.")]
    public SceneLook look;
    [Tooltip("0: the scene's first look, 1: the second.")]
    [Range(0f, 1f)] public float amount = 1f;
    [Min(0f)] public float seconds = 8f;

    public override string Title => "Change Look";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (look != null)
        {
            look.FadeTo(amount, seconds);
        }

        yield break;
    }

    public override string FindProblem() => look == null ? "choose the Scene Look" : null;
}

[Serializable]
[WaitsForThePlayer]
public sealed class WalkPartyNode : StoryNode
{
    private const float RefreshSeconds = 0.25f;
    private const float Lead = 4f;

    [Tooltip("An empty object whose children are the route's points, in order: the party walks through them.")]
    public Transform route;
    [Tooltip("Who walks. Empty: every companion in the scene.")]
    public List<Companion> companions = new List<Companion>();
    [Tooltip("The party's steady pace, in metres a second (the player's walk is faster than this).")]
    [Min(0.5f)] public float pace = 2.6f;
    [Tooltip("They stop and wait when the player falls this far behind. 0: they don't wait.")]
    [Min(0f)] public float waitForPlayer = 22f;
    [Tooltip("Carry on only once they have arrived. Off: what follows starts as they set out.")]
    public bool waitUntilArrived = true;

    public override string Title => "Walk The Party";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (route == null || route.childCount < 2)
        {
            yield break;
        }

        var points = new Vector3[route.childCount];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = route.GetChild(i).position;
        }

        var walk = new PartyWalk(points);
        PartyWalk.Current = walk;
        var party = new List<Companion>(companions.Count > 0 ? companions : new List<Companion>(Companion.Active));
        IEnumerator walking = Walk(walk, party, context);
        if (waitUntilArrived)
        {
            yield return walking;
        }
        else
        {
            context.Script.StartCoroutine(walking);
        }
    }

    private IEnumerator Walk(PartyWalk walk, List<Companion> party, StoryContext context)
    {
        foreach (Companion member in party)
        {
            member.SetPace(pace);
        }

        try
        {
            float refresh = 0f;
            while (!walk.IsDone)
            {
                if (!MustWait(walk, party, context.Player))
                {
                    walk.Advance(pace * Time.deltaTime);
                }

                refresh -= Time.deltaTime;
                if (refresh <= 0f)
                {
                    refresh = RefreshSeconds;
                    Order(walk, party);
                }

                yield return null;
            }

            // Into their places at the end of the road, where the last order leads them.
            Order(walk, party);
        }
        finally
        {
            foreach (Companion member in party)
            {
                if (member != null) member.SetPace(0f);
            }
        }
    }

    // Each walks to a spot a little ahead of them on the road (kept in formation, turned with the road), so they stride
    // steadily at the party's pace instead of catching up and stopping.
    private static void Order(PartyWalk walk, List<Companion> party)
    {
        Quaternion turn = Quaternion.LookRotation(walk.Heading(Lead));
        Vector3 spot = walk.Ahead(Lead);
        foreach (Companion member in party)
        {
            if (member != null) member.Command(CompanionOrder.Stay, spot + turn * member.FormationOffset);
        }
    }

    private bool MustWait(PartyWalk walk, List<Companion> party, Player player)
    {
        // Nobody walks off during a conversation or a fight, or from someone who has fallen behind.
        if (DialogueBox.IsOpen || MonsterAI.AnyHuntingPlayerSide())
        {
            return true;
        }

        Vector3 here = walk.Position;
        if (waitForPlayer > 0f && player != null && Flat(player.transform.position - here) > waitForPlayer && Behind(walk, player))
        {
            return true;
        }

        foreach (Companion member in party)
        {
            if (member != null && Flat(member.transform.position - here) > Lead + 8f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Behind(PartyWalk walk, Player player) =>
        Vector3.Dot(player.transform.position - walk.Position, walk.Heading()) < 0f;

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }

    public override string FindProblem() => route == null ? "choose the route (an object with its points as children)" :
        route.childCount < 2 ? "the route needs at least two points (children of it)" : null;
}

[Serializable]
public sealed class RevealGroundNode : StoryNode
{
    [Tooltip("The ground around this becomes explored: the fog of war lifts and it shows on the map, as if the player had already been there.")]
    public Transform centre;
    [Min(1f)] public float radius = 60f;

    public override string Title => "Reveal Ground";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        if (centre != null && ExplorationMap.Current != null)
        {
            ExplorationMap.Current.RevealAround(centre.position, radius);
        }

        yield break;
    }

    public override string FindProblem() => centre == null ? "choose where" : null;
}

[Serializable]
public sealed class InvokeEventNode : StoryNode
{
    [Tooltip("Anything else: any component's method or property, picked in the Inspector.")]
    public UnityEvent response = new UnityEvent();

    public override string Title => "Do Something";
    public override string Category => "World";

    public override IEnumerator Run(StoryContext context)
    {
        response.Invoke();
        yield break;
    }
}
