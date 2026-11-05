using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A tree coming down, as a real rigid body. A copy of the tree stands where it stood on a hinge at the cut, leaning a
/// degree or two: gravity does the rest, so it starts slow, creaks and goes over faster and faster, as a tree does. Part
/// way down the hinge gives and the butt is free to kick and slide. When it lands the logs of the felling are given, the
/// crown throws off leaves and branches, and once it lies still FelledLog takes over.
/// <para>
/// Only the trunk is a collider: a crown gives way, so its branches simply sink into the ground. The trunk touches nothing
/// but the ground (see TreeGroundPatch), so it can't hang in its neighbours and always ends up where it can be worked; the
/// direction it falls keeps it off walls and rocks.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(FelledLog))]
public sealed class FelledTree : MonoBehaviour
{
    /// <summary>Height of the cut: what stays behind as the stump.</summary>
    public const float CutHeight = 0.45f;

    private const float StartingLean = 1.5f;
    private const float StartingSpin = 0.06f;
    private const float HingeGivesAtLean = 62f;
    private const float ButtKick = 1.2f;
    private const float RushSpin = 0.3f;
    private const float LandsFromLean = 20f;
    private const float FallTimeout = 10f;
    private const float SettleTimeout = 3f;
    private const float RestSpeed = 0.15f;
    private const float RestSeconds = 0.4f;
    private const float LandedLinearDamping = 1.5f;
    private const float LandedAngularDamping = 8f;
    private const float KilogramsPerCubicMetre = 500f;
    private const int MaxBranches = 8;

    private enum Stage
    {
        Falling,
        Settling,
        Lying,
    }

    private readonly List<ItemAmount> logs = new List<ItemAmount>();
    private Rigidbody body;
    private CapsuleCollider trunk;
    private FelledLog log;
    private TreeImpacts impacts;
    private ResourceSource source;
    private HingeJoint hinge;
    private GameObject ground;
    private TreeParts parts;
    private GameObject feller;
    private Vector3 fallDirection;
    private Stage stage;
    private float startedAt;
    private float landedAt;
    private float restingFor;
    private float size;
    private bool rushing;

    /// <summary>The direction it fell.</summary>
    public Vector3 FallDirection => fallDirection;

    /// <summary>True until it has hit the ground.</summary>
    public bool IsFalling => stage == Stage.Falling;

    /// <summary>True once it has stopped moving and can be worked.</summary>
    public bool IsLying => stage == Stage.Lying;

    /// <summary>Brings a tree down, away from whoever felled it. Returns null when no fallen tree is set up, so nothing can fall.</summary>
    public static FelledTree Spawn(TreeParts parts, float size, GameObject feller, List<ItemAmount> logs)
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        if (rules.FelledTreePrefab == null || parts.Parts.Count == 0)
        {
            return null;
        }

        Vector3 away = feller != null ? parts.Base - feller.transform.position : Vector3.forward;
        Vector3 direction = FellDirection.Choose(away, rules.FallSpreadDegrees, 5, way => Clearance(parts, way));
        var tree = Instantiate(rules.FelledTreePrefab, parts.Base, Quaternion.identity).GetComponent<FelledTree>();
        tree.name = "Felled Tree";
        tree.Begin(parts, size, feller, logs, direction);
        return tree;
    }

    // How far the tree can lay down this way before something it must not pass through: walls and rocks, or rising ground.
    private static float Clearance(TreeParts parts, Vector3 direction)
    {
        float reach = parts.Height;
        float clear = reach;
        foreach (RaycastHit hit in Physics.RaycastAll(parts.Base + Vector3.up * 2f, direction, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<ChoppableTree>() == null && !NodeStreaming.IsDormantTree(hit.collider) && hit.collider.attachedRigidbody == null)
            {
                clear = Mathf.Min(clear, hit.distance);
            }
        }

        return clear / reach;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        trunk = GetComponent<CapsuleCollider>();
        log = GetComponent<FelledLog>();
        source = GetComponent<ResourceSource>();
    }

    private void Begin(TreeParts parts, float size, GameObject feller, List<ItemAmount> logs, Vector3 direction)
    {
        this.parts = parts;
        this.size = size;
        this.feller = feller;
        this.logs.AddRange(logs);
        fallDirection = direction;
        startedAt = Time.time;

        log.Prepare(parts, size, parts.BuildModel(transform));
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        Material bark = rules.LogBark != null ? rules.LogBark : parts.Bark;
        TimberPiece.Stump(parts, bark, rules.CutFace);

        float length = Mathf.Max(1f, parts.TrunkLength - CutHeight);
        trunk.direction = 1;
        trunk.radius = parts.TrunkRadius;
        trunk.height = length;
        trunk.center = Vector3.up * (CutHeight + length * 0.5f);
        trunk.sharedMaterial = rules.TimberSurface;

        ground = TreeGroundPatch.Build(parts.Base, direction, parts.Height + 4f, parts.Height * 0.8f + 8f);
        if (ground != null)
        {
            body.excludeLayers = ~(1 << ground.layer);
        }

        Vector3 axis = Vector3.Cross(Vector3.up, direction).normalized;
        transform.rotation = Quaternion.AngleAxis(StartingLean, axis);
        body.isKinematic = false;
        body.mass = Mathf.PI * parts.TrunkRadius * parts.TrunkRadius * parts.Height * KilogramsPerCubicMetre;
        body.centerOfMass = Vector3.up * (parts.Height * 0.42f);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = 0.02f;
        body.angularDamping = 0.1f;
        body.maxDepenetrationVelocity = 3f;
        body.angularVelocity = axis * StartingSpin;

        hinge = gameObject.AddComponent<HingeJoint>();
        hinge.anchor = Vector3.up * CutHeight;
        hinge.axis = transform.InverseTransformDirection(axis);

        AudioManager.Play(rules.CreakSound, parts.Base + Vector3.up * CutHeight, size);
        if (TryGetComponent(out impacts))
        {
            impacts.Begin(parts, size, feller);
        }
    }

    private float Lean => Vector3.Angle(Vector3.up, transform.up);

    private void FixedUpdate()
    {
        if (stage == Stage.Falling)
        {
            Fall();
        }
        else if (stage == Stage.Settling)
        {
            Settle();
        }
    }

    private void Fall()
    {
        if (hinge != null && Lean >= HingeGivesAtLean)
        {
            Destroy(hinge);
            hinge = null;
            body.AddForce(-fallDirection * ButtKick, ForceMode.VelocityChange);
        }

        if (!rushing && body.angularVelocity.sqrMagnitude >= RushSpin * RushSpin)
        {
            rushing = true;
            AudioManager.Play(TreeChoppingSettings.Current.RushSound, transform.TransformPoint(parts.Crown.center), size);
        }

        if (Time.time - startedAt >= FallTimeout)
        {
            Land(transform.position);
        }
    }

    private void OnCollisionEnter(Collision hit)
    {
        if (stage == Stage.Falling && Lean >= LandsFromLean)
        {
            Land(hit.GetContact(0).point);
        }
    }

    private void Land(Vector3 point)
    {
        stage = Stage.Settling;
        landedAt = Time.time;
        if (impacts != null)
        {
            impacts.End();
        }

        restingFor = 0f;
        // The collider is only a trunk, which would roll like a rolling pin: the crown it doesn't have stops that.
        body.linearDamping = LandedLinearDamping;
        body.angularDamping = LandedAngularDamping;
        if (hinge != null)
        {
            Destroy(hinge);
            hinge = null;
        }

        source.GrantItems(feller, logs);

        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        Vector3 crown = transform.TransformPoint(parts.Crown.center);
        AudioManager.Play(rules.LandingSound, point, size);
        Player player = Player.Current;
        if (player != null)
        {
            float felt = 1f - Mathf.Clamp01(Vector3.Distance(player.transform.position, crown) / rules.LandingFeltDistance);
            CameraShake.Add(rules.LandingShake * felt);
            if (player.Noise != null)
            {
                player.Noise.Make(crown, rules.LandingNoiseRadius);
            }
        }
        else
        {
            WorldNoise.Emit(crown, rules.LandingNoiseRadius);
        }

        if (parts.HasFoliage)
        {
            TreeEffects.Play(parts.LeafFall != null ? parts.LeafFall : rules.LeafFall, crown, transform.rotation, parts.Crown.size, parts.LeafTint, Mathf.RoundToInt(60f * size));
        }

        float length = trunk.height;
        Vector3 middle = transform.TransformPoint(trunk.center);
        TreeEffects.Play(rules.LandingDust, middle, transform.rotation, new Vector3(1.5f, length, 1.5f), rules.DustTint, Mathf.RoundToInt(6f + 2f * length));
        ThrowBranches();
    }

    // The crown's branches break off where they hit, and spring out sideways and up from the blow.
    private void ThrowBranches()
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        Material bark = rules.LogBark != null ? rules.LogBark : parts.Bark;
        int count = Mathf.Min(MaxBranches, Mathf.RoundToInt(2f + size * 2f));
        Bounds crown = parts.Crown;
        for (int i = 0; i < count; i++)
        {
            Vector3 along = new Vector3(0f, Random.Range(crown.min.y, crown.max.y), 0f);
            Vector3 aside = Random.onUnitSphere * 0.8f;
            aside.y = Mathf.Abs(aside.y) + 0.8f;
            TimberPiece.Branch(bark, rules.CutFace, transform.TransformPoint(along) + Vector3.up * 0.8f, aside * Random.Range(2f, 5f));
        }

        AudioManager.Play(rules.BranchSnapSound, transform.TransformPoint(crown.center));
    }

    private void Settle()
    {
        bool still = body.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed && body.angularVelocity.sqrMagnitude < RestSpeed * RestSpeed;
        restingFor = still ? restingFor + Time.fixedDeltaTime : 0f;
        if (restingFor >= RestSeconds || Time.time - landedAt >= SettleTimeout)
        {
            stage = Stage.Lying;
            body.isKinematic = true;
            RemoveGround();
            log.Ready();
        }
    }

    private void RemoveGround()
    {
        if (ground != null)
        {
            TreeGroundPatch.Remove(ground);
            ground = null;
        }
    }

    private void OnDestroy() => RemoveGround();
}
