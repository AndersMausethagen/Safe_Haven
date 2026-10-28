using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The stash chest in Safe Haven. Using it (its Interactable) opens the stash window, where the player leaves what they
/// don't want to risk in a raid. The Interactable beside it must not deactivate on completion.
/// <para>
/// Every chest opens the same stash (GameSession.Stash), but the stash only has as many slots as the biggest chest
/// standing at home: a Small Chest early on, all of them with a Storage Shed. Items in slots that close again (a bigger
/// chest gone) are kept, just out of reach until there is room again.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class StashChest : MonoBehaviour
{
    [Tooltip("Left empty, the first StashWindow in the scene is used.")]
    [SerializeField] private StashWindow window;
    [Tooltip("The window closes when the player walks further than this from the chest, in meters.")]
    [SerializeField, Min(1f)] private float windowRange = 4f;
    [Tooltip("Stash slots this chest gives room for. 0: all of them.")]
    [SerializeField, Min(0)] private int stashSlots;

    private static readonly List<StashChest> standing = new List<StashChest>();
    private Interactable interactable;

    /// <summary>Stash slots this chest gives room for (all of them for 0).</summary>
    public int StashSlots => Mathf.Min(StashStorage.Columns * StashStorage.Rows,
        (stashSlots > 0 ? stashSlots : StashStorage.Columns * StashStorage.Rows) + Mathf.RoundToInt(Upgraded));

    // Upgrades (Construction) add stash slots.
    private float Upgraded => TryGetComponent(out Construction construction) ? construction.Sum(level => level.extraStorage) : 0f;

    /// <summary>The stash slots the chests standing now give room for: the biggest one's; -1 with no chest.</summary>
    public static int SlotsStanding()
    {
        int most = -1;
        foreach (StashChest chest in standing)
        {
            // A chest still being built (or a ruin) holds nothing yet.
            if (!chest.TryGetComponent(out Construction construction) || construction.IsStanding)
            {
                most = Mathf.Max(most, chest.StashSlots);
            }
        }

        return most;
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

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        standing.Remove(this);
        Construction.Finished -= OnUpgraded;
        RefreshStash();
    }

    private void OnUpgraded(Construction upgraded)
    {
        if (upgraded.gameObject == gameObject)
        {
            RefreshStash();
        }
    }

    // Away from home (no chest standing) the stash keeps its size: it can't be opened there anyway.
    private static void RefreshStash()
    {
        int slots = SlotsStanding();
        if (slots >= 0)
        {
            GameSession.Stash.Items.SetUnlockedSlots(slots);
        }
    }

    /// <summary>True while a position is close enough to keep the stash window open.</summary>
    public bool IsInReach(Vector3 position)
    {
        return interactable.DistanceFrom(position) <= windowRange;
    }

    private void OnUsed()
    {
        if (window == null && UIStateManager.Instance != null)
        {
            window = UIStateManager.Instance.Find<StashWindow>(UIWindowId.Stash);
        }

        if (window == null)
        {
            Debug.LogError("StashChest found no StashWindow in the scene.", this);
            return;
        }

        window.Open(this, interactable.Interactor);
    }
}
