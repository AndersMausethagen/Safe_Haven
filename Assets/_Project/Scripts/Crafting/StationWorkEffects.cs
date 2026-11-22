using UnityEngine;

/// <summary>
/// What a station shows and sounds only while it works its queue (the user, 2026-11-22, on the Cauldron: "add a fire on the firepit
/// model as well that burns while the cauldron is creating items", "also have it create smoke and sound while brewing"): a child
/// shown while a craft is under way and not stuck, hidden otherwise, holding the fire, its flickering light, the steam and a
/// SoundEmitter loop. On the station's model prefab; finds its station above it. Not while the building is a scaffold or a ruin.
/// </summary>
[DisallowMultipleComponent]
public sealed class StationWorkEffects : MonoBehaviour
{
    [Tooltip("Shown while the station's queue is at work; its effects and sound loops (SoundEmitter › Loop) run with it.")]
    [SerializeField] private GameObject whileWorking;

    // Looked at a few times a second, each station out of step with the others.
    private const float LookEvery = 0.3f;

    private CraftingStation station;
    private Construction construction;
    private float nextLook;

    /// <summary>True while the effects are shown.</summary>
    public bool IsShowing => whileWorking != null && whileWorking.activeSelf;

    public void Configure(GameObject shown) => whileWorking = shown;

    private void Awake()
    {
        station = GetComponentInParent<CraftingStation>();
        construction = GetComponentInParent<Construction>();
        if (whileWorking != null)
        {
            whileWorking.SetActive(false);
        }

        nextLook = Time.time + Random.value * LookEvery;
    }

    private void Update()
    {
        if (whileWorking == null || Time.time < nextLook)
        {
            return;
        }

        nextLook = Time.time + LookEvery;
        bool working = Working(station, construction);
        if (whileWorking.activeSelf != working)
        {
            whileWorking.SetActive(working);
        }
    }

    /// <summary>A station at work: standing, a craft in its queue, and not stuck (output full, or its materials gone).</summary>
    public static bool Working(CraftingStation station, Construction construction)
    {
        if (station == null || (construction != null && (!construction.IsStanding || construction.IsBuilding)))
        {
            return false;
        }

        StationQueue queue = station.Queue;
        return queue != null && !queue.IsIdle && !queue.Blocked;
    }
}
