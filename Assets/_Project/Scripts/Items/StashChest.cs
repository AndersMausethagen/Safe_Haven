using UnityEngine;

/// <summary>
/// The stash chest in Safe Haven. Using it (its Interactable) opens the stash window, where the player leaves what they
/// don't want to risk in a raid. The Interactable beside it must not deactivate on completion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class StashChest : MonoBehaviour
{
    [Tooltip("Left empty, the first StashWindow in the scene is used.")]
    [SerializeField] private StashWindow window;
    [Tooltip("The window closes when the player walks further than this from the chest, in meters.")]
    [SerializeField, Min(1f)] private float windowRange = 4f;

    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
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
