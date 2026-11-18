using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The stash chest in Safe Haven. Using it (its Interactable) opens the stash window, where the player leaves what they
/// don't want to risk in a raid. The Interactable beside it must not deactivate on completion.
/// <para>
/// Each chest holds its own items (the user, 2026-11-15; until then every chest opened one shared stash): its Block of
/// GameSession.Stash.Items, chosen when it first stands and kept in the save. Its size is its StashSlots (a Small Chest
/// 12, a Storage Shed all 60 of a block). Items in a chest that is gone are kept in its block, out of reach until a new
/// chest takes that block (the lowest free one is taken first, so they come back in the next chest built).
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.AsksTheHost, "the host's stores: with the host's yes (HavenVisit.MayUseStash)")]
public sealed class StashChest : MonoBehaviour, ISaveable
{
    [Tooltip("Left empty, the first StashWindow in the scene is used.")]
    [SerializeField] private StashWindow window;
    [Tooltip("The window closes when the player walks further than this from the chest, in meters.")]
    [SerializeField, Min(1f)] private float windowRange = 4f;
    [Tooltip("Stash slots this chest gives room for. 0: all of them.")]
    [SerializeField, Min(0)] private int stashSlots;

    private static readonly List<StashChest> standing = new List<StashChest>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        standing.Clear();
        waiting.Clear();
        openSlots.Clear();
        allocationDue = false;
    }
    private Interactable interactable;
    private int block = -1;
    private bool started;

    /// <summary>Stash slots this chest gives room for (all of them for 0).</summary>
    public int StashSlots => Mathf.Min(StashStorage.ChestSlots,
        (stashSlots > 0 ? stashSlots : StashStorage.ChestSlots) + Mathf.RoundToInt(Upgraded));

    /// <summary>Which block of GameSession.Stash.Items holds this chest's items; -1 until it first stands.</summary>
    public int Block => block;

    /// <summary>The first slot of this chest's items in GameSession.Stash.Items.</summary>
    public int FirstSlot => StashStorage.BlockStart(Mathf.Max(0, block));

    /// <summary>True when it is built (not a scaffold or a ruin) and so holds things.</summary>
    public bool IsStanding => !TryGetComponent(out Construction construction) || construction.IsStanding;

    // Upgrades (Construction) add stash slots.
    private float Upgraded => TryGetComponent(out Construction construction) ? construction.Sum(level => level.extraStorage) : 0f;

    /// <summary>The stash slots the chests standing now give room for, all of them together; -1 with no chest.</summary>
    public static int SlotsStanding()
    {
        int total = -1;
        foreach (StashChest chest in standing)
        {
            // A chest still being built (or a ruin) holds nothing yet.
            if (chest.IsStanding)
            {
                total = Mathf.Max(0, total) + chest.StashSlots;
            }
        }

        return total;
    }

    /// <summary>True when a finished chest stands within <paramref name="reach"/> metres of a point.</summary>
    public static bool AnyWithin(Vector3 point, float reach)
    {
        foreach (StashChest chest in standing)
        {
            bool finished = !chest.TryGetComponent(out Construction construction) || construction.IsStanding;
            if (finished && chest.interactable.DistanceFrom(point) <= reach)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        standing.Add(this);
        Construction.Finished += OnUpgraded;
        RefreshStash();
    }

    // After SaveableEntity (order -80) has put back the block this chest had: only a chest without one takes a new one,
    // at the end of the frame, once every chest loaded with it has started too (so the biggest is first).
    private static bool allocationDue;

    private void Start()
    {
        started = true;
        allocationDue = true;
    }

    private void LateUpdate()
    {
        if (allocationDue)
        {
            allocationDue = false;
            RefreshStash();
        }
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        standing.Remove(this);
        Construction.Finished -= OnUpgraded;
        // The Haven unloading switches every chest off one by one; refreshing on each would shrink the open slots to the last
        // chest's block for the whole raid (workers carry their output into the stash meanwhile).
        if (!SceneLoader.IsLeaving)
        {
            RefreshStash();
        }
    }

    private void OnUpgraded(Construction upgraded)
    {
        if (upgraded.gameObject == gameObject)
        {
            RefreshStash();
        }
    }

    // Chests that stand and have no block yet take one (the biggest first, so a save from before blocks puts its whole
    // stash in the biggest chest), then exactly the standing chests' slots are opened. Away from home (no chest standing)
    // the stash keeps its slots: it can't be opened there anyway.
    private static readonly List<StashChest> waiting = new List<StashChest>();
    private static readonly List<bool> openSlots = new List<bool>();

    private static void RefreshStash()
    {
        waiting.Clear();
        foreach (StashChest chest in standing)
        {
            if (chest.started && chest.block < 0 && chest.IsStanding)
            {
                waiting.Add(chest);
            }
        }

        waiting.Sort((a, b) => b.StashSlots != a.StashSlots ? b.StashSlots.CompareTo(a.StashSlots) : string.CompareOrdinal(a.name, b.name));
        foreach (StashChest chest in waiting)
        {
            chest.block = FreeBlock();
        }

        if (SlotsStanding() < 0)
        {
            return;
        }

        openSlots.Clear();
        for (int i = 0; i < GameSession.Stash.Items.SlotCount; i++)
        {
            openSlots.Add(false);
        }

        foreach (StashChest chest in standing)
        {
            if (chest.block >= 0 && chest.IsStanding)
            {
                int start = chest.FirstSlot;
                for (int i = start; i < start + chest.StashSlots && i < openSlots.Count; i++)
                {
                    openSlots[i] = true;
                }
            }
        }

        GameSession.Stash.Items.SetUnlockedMask(openSlots);
    }

    // The lowest block no chest here has. A gone chest's block counts as free, so its items come back in the next chest.
    private static int FreeBlock()
    {
        for (int candidate = 0; candidate < StashStorage.MaxChests; candidate++)
        {
            if (!standing.Exists(chest => chest.block == candidate))
            {
                return candidate;
            }
        }

        Debug.LogWarning($"Every one of the {StashStorage.MaxChests} chest blocks is taken: a new chest shares the last one.");
        return StashStorage.MaxChests - 1;
    }

    // ----- Saving: which block is this chest's -----

    [System.Serializable]
    private struct SavedState
    {
        public int block;
    }

    public string SaveKey => "stash-chest";

    public string CaptureState() => JsonUtility.ToJson(new SavedState { block = block });

    public void RestoreState(string state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return;
        }

        int saved = JsonUtility.FromJson<SavedState>(state).block;
        block = saved >= 0 && saved < StashStorage.MaxChests ? saved : -1;
        RefreshStash();
    }

    /// <summary>True while a position is close enough to keep the stash window open.</summary>
    public bool IsInReach(Vector3 position)
    {
        return interactable.DistanceFrom(position) <= windowRange;
    }

    private void OnUsed()
    {
        // A friend's stash takes their yes; then their stash is sent over, and what is put in or taken goes back (StashShare).
        if (!Place.StashUsable)
        {
            if (CoopSession.Current != null)
            {
                CoopSession.Current.Access.Ask(VisitorRight.Stash);
            }

            return;
        }

        if (window == null && UIStateManager.Current != null)
        {
            window = UIStateManager.Current.Find<StashWindow>(UIWindowId.Stash);
        }

        if (window == null)
        {
            Debug.LogError("StashChest found no StashWindow in the scene.", this);
            return;
        }

        window.Open(this, interactable.Interactor);
    }
}
