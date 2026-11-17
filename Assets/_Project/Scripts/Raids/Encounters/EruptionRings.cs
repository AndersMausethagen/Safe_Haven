using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A boss arena's rings of eruptions at a stage of the fight: the Heartroot's Hollow's Hold at 12%, "it roots itself and calls the whole
/// hollow up" (Docs/HeartrootEncounterDesign_2026-11-17.md §2.2 and §3.4, Docs/HeartrootBossReview_2026-11-17.md C10). As its boss enters
/// the stage, rings of eruptions burst round it from the outermost in, each circle warned by its filling ring on the ground (LobStrike, the
/// same warning as Root Eruption), so the only safe ground left is near the boss's body.
/// <para>
/// The boss's own part is data: an Opener strike in that stage (Take Root: a Howl with Self Only and a long Hit Delay, so it stands rooted
/// through the rings) with Open After and Topple After, which ends the hold in a topple, the kill window. The eruptions are the boss's blows
/// (it is their maker), so they hit as hard as its stage and its rage make it, and only its foes.
/// </para>
/// <para>
/// Where it belongs: on an object in the arena scene. A friend's game (co-op) bursts its own from its copy of the boss, whose phases follow
/// the host's health, as CollapsingGround does: each game's rings only hurt that game's player (a friend's body in the host's game takes
/// no damage), so every player sees and dodges their own. A copy that arrives with the fight already past the stage bursts nothing.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "each game bursts its own rings from its copy of the boss's phases")]
public sealed class EruptionRings : MonoBehaviour
{
    [Serializable]
    public sealed class Ring
    {
        [Tooltip("Metres from the centre to the middle of this ring's circles.")]
        [Min(0.5f)] public float radius = 14f;
        [Tooltip("Seconds after the ring before (or the start, for the first) before this one's warnings appear.")]
        [Min(0f)] public float after = 1.2f;
    }

    [Tooltip("The boss whose stage starts the rings, and their maker. Known by its Boss Definition: on a friend's game (co-op) the host's copy " +
             "starts them too.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("The stage that starts them (1 is the first of the boss's Phases: the Heartroot's Hollow's Hold is its fourth phase, at 12%: 3).")]
    [SerializeField, Min(1)] private int atPhase = 3;
    [Tooltip("Where the rings are centred. Empty: on the boss, where it stands as the stage begins.")]
    [SerializeField] private Transform centre;
    [Tooltip("Seconds after the stage begins before the first ring's warnings appear.")]
    [SerializeField, Min(0f)] private float startDelay;
    [Tooltip("The rings, outermost first.")]
    [SerializeField] private List<Ring> rings = new List<Ring>();
    [Tooltip("One eruption's blow: its Range is the radius of its circle (give it an Arc of 360), with its damage type, status (Roots) and pull.")]
    [SerializeField] private AttackDefinition eruption;
    [Tooltip("Damage of one eruption, as a strike's Damage (the boss's stage and rage add to it).")]
    [SerializeField, Min(0f)] private float damage = 50f;
    [Tooltip("Seconds each circle fills before it bursts: the time to get clear.")]
    [SerializeField, Min(0.2f)] private float warningSeconds = 1.5f;
    [Tooltip("How far the circles of a ring overlap, as a share of their radius: 0 sets them edge to edge, about 0.3 leaves no gap to stand in.")]
    [SerializeField, Range(0f, 0.9f)] private float overlap = 0.3f;
    [Tooltip("Left where each bursts (a prefab with a GroundHazard). Empty: none.")]
    [SerializeField] private GameObject hazardPrefab;
    [Tooltip("Seen flying from the boss to each circle as it fills, only a look (a clod of root and earth). Empty: none, they burst from below.")]
    [SerializeField] private GameObject thrownPrefab;
    [Tooltip("Plays as each ring's warnings appear.")]
    [SerializeField] private SoundDefinition ringSound;
    [Tooltip("Camera shake as each ring's warnings appear.")]
    [SerializeField, Min(0f)] private float shake = 0.35f;

    private static readonly List<Vector3> Points = new List<Vector3>();

    // The boss this listens for, read from the one placed as it starts listening (on a friend's game that one is then removed).
    private BossDefinition watching;
    private bool started;

    public IReadOnlyList<Ring> Rings => rings;

    private void OnEnable()
    {
        BossDefinition placed = BossEncounter.DefinitionOf(boss);
        if (placed != null)
        {
            watching = placed;
        }

        BossEncounter.AnyPhaseEntered += OnPhase;
    }

    private void OnDisable()
    {
        BossEncounter.AnyPhaseEntered -= OnPhase;
    }

    private void OnPhase(BossEncounter from, int phase)
    {
        if (started || watching == null || from == null || from.Definition != watching || phase < atPhase || eruption == null)
        {
            return;
        }

        started = true;
        // A friend's copy built mid-fight enters every stage it missed at once (BossEncounter.IsCatchingUp): the rings burst long ago in
        // the host's game, so none here, then or later at a friend who came after the stage began.
        if (!from.IsCatchingUp)
        {
            StartCoroutine(Burst(from));
        }
    }

    // The boss that entered the stage (the one placed, or on a friend's game the host's copy) makes every eruption.
    private IEnumerator Burst(BossEncounter from)
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (from == null || !from.TryGetComponent(out Combatant maker) || !from.TryGetComponent(out MonsterAI ai))
        {
            yield break;
        }

        Vector3 middle = centre != null ? centre.position : from.transform.position;
        MonsterAttack blow = Blow();
        Vector3 throwFrom = middle + Vector3.up * 2.2f;
        for (int i = 0; i < rings.Count; i++)
        {
            Ring ring = rings[i];
            if (ring == null)
            {
                continue;
            }

            if (i > 0 && ring.after > 0f)
            {
                yield return new WaitForSeconds(ring.after);
            }

            // Its fall ends the hold: no ring bursts over the body.
            if (from == null || !from.IsAwake)
            {
                yield break;
            }

            // Each ring turned half a step from the last, so their seams don't line up into a way through.
            RingPoints(middle, ring.radius, eruption.Range, overlap, (i & 1) * 0.5f, Points);
            foreach (Vector3 point in Points)
            {
                LobStrike.Throw(maker, blow, throwFrom, point, ai.TargetLayers);
            }

            AudioManager.Play(ringSound, middle);
            CameraShake.Add(shake);
        }

        Points.Clear();
    }

    // One eruption as a lob (LobStrike): its circle, damage, warning and what it leaves.
    private MonsterAttack Blow() => new MonsterAttack
    {
        attack = eruption,
        kind = MonsterAttackKind.Lob,
        damage = damage,
        lobSeconds = warningSeconds,
        hazardPrefab = hazardPrefab,
        projectilePrefab = thrownPrefab,
    };

    /// <summary>
    /// Where one ring's circles fall: evenly round <paramref name="middle"/> at <paramref name="radius"/>, as many as it takes for circles
    /// of <paramref name="eruptionRadius"/> overlapping by <paramref name="overlap"/> of it to leave no gap (at least three), turned by
    /// <paramref name="turn"/> of a step. Pure, for tests: written into <paramref name="into"/>.
    /// </summary>
    public static void RingPoints(Vector3 middle, float radius, float eruptionRadius, float overlap, float turn, List<Vector3> into)
    {
        into.Clear();
        float step = Mathf.Max(0.25f, 2f * eruptionRadius * (1f - Mathf.Clamp(overlap, 0f, 0.9f)));
        int count = Mathf.Max(3, Mathf.CeilToInt(2f * Mathf.PI * radius / step));
        for (int i = 0; i < count; i++)
        {
            float angle = (i + turn) / count * Mathf.PI * 2f;
            into.Add(middle + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Points.Clear();
    }
}
