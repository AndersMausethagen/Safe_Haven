using UnityEngine;

/// <summary>
/// Wood that has come away from a tree and lies around for a while: a branch knocked off, a cut section of trunk. Each one
/// is a free rigid body that tumbles, knocks on the ground as it rolls and clears itself away. The stump left behind is
/// made here too. A piece owns its mesh, as each one is built for it.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimberPiece : MonoBehaviour
{
    private const float BranchSeconds = 4f;
    // Long enough to see it roll free. Its wood is already in the bag, so it doesn't stay to clutter the ground.
    private const float LogSeconds = 3.5f;
    private const float FadeSeconds = 0.8f;
    private const float KnockImpulsePerKilogram = 0.5f;
    private const float KnockGap = 0.2f;
    private const float KnockRadius = 0.3f;
    private const float KilogramsPerCubicMetre = 500f;

    private Mesh mesh;
    private Rigidbody body;
    private Vector3 fullScale;
    private float fadesAt;
    private float nextKnock;
    private float size = 1f;

    public Collider Collider { get; private set; }

    /// <summary>A branch thrown off a crown.</summary>
    public static TimberPiece Branch(Material bark, Material face, Vector3 position, Vector3 velocity)
    {
        float length = Random.Range(1f, 2.6f);
        float radius = Random.Range(0.035f, 0.07f);
        Mesh branch = LogMesh.Build(radius, radius * 0.4f, length, 0f, Random.Range(0, 1000), 2);
        TimberPiece piece = Make("Branch", branch, new[] { bark, face }, position, Random.rotation, radius, length, BranchSeconds);
        piece.body.linearVelocity = velocity;
        piece.body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 8f);
        piece.size = 0.5f;
        return piece;
    }

    /// <summary>A section cut from a log. It takes over the mesh it is given.</summary>
    public static TimberPiece Log(Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation, float radius, float length, Vector3 velocity, Vector3 spin)
    {
        TimberPiece piece = Make("Log", mesh, materials, position, rotation, radius, length, LogSeconds);
        piece.body.linearVelocity = velocity;
        piece.body.angularVelocity = spin;
        piece.size = radius / KnockRadius;
        return piece;
    }

    /// <summary>What is left of a tree where it stood. It lasts as long as the tree takes to grow back.</summary>
    public static void Stump(TreeParts parts, Material bark, Material face)
    {
        // Hugs the trunk with a slight flare at the foot: wider than the tree read as a different, older stump.
        float radius = Mathf.Max(0.07f, parts.TrunkRadius * 1.05f);
        Mesh mesh = LogMesh.Build(radius * 1.12f, radius, FelledTree.CutHeight, 0f, Random.Range(0, 1000), 1);
        var stump = new GameObject("Stump");
        stump.transform.position = parts.Base;
        stump.AddComponent<MeshFilter>().sharedMesh = mesh;
        stump.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, face };
        stump.AddComponent<StumpLifetime>().Begin(mesh, TreeChoppingSettings.Current.TerrainTreeRegrowSeconds);
    }

    private static TimberPiece Make(string name, Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation, float radius, float length, float seconds)
    {
        var made = new GameObject(name);
        made.transform.SetPositionAndRotation(position, rotation);
        made.AddComponent<MeshFilter>().sharedMesh = mesh;
        made.AddComponent<MeshRenderer>().sharedMaterials = materials;

        var capsule = made.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = Mathf.Max(length, radius * 2f);
        capsule.center = Vector3.up * (length * 0.5f);
        capsule.sharedMaterial = TreeChoppingSettings.Current.TimberSurface;

        var body = made.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(1f, Mathf.PI * radius * radius * length * KilogramsPerCubicMetre);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.angularDamping = 1.2f;
        body.linearDamping = 0.1f;

        var piece = made.AddComponent<TimberPiece>();
        piece.mesh = mesh;
        piece.body = body;
        piece.Collider = capsule;
        piece.fullScale = made.transform.localScale;
        piece.fadesAt = Time.time + seconds;
        if (Player.Current != null && Player.Current.TryGetComponent(out CharacterController walker))
        {
            Physics.IgnoreCollision(capsule, walker);
        }

        return piece;
    }

    private void Update()
    {
        if (Time.time < fadesAt)
        {
            return;
        }

        float left = 1f - (Time.time - fadesAt) / FadeSeconds;
        if (left <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = fullScale * left;
    }

    private void OnCollisionEnter(Collision hit)
    {
        if (Time.time >= nextKnock && hit.impulse.magnitude >= body.mass * KnockImpulsePerKilogram)
        {
            nextKnock = Time.time + KnockGap;
            AudioManager.Play(TreeChoppingSettings.Current.LogKnockSound, transform.position, size);
        }
    }

    private void OnDestroy()
    {
        Destroy(mesh);
    }
}

/// <summary>Keeps a stump until its tree has grown back, then clears it and its mesh.</summary>
[DisallowMultipleComponent]
public sealed class StumpLifetime : MonoBehaviour
{
    private Mesh mesh;

    public void Begin(Mesh mesh, float seconds)
    {
        this.mesh = mesh;
        if (seconds > 0f)
        {
            Destroy(gameObject, seconds);
        }
    }

    private void OnDestroy()
    {
        Destroy(mesh);
    }
}
