using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A gathering node in the world, such as a tree, a rock or an ore vein, configured by a GatheringNodeDefinition. Before
/// the player starts it checks for the right tool (a Tier II node needs a Tier II tool of its kind), and once gathered it
/// disappears and grows back after the node's respawn time. The yield itself is granted by the ResourceSource beside it,
/// and what a node that breaks apart gives on top (its definition's Burst Yields) along with it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable), typeof(ResourceSource))]
[InAFriendsHaven(HavenUse.Anyone, "their own yield; the work on the host's land goes to the host (CoopLands)")]
public sealed class ResourceNode : MonoBehaviour, IInteractionCondition, ISaveable, ISharedState, IInteractionName
{
    [System.Serializable]
    private struct SavedNode
    {
        public bool depleted;
        public float respawnRemaining;
        // When it was saved, in this run of the game (DayNightCycle.Run), so the time away on a raid counts.
        public string run;
        public double savedAt;
        // The home day it grows back on (Gathering Node › Regrows At Home After Days), 0 when it doesn't count days.
        public int regrowOnDay;
    }

    [SerializeField] private GatheringNodeDefinition node;
    [Tooltip("Keep this prefab's own model, size and facing instead of the definition's variants: a rock painted on a terrain " +
             "must look exactly like the painted one it stands in for.")]
    [SerializeField] private bool keepOwnModel;

    public bool KeepsOwnModel => keepOwnModel;

    private Interactable interactable;
    private ResourceNodeFeedback feedback;
    private Renderer[] renderers;
    private Collider[] colliders;
    private float respawnAt;
    // The home day (SettlementState.DaysPassed) it grows back on, 0 when it doesn't count days.
    private int regrowOnDay;
    private float hideAt = -1f;
    private Coroutine countdown;

    public GatheringNodeDefinition Definition => node;
    public string InteractionName => node != null ? node.DisplayName : null;
    public bool IsDepleted { get; private set; }

    /// <summary>A minigame swing landed on this node: its result and where the swinger stood.</summary>
    public event System.Action<SwingResult, Vector3> Swung;

    /// <summary>The node was gathered (any way, also by hand).</summary>
    public event System.Action Harvested;

    /// <summary>The node grew back after being gathered.</summary>
    public event System.Action Regrown;

    /// <summary>Seconds until the node grows back, or 0 when it is present.</summary>
    public float RespawnRemaining => IsDepleted ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

    public string SaveKey => "node";

    bool ISharedState.IsShared => true;

    // Gathered or not; the regrowth countdown is left out.
    int ISharedState.SharedMark => IsDepleted ? 1 : 0;

    // The regrowth time is saved as time left, since the game clock starts over after loading.
    public string CaptureState()
    {
        return JsonUtility.ToJson(new SavedNode
        {
            depleted = IsDepleted, respawnRemaining = RespawnRemaining, run = DayNightCycle.Run, savedAt = DayNightCycle.AwayStamp,
            regrowOnDay = IsDepleted ? regrowOnDay : 0,
        });
    }

    public void RestoreState(string state)
    {
        SavedNode saved = JsonUtility.FromJson<SavedNode>(state);
        if (saved.depleted)
        {
            // Home from a raid in the same run, the time away counts, as it does for the clock, the days and the saplings: the node
            // had exactly as long to go as when the player left. From disk (or a friend's game) there's no knowing how long it was.
            // On the day's own clock (game time): in real time, an hour in the pause menu on a raid grew every node back.
            float away = DayNightCycle.SecondsAwaySince(saved.run, saved.savedAt);
            respawnAt = Time.time + Mathf.Max(0f, saved.respawnRemaining - away);
            regrowOnDay = saved.regrowOnDay;
            SetPresent(false);
        }
        else if (IsDepleted)
        {
            // Grown back in a friend's Haven (co-op).
            SetPresent(true);
        }
    }

    // A raid tier holds nothing it can't hand over, whatever placed it there (an event's ore vein, say). In Start, as an object can't
    // be switched off while it is still being switched on.
    private void Start()
    {
        if (GameSession.ActiveRaid != null && !RaidAllows(node, GameSession.ActiveRaid.Difficulty))
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>False when a raid tier allows nothing this node gives: an iron vein in a tier I raid.</summary>
    public static bool RaidAllows(GatheringNodeDefinition definition, RaidDifficulty tier)
    {
        if (definition == null || tier == null)
        {
            return true;
        }

        bool anyItem = false;
        foreach (ItemDropEntry entry in definition.Yields.Entries)
        {
            if (entry.Item != null)
            {
                anyItem = true;
                if (tier.Allows(entry.Item))
                {
                    return true;
                }
            }
        }

        return !anyItem;
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (Application.isPlaying)
        {
            ApplyVariant();
        }

        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        if (!TryGetComponent(out feedback))
        {
            feedback = gameObject.AddComponent<ResourceNodeFeedback>();
        }
    }

    private static readonly List<ResourceNode> active = new List<ResourceNode>();

    /// <summary>Every node in the loaded scenes, gathered or not (a quarry ring's rocks: RingWork).</summary>
    public static IReadOnlyList<ResourceNode> Active => active;

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnGathered);
        WatchTimers();
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
        interactable.Completed.RemoveListener(OnGathered);
        if (countdown != null)
        {
            StopCoroutine(countdown);
            countdown = null;
        }
    }

    private bool HasTimer => hideAt >= 0f || (IsDepleted && node != null && (node.RegrowsAfter > 0f || regrowOnDay > 0));

    /// <summary>
    /// The home day a node gathered now grows back on: today plus its Regrows At Home After Days, at home only (not in a raid, the
    /// prologue or a friend's Haven, whose host keeps its own), else 0. Pure, for tests.
    /// </summary>
    public static int RegrowDay(int afterDays, int today, bool atHome) => afterDays > 0 && atHome ? today + afterDays : 0;

    private static bool AtOwnHome => RaidDirector.Current == null && PrologueDirector.Current == null && !HavenVisit.IsVisiting;

    // True once the home day it waits for has come.
    private bool DayHasCome => regrowOnDay > 0 && GameSession.Home.DaysPassed >= regrowOnDay;

    private void WatchTimers()
    {
        if (countdown == null && isActiveAndEnabled && HasTimer)
        {
            countdown = StartCoroutine(CountDown());
        }
    }

    // Runs only while the break plays or the node grows back: an untouched node costs nothing per frame (there are a
    // thousand round the Haven).
    private IEnumerator CountDown()
    {
        while (HasTimer)
        {
            if (hideAt >= 0f && Time.time >= hideAt)
            {
                hideAt = -1f;
                SetRenderers(false);
            }

            if (IsDepleted && node != null && ((node.RegrowsAfter > 0f && Time.time >= respawnAt) || DayHasCome))
            {
                regrowOnDay = 0;
                SetPresent(true);
                Regrown?.Invoke();
            }

            yield return null;
        }

        countdown = null;
    }

    /// <summary>The look this spot gets: the node's own model or one of its definition's variants (the same one every
    /// time for the same spot), at a random size and facing. A variant takes the place and size of the model it replaces
    /// (its "Model" child, or the part holding its mesh), whose colliders stay.</summary>
    private void ApplyVariant()
    {
        Transform model = transform.Find("Model");
        Renderer first = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => !(r is ParticleSystemRenderer));
        if (node == null || first == null || keepOwnModel)
        {
            return;
        }

        Transform look = model != null ? model : first.transform;
        Vector3 at = transform.position;
        var random = new System.Random(Mathf.RoundToInt(at.x * 10f) * 73856093 ^ Mathf.RoundToInt(at.z * 10f) * 19349663 ^ node.name.GetHashCode());
        IReadOnlyList<GameObject> variants = node.ModelVariants;
        int pick = random.Next(variants.Count + 1) - 1;
        if (pick >= 0 && variants[pick] != null)
        {
            Bounds original = BoundsOf(look);
            foreach (Renderer part in look.GetComponentsInChildren<Renderer>(true))
            {
                if (part.TryGetComponent(out MeshSurface surface))
                {
                    DestroyImmediate(surface);
                }

                if (part.TryGetComponent(out MeshFilter filter))
                {
                    DestroyImmediate(filter);
                }

                DestroyImmediate(part);
            }

            GameObject variant = Instantiate(variants[pick], look, false);
            variant.name = variants[pick].name;
            HeldItemView.StripToVisual(variant);
            Bounds fitted = BoundsOf(variant.transform);
            float factor = fitted.size.magnitude > 0.0001f ? original.size.magnitude / fitted.size.magnitude : 1f;
            variant.transform.localScale *= factor;
            fitted = BoundsOf(variant.transform);
            variant.transform.position += new Vector3(original.center.x - fitted.center.x, original.min.y - fitted.min.y, original.center.z - fitted.center.z);
        }

        Vector2 scale = node.VariantScale;
        look.localScale *= Mathf.Lerp(Mathf.Min(scale.x, scale.y), Mathf.Max(scale.x, scale.y), (float)random.NextDouble());
        look.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * look.localRotation;
    }

    private static Bounds BoundsOf(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>(true);
        if (parts.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }

        Bounds bounds = parts[0].bounds;
        foreach (Renderer part in parts)
        {
            bounds.Encapsulate(part.bounds);
        }

        return bounds;
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = null;
        if (node == null || node.GatherTool == ToolType.None)
        {
            return true;
        }

        if (ItemInventory.GatheringTier(user, node.GatherTool) >= node.RequiredToolTier)
        {
            return true;
        }

        reason = $"Needs a Tier {Tiers.ToRoman(node.RequiredToolTier)} {node.GatherTool} to gather {node.DisplayName}.";
        return false;
    }

    /// <summary>Announces a minigame swing on this node (called by PlayerGathering) for its feedback and listeners.</summary>
    public void NotifySwing(SwingResult result, Vector3 from)
    {
        Swung?.Invoke(result, from);
    }

    private void OnGathered() => Gathered(interactable.Interactor);

    /// <summary>Gathers the node for someone without their working it (the Sweep technique): its yield, then as if worked.
    /// False when it is depleted or can't be gathered now.</summary>
    public bool GatherBy(GameObject gatherer)
    {
        if (IsDepleted || gatherer == null || !interactable.enabled || !TryGetComponent(out ResourceSource source))
        {
            return false;
        }

        source.GrantTo(gatherer);
        Gathered(gatherer);
        return true;
    }

    /// <summary>True while broken down quietly (BreakDown): what listens to Harvested leaves out its sound and show.</summary>
    public bool BreaksQuietly { get; private set; }

    /// <summary>Broken down by no one (a troll's charge snapping a sapling): as if gathered, with nothing given. False when
    /// it is already down. <paramref name="quietly"/>: no sound or burst (a rock a mason broke while the player was away, laid down on
    /// coming home: RingWork.SettleAtHome).</summary>
    public bool BreakDown(bool quietly = false)
    {
        if (IsDepleted || node == null || !interactable.enabled)
        {
            return false;
        }

        BreaksQuietly = quietly;
        try
        {
            Harvested?.Invoke();
        }
        finally
        {
            BreaksQuietly = false;
        }

        if (!node.DepleteOnGather)
        {
            return true;
        }

        respawnAt = Time.time + node.RegrowsAfter;
        regrowOnDay = RegrowDay(node.RegrowsAtHomeAfterDays, GameSession.Home.DaysPassed, AtOwnHome);
        SetPresent(false, 0f);
        return true;
    }

    private void Gathered(GameObject by)
    {
        if (node != null && TryGetComponent(out ResourceSource source))
        {
            source.GrantExtra(by, node.BurstYields);
        }

        Harvested?.Invoke();
        if (node == null || !node.DepleteOnGather)
        {
            return;
        }

        respawnAt = Time.time + node.RegrowsAfter;
        regrowOnDay = RegrowDay(node.RegrowsAtHomeAfterDays, GameSession.Home.DaysPassed, AtOwnHome);
        SetPresent(false, feedback != null ? feedback.BreakSeconds : 0f);
    }

    // The node stays active while depleted so it can count down to growing back. Its model may stay visible a moment
    // longer than its colliders so the break can play.
    private void SetPresent(bool present, float hideDelay = 0f)
    {
        IsDepleted = !present;
        foreach (Collider part in colliders)
        {
            if (part != null)
            {
                part.enabled = present;
            }
        }

        interactable.enabled = present;
        if (!present && hideDelay > 0f)
        {
            hideAt = Time.time + hideDelay;
        }
        else
        {
            hideAt = -1f;
            SetRenderers(present);
        }

        WatchTimers();
    }

    private void SetRenderers(bool visible)
    {
        foreach (Renderer part in renderers)
        {
            part.enabled = visible;
        }
    }
}
