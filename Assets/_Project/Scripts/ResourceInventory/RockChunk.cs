using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A piece of a mined node (GatheringNodeDefinition › Break Apart): the node's own model, cracked ahead of time (NodeFracture), thrown
/// out as a free rigid body from exactly where that part of the rock was. It knocks on the ground as it rolls and clears itself away.
/// What it stands for is already in the bag, so it doesn't stay to clutter the ground or get in anyone's way.
/// </summary>
[DisallowMultipleComponent]
public sealed class RockChunk : MonoBehaviour
{
    private const float FadeSeconds = 0.6f;
    private const float KnockGap = 0.18f;
    private const float KnockImpulsePerKilogram = 0.6f;
    private const float KilogramsPerCubicMetre = 1500f;
    // Layer 2, Ignore Raycast: it still lands on the ground, but footprints, arrows and the cursor look straight past it.
    private const int ChunkLayer = 2;

    private Rigidbody body;
    private SoundDefinition knock;
    private Vector3 fullScale;
    private Vector3 middle;
    private Vector3 restsAt;
    private float fadesAt;
    private float nextKnock;
    private float size;
    private bool fading;

    /// <summary>Breaks a model into its pieces where it stands, thrown out from the middle of <paramref name="whole"/> and a little
    /// away from whoever stands at <paramref name="from"/>. A part that isn't cracked flies off whole. Returns how many there are.</summary>
    public static int Burst(IEnumerable<MeshRenderer> parts, Bounds whole, NodeFracture fracture, HarvestFeedbackProfile feel, Vector3 from)
    {
        Vector3 away = whole.center - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;
        float reach = Mathf.Max(whole.size.magnitude, 0.01f);

        var made = new List<Collider>();
        foreach (MeshRenderer part in parts)
        {
            Mesh source = part.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (source == null)
            {
                continue;
            }

            Mesh[] pieces = fracture != null ? fracture.PiecesOf(source) : null;
            foreach (Mesh piece in pieces ?? new[] { source })
            {
                Transform model = part.transform;
                Vector3 centre = model.TransformPoint(piece.bounds.center);
                Vector3 outward = centre - whole.center;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                float share = Mathf.Clamp01(Vector3.Scale(piece.bounds.size, model.lossyScale).magnitude / reach);
                // The small bits fly further than the big halves.
                float speed = feel.BurstSpeed * Random.Range(0.75f, 1.1f) * Mathf.Lerp(1.3f, 0.7f, share);
                Vector3 velocity = (outward + away * 0.35f + Vector3.up * Random.Range(0.45f, 1f)).normalized * speed;
                made.Add(Make(piece, part, velocity, share, feel));
            }
        }

        // They begin packed together as the rock they were: left to collide, they would burst apart far too hard.
        for (int a = 0; a < made.Count; a++)
        {
            for (int b = a + 1; b < made.Count; b++)
            {
                Physics.IgnoreCollision(made[a], made[b]);
            }
        }

        return made.Count;
    }

    private static Collider Make(Mesh mesh, MeshRenderer from, Vector3 velocity, float share, HarvestFeedbackProfile feel)
    {
        Transform model = from.transform;
        var made = new GameObject("Rock Chunk") { layer = ChunkLayer };
        made.transform.SetPositionAndRotation(model.position, model.rotation);
        made.transform.localScale = model.lossyScale;
        made.AddComponent<MeshFilter>().sharedMesh = mesh;
        var look = made.AddComponent<MeshRenderer>();
        Material surface = from.sharedMaterial;
        look.sharedMaterials = mesh.subMeshCount > 1
            ? new[] { surface, feel.PieceInterior != null ? feel.PieceInterior : surface }
            : new[] { surface };

        var box = made.AddComponent<BoxCollider>();
        box.center = mesh.bounds.center;
        box.size = mesh.bounds.size;

        Vector3 worldSize = Vector3.Scale(mesh.bounds.size, model.lossyScale);
        var body = made.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(1f, Mathf.Abs(worldSize.x * worldSize.y * worldSize.z) * KilogramsPerCubicMetre);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.angularDamping = 0.8f;
        // A rock sunk in the ground rises out of it, rather than being fired out.
        body.maxDepenetrationVelocity = 1f;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.onUnitSphere * Random.Range(2f, 8f);

        var chunk = made.AddComponent<RockChunk>();
        chunk.body = body;
        chunk.knock = feel.PieceKnock;
        chunk.fullScale = model.lossyScale;
        chunk.middle = mesh.bounds.center;
        chunk.fadesAt = Time.time + feel.PieceSeconds * Random.Range(0.85f, 1.15f);
        chunk.size = Mathf.Lerp(0.6f, 1.4f, share * 2f);
        if (Player.Current != null && Player.Current.TryGetComponent(out CharacterController walker))
        {
            Physics.IgnoreCollision(box, walker);
        }

        return box;
    }

    private void Update()
    {
        if (Time.time < fadesAt)
        {
            return;
        }

        // A piece's origin is the whole model's, so it shrinks on the spot only if it is held there.
        if (!fading)
        {
            fading = true;
            body.isKinematic = true;
            restsAt = transform.TransformPoint(middle);
        }

        float left = 1f - (Time.time - fadesAt) / FadeSeconds;
        if (left <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = fullScale * left;
        transform.position = restsAt - transform.rotation * Vector3.Scale(middle, transform.localScale);
    }

    private void OnCollisionEnter(Collision hit)
    {
        if (Time.time >= nextKnock && hit.impulse.magnitude >= body.mass * KnockImpulsePerKilogram)
        {
            nextKnock = Time.time + KnockGap;
            AudioManager.Play(knock, transform.position, size);
        }
    }
}
