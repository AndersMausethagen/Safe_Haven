using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A boss arena's ground that gives way as the fight goes on (Brukam's Bridge: at 66% the spans fall in a fixed order, each one
/// telegraphed with a shake, dust and cracking before it goes; Docs/BossEncounterResearch_2026-11-17.md §2.3, "as the bridge runs
/// out"). It starts when its boss enters the phase. A piece with the boss on it waits until he steps off, so he never drops with it.
/// A fallen piece drops away out of sight with its colliders off, and a carving NavMeshObstacle closes the gap to creatures.
/// <para>
/// Where it belongs: on an object in the arena scene, with the pieces (each a child holding its model and colliders) in falling order.
/// A friend's game (co-op) runs its own, from its copy of the boss, whose phases follow the host's health: the boss is known by its
/// BossDefinition (BossEncounter.AnyPhaseEntered), as the one placed here is removed on a friend's game and the copy comes later.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "each game drops its own pieces from its copy of the boss's phases")]
public sealed class CollapsingGround : MonoBehaviour
{
    [Serializable]
    public sealed class Piece
    {
        [Tooltip("The piece that falls: its model and colliders.")]
        public Transform piece;
        [Tooltip("Seconds after the previous piece (or the phase, for the first) before this one starts to crack.")]
        [Min(0f)] public float after = 4f;
    }

    [Tooltip("The boss whose phase starts the collapse. Known by its Boss Definition: on a friend's game (co-op) the host's copy starts it too.")]
    [SerializeField] private BossEncounter boss;
    [Tooltip("The phase that starts it (1 is the first of the boss's Phases).")]
    [SerializeField, Min(1)] private int atPhase = 1;
    [Tooltip("The pieces, in the order they fall.")]
    [SerializeField] private List<Piece> pieces = new List<Piece>();
    [Tooltip("Seconds a piece shakes and cracks before it falls: the warning to get off it.")]
    [SerializeField, Min(0.5f)] private float warning = 2f;
    [Tooltip("Metres a fallen piece drops before it is hidden.")]
    [SerializeField, Min(1f)] private float dropDistance = 30f;
    [Tooltip("Seconds the drop takes.")]
    [SerializeField, Min(0.1f)] private float dropSeconds = 1.6f;
    [SerializeField] private SoundDefinition crackSound;
    [SerializeField] private SoundDefinition fallSound;
    [Tooltip("Dust spawned at a piece as it cracks and as it falls (a particle prefab). Empty: none.")]
    [SerializeField] private GameObject dust;

    private bool started;
    // The boss this listens for, read from the one placed as it starts listening (on a friend's game that one is then removed).
    private BossDefinition watching;

    /// <summary>How many pieces have fallen.</summary>
    public int Fallen { get; private set; }

    public IReadOnlyList<Piece> Pieces => pieces;

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
        if (!started && watching != null && from != null && from.Definition == watching && phase >= atPhase)
        {
            started = true;
            StartCoroutine(Collapse(from));
        }
    }

    // The boss that entered the phase (the one placed, or on a friend's game the host's copy) is the one it never drops.
    private IEnumerator Collapse(BossEncounter from)
    {
        foreach (Piece each in pieces)
        {
            if (each == null || each.piece == null)
            {
                continue;
            }

            yield return new WaitForSeconds(each.after);
            // Never under the boss: it waits until he is off it (or down).
            while (from != null && from.IsAwake && Under(each.piece, from.transform.position))
            {
                yield return new WaitForSeconds(0.25f);
            }

            yield return Crack(each.piece);
            Fall(each.piece);
        }
    }

    private IEnumerator Crack(Transform piece)
    {
        AudioManager.Play(crackSound, piece.position);
        Burst(BoundsOf(piece).center);
        CameraShake.Add(0.3f);
        Vector3 rest = piece.localPosition;
        float until = Time.time + warning;
        while (Time.time < until)
        {
            float strength = 0.04f + 0.06f * (1f - (until - Time.time) / warning);
            piece.localPosition = rest + new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.5f, 0.2f), UnityEngine.Random.Range(-1f, 1f)) * strength;
            yield return null;
        }

        piece.localPosition = rest;
    }

    private void Fall(Transform piece)
    {
        Fallen++;
        Bounds bounds = BoundsOf(piece);
        foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        // The gap, closed to creatures where the piece stood: carved out of the navmesh.
        var gap = new GameObject(piece.name + " (gap)");
        gap.transform.SetParent(transform, false);
        gap.transform.position = bounds.center;
        NavMeshObstacle obstacle = gap.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = bounds.size + Vector3.up * 2f;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        AudioManager.Play(fallSound, piece.position);
        // Where it stood, measured before its colliders went off: a disabled collider's bounds are empty at the world's origin.
        Burst(bounds.center);
        CameraShake.Add(0.7f);
        StartCoroutine(Drop(piece));
    }

    private IEnumerator Drop(Transform piece)
    {
        Vector3 from = piece.position;
        Quaternion turn = Quaternion.Euler(UnityEngine.Random.Range(-25f, 25f), 0f, UnityEngine.Random.Range(-25f, 25f));
        Quaternion rest = piece.rotation;
        for (float t = 0f; t < dropSeconds; t += Time.deltaTime)
        {
            float k = t / dropSeconds;
            piece.SetPositionAndRotation(from + Vector3.down * (dropDistance * k * k), Quaternion.Slerp(rest, turn * rest, k));
            yield return null;
        }

        piece.gameObject.SetActive(false);
    }

    private void Burst(Vector3 at)
    {
        if (dust != null)
        {
            Destroy(Instantiate(dust, at, Quaternion.identity), 8f);
        }
    }

    private static bool Under(Transform piece, Vector3 point)
    {
        Bounds bounds = BoundsOf(piece);
        bounds.Expand(new Vector3(1f, 6f, 1f));
        return bounds.Contains(point);
    }

    private static Bounds BoundsOf(Transform piece)
    {
        var bounds = new Bounds(piece.position, Vector3.zero);
        bool any = false;
        foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
        {
            if (!any)
            {
                bounds = collider.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!any)
        {
            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return bounds;
    }
}
