using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The work left in a fallen tree. It lies whole, crown and all, until the player limbs it: the branches come away for
/// sticks and a clean trunk is left. Then the trunk is cut into sections from the butt up, each one a little run of the
/// swing minigame through the Felled Tree node, and each section gives logs and rolls free as timber of its own. Only the
/// next section can be reached, so the work moves along the log. A tree nobody finishes clears itself away in time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceNode), typeof(ResourceSource))]
[InAFriendsHaven(HavenUse.Anyone, "their own yield; the work on the host's land goes to the host (CoopLands)")]
public sealed class FelledLog : MonoBehaviour, IHarvestBody
{
    private const float TipRadiusShare = 0.6f;
    private const float ButtRadiusScale = 1.1f;
    private const float MinButtRadius = 0.27f;
    private const float MinReachRadius = 0.35f;
    private const int LimbingBranches = 5;

    private struct Section
    {
        public GameObject Body;
        public Mesh Mesh;
        public float Bottom;
        public float Length;
        public float RadiusBottom;
        public float RadiusTop;
    }

    private readonly List<Section> sections = new List<Section>();
    private readonly List<ItemAmount> yield = new List<ItemAmount>();
    private Interactable interactable;
    private ResourceNode node;
    private ResourceSource source;
    private CapsuleCollider capsule;
    private Transform model;
    private TreeParts parts;
    private float size;
    private float goneAt = float.PositiveInfinity;

    /// <summary>True once the branches have been cleared and the trunk can be cut up.</summary>
    public bool IsLimbed { get; private set; }

    public int SectionsLeft => sections.Count;

    public float HarvestSize => size;

    private Material Bark => TreeChoppingSettings.Current.LogBark != null
        ? TreeChoppingSettings.Current.LogBark
        : parts.Bark;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        node = GetComponent<ResourceNode>();
        source = GetComponent<ResourceSource>();
        // Its limbing and sections are pieces of a tree already counted at its fall.
        if (source != null)
        {
            source.CountsAsNode = false;
        }
        capsule = GetComponent<CapsuleCollider>();
        interactable.enabled = false;
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnWorked);
        node.Swung += OnSwung;
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnWorked);
        node.Swung -= OnSwung;
    }

    /// <summary>Takes in the tree it will become. Called while it is still standing.</summary>
    public void Prepare(TreeParts parts, float size, Transform model)
    {
        this.parts = parts;
        this.size = size;
        this.model = model;
    }

    /// <summary>The tree has stopped moving: the player can get to work.</summary>
    public void Ready()
    {
        interactable.enabled = true;
        goneAt = Time.time + TreeChoppingSettings.Current.FallenTreeSeconds;
    }

    private void Update()
    {
        if (Time.time >= goneAt && !interactable.IsInUse)
        {
            Destroy(gameObject);
        }
    }

    private void OnWorked()
    {
        goneAt = Time.time + TreeChoppingSettings.Current.FallenTreeSeconds;
        if (IsLimbed)
        {
            CutSection(interactable.Interactor);
        }
        else
        {
            Limb(interactable.Interactor);
        }
    }

    // Each blow on the crown knocks more of it down.
    private void OnSwung(SwingResult result, Vector3 from)
    {
        if (!IsLimbed && parts.HasFoliage)
        {
            TreeEffects.Play(TreeChoppingSettings.Current.LeafFall, transform.TransformPoint(parts.Crown.center), transform.rotation,
                parts.Crown.size, parts.LeafTint, result == SwingResult.Glancing ? 6 : 16);
        }
    }

    private void Limb(GameObject user)
    {
        IsLimbed = true;
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        int sticks = rules.LimbingItems(size);
        if (sticks > 0)
        {
            yield.Clear();
            yield.Add(new ItemAmount(rules.LimbingItem, sticks));
            source.GrantItems(user, yield);
        }

        Vector3 crown = transform.TransformPoint(parts.Crown.center);
        AudioManager.Play(rules.LimbingSound, crown, size);
        if (parts.HasFoliage)
        {
            TreeEffects.Play(rules.LeafFall, crown, transform.rotation, parts.Crown.size, parts.LeafTint, Mathf.RoundToInt(80f * size));
        }

        float length = capsule.height;
        TreeEffects.Play(rules.LandingDust, transform.TransformPoint(capsule.center), transform.rotation, new Vector3(1.5f, length, 1.5f), rules.DustTint, 14);
        for (int i = 0; i < LimbingBranches; i++)
        {
            Vector3 along = Vector3.up * (FelledTree.CutHeight + length * Random.value);
            Vector3 aside = Random.onUnitSphere;
            aside.y = Mathf.Abs(aside.y) + 0.6f;
            TimberPiece.Branch(Bark, rules.CutFace, transform.TransformPoint(along) + Vector3.up * 0.5f, aside * Random.Range(1.5f, 3.5f));
        }

        Destroy(model.gameObject);
        BuildTrunk(rules.Sections(size));
        ReachNextSection();
    }

    private void BuildTrunk(int count)
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        float bottom = capsule.center.y - capsule.height * 0.5f;
        float length = capsule.height / count;
        float butt = Mathf.Max(MinButtRadius, parts.TrunkRadius * ButtRadiusScale);
        float tip = butt * TipRadiusShare;
        int seed = Random.Range(0, 1000);
        var materials = new[] { Bark, rules.CutFace };
        for (int i = 0; i < count; i++)
        {
            float start = butt + (tip - butt) * i / count;
            float end = butt + (tip - butt) * (i + 1) / count;
            Mesh mesh = LogMesh.Build(start, end, length, i * length, seed);
            var piece = new GameObject("Section " + (i + 1));
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = Vector3.up * (bottom + i * length);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials;
            sections.Add(new Section { Body = piece, Mesh = mesh, Bottom = bottom + i * length, Length = length, RadiusBottom = start, RadiusTop = end });
        }
    }

    // Only the next cut can be reached, so the player works along the log instead of hacking at it from anywhere.
    private void ReachNextSection()
    {
        Section next = sections[0];
        capsule.radius = Mathf.Max(MinReachRadius, next.RadiusBottom);
        capsule.height = Mathf.Max(next.Length, capsule.radius * 2f);
        capsule.center = Vector3.up * (next.Bottom + next.Length * 0.5f);
    }

    private void CutSection(GameObject user)
    {
        Section section = sections[0];
        sections.RemoveAt(0);
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        int axe = Mathf.Max(1, ItemInventory.GatheringTier(user, ToolType.Axe));
        ChoppableTree.Roll(rules.SectionLogs(axe) + HavenReadiness.ExtraLogs, axe, size, () => Random.value, yield, parts?.Wood);
        source.GrantItems(user, yield);

        Vector3 roll = Vector3.Cross(transform.up, Vector3.up);
        roll = roll.sqrMagnitude > 0.0001f ? roll.normalized : transform.right;
        if (Random.value < 0.5f)
        {
            roll = -roll;
        }

        Vector3 push = -transform.up * 0.7f + roll * 1.4f + Vector3.up * 0.8f;
        TimberPiece piece = TimberPiece.Log(section.Mesh, new[] { Bark, rules.CutFace }, section.Body.transform.position, section.Body.transform.rotation,
            section.RadiusBottom, section.Length, push, transform.up * Random.Range(-1f, 1f) + Vector3.Cross(Vector3.up, roll) * 3f);
        Physics.IgnoreCollision(piece.Collider, capsule);

        // The timber is a new object: the old section may have an outline attached to it, and takes that away with it.
        Destroy(section.Body);
        if (sections.Count > 0)
        {
            ReachNextSection();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Chips fly where the axe bites: at the end of the next section, or at the nearest point of a tree still whole.</summary>
    public Vector3 HitPoint(Vector3 from)
    {
        float along;
        float radius;
        if (sections.Count > 0)
        {
            along = sections[0].Bottom + sections[0].Length;
            radius = sections[0].RadiusTop;
        }
        else
        {
            along = transform.InverseTransformPoint(from).y;
            along = Mathf.Clamp(along, capsule.center.y - capsule.height * 0.5f, capsule.center.y + capsule.height * 0.5f);
            radius = capsule.radius;
        }

        Vector3 point = transform.TransformPoint(Vector3.up * along);
        Vector3 toward = from - point;
        toward.y = 0f;
        return point + (toward.sqrMagnitude > 0.0001f ? toward.normalized * radius : Vector3.up * radius);
    }

    private void OnDestroy()
    {
        foreach (Section section in sections)
        {
            Destroy(section.Mesh);
        }
    }
}
