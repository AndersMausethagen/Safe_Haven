using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A thing that breaks (2026-11-14): a crate, a barrel, a fence section. It takes blows like anything else (its Damageable,
/// on the Neutral side, so no creature ever picks it as a target but every blow, charge and boulder in reach lands on it),
/// and when its health is gone it bursts: its own meshes cut into pieces (BodyCutter) that fly from the blow and lie a
/// while, a puff of splinters, a crack, a little noise, and whatever its LootOnDeath drops. A heavy ground impact breaks
/// everything in its push reach outright (<see cref="SmashNear"/>).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class Breakable : MonoBehaviour
{
    private static readonly Collider[] Overlaps = new Collider[32];
    private static readonly List<Breakable> Found = new List<Breakable>();

    // Entering Play doesn't reload scripts in this project: the last session's breakables go with it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Found.Clear();

    [Tooltip("Cells its meshes are cut into: more for more, smaller pieces.")]
    [SerializeField] private Vector3Int pieces = new Vector3Int(2, 2, 2);
    [Tooltip("Thrown where it breaks (a pooled effect: splinters, dust).")]
    [SerializeField] private GameObject breakEffect;
    [SerializeField] private SoundDefinition breakSound;
    [Tooltip("Metres the noise of it breaking carries.")]
    [SerializeField, Min(0f)] private float noise = 8f;
    [Tooltip("How hard the pieces fly, metres per second.")]
    [SerializeField, Min(0f)] private float fling = 3.5f;
    [Tooltip("Seconds the pieces lie before they go.")]
    [SerializeField, Min(1f)] private float pieceSeconds = 10f;

    private Damageable health;
    private bool broken;

    public bool IsBroken => broken;

    private void Awake()
    {
        health = GetComponent<Damageable>();
    }

    private void OnEnable() => health.Died += OnBroken;

    private void OnDisable() => health.Died -= OnBroken;

    /// <summary>Breaks every breakable within <paramref name="radius"/> of <paramref name="at"/> (a giant's stomp, a boulder).</summary>
    public static void SmashNear(Vector3 at, float radius, Combatant by)
    {
        Found.Clear();
        int count = Physics.OverlapSphereNonAlloc(at, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Breakable thing = Overlaps[i].GetComponentInParent<Breakable>();
            if (thing != null && !thing.broken && !Found.Contains(thing))
            {
                Found.Add(thing);
            }
        }

        foreach (Breakable thing in Found)
        {
            thing.health.Finish(by);
        }
    }

    private void OnBroken(Damageable victim, DamageInfo blow)
    {
        if (broken)
        {
            return;
        }

        broken = true;
        Vector3 blowWay = blow.Direction.sqrMagnitude > 1e-4f ? blow.Direction.normalized : Random.onUnitSphere;
        blowWay.y = Mathf.Abs(blowWay.y) * 0.3f;
        List<MeshRenderer> shown = Shown();
        var readable = shown.FindAll(part => part.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh.isReadable);
        Bounds whole = shown.Count > 0 ? shown[0].bounds : new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        foreach (MeshRenderer part in shown)
        {
            whole.Encapsulate(part.bounds);
        }

        foreach (BodyCutter.Chunk chunk in BodyCutter.Break(readable, pieces))
        {
            Fly(chunk, whole.center, blowWay);
        }

        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            part.enabled = false;
        }

        foreach (Collider part in GetComponentsInChildren<Collider>(true))
        {
            part.enabled = false;
        }

        if (TryGetComponent(out NavMeshObstacle obstacle))
        {
            obstacle.enabled = false;
        }

        if (breakEffect != null)
        {
            PrefabPool.Spawn(breakEffect, whole.center, Quaternion.identity);
        }

        AudioManager.Play(breakSound, whole.center);
        if (noise > 0f)
        {
            WorldNoise.Emit(whole.center, noise);
        }

        Destroy(gameObject, 2f);
    }

    // What is drawn now: the nearest level of detail only.
    private List<MeshRenderer> Shown()
    {
        var shown = new List<MeshRenderer>();
        if (TryGetComponent(out LODGroup lods) || (lods = GetComponentInChildren<LODGroup>()) != null)
        {
            LOD[] levels = lods.GetLODs();
            if (levels.Length > 0)
            {
                foreach (Renderer part in levels[0].renderers)
                {
                    if (part is MeshRenderer mesh && mesh.enabled)
                    {
                        shown.Add(mesh);
                    }
                }

                return shown;
            }
        }

        foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>())
        {
            if (part.enabled)
            {
                shown.Add(part);
            }
        }

        return shown;
    }

    // One piece, out from the middle and along the blow, tumbling; it lies a while, then goes.
    private void Fly(BodyCutter.Chunk chunk, Vector3 middle, Vector3 blowWay)
    {
        var piece = new GameObject(name + " Piece", typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.position = chunk.Centre;
        piece.GetComponent<MeshFilter>().sharedMesh = chunk.Mesh;
        piece.GetComponent<MeshRenderer>().sharedMaterials = chunk.Materials;
        var box = piece.AddComponent<BoxCollider>();
        box.center = chunk.Mesh.bounds.center;
        box.size = Vector3.Max(chunk.Mesh.bounds.size, Vector3.one * 0.05f);
        var body = piece.AddComponent<Rigidbody>();
        body.mass = Mathf.Clamp(box.size.x * box.size.y * box.size.z * 300f, 0.3f, 15f);
        Vector3 outward = chunk.Centre - middle;
        outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.onUnitSphere;
        body.linearVelocity = (outward * 0.6f + blowWay * 0.8f + Vector3.up * 0.7f) * fling * Random.Range(0.7f, 1.2f);
        body.angularVelocity = Random.insideUnitSphere * 8f;
        Destroy(piece, pieceSeconds + Random.Range(0f, 3f));
        Destroy(chunk.Mesh, pieceSeconds + 3.5f);
    }
}
