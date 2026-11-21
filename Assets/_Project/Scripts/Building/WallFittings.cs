using UnityEngine;

/// <summary>
/// What a wall piece holds (the user, 2026-11-21: "a wall section can be upgraded to hold a torch"): a torch on its face, from its
/// BuildingDefinition › Wall Torch, fitted and taken down from the piece's Upgrade page (WallUpgrades). The torch is a child of the piece,
/// so it moves, breaks and goes with it; it burns charcoal at night like any torch (TorchFuel on the torch). Saved with the piece.
/// </summary>
[DisallowMultipleComponent]
public sealed class WallFittings : MonoBehaviour, ISaveable
{
    private const string TorchName = "Fitted Wall Torch";

    private bool torch;
    private GameObject torchObject;
    private BuildingObject building;

    private BuildingDefinition Definition => (building != null ? building : building = GetComponent<BuildingObject>()) != null ? building.Definition : null;

    /// <summary>True when this kind of wall can hold a torch.</summary>
    public bool TakesTorch => Definition != null && Definition.WallTorch != null;

    public bool HasTorch => torch;

    /// <summary>Fits the torch (paid for by the caller). False when it can't hold one or already does.</summary>
    public bool AddTorch()
    {
        if (torch || !TakesTorch)
        {
            return false;
        }

        torch = true;
        ShowTorch();
        return true;
    }

    /// <summary>Takes the torch down (the caller gives back its cost). False when there was none.</summary>
    public bool RemoveTorch()
    {
        if (!torch)
        {
            return false;
        }

        torch = false;
        if (torchObject != null)
        {
            Destroy(torchObject);
        }

        torchObject = null;
        return true;
    }

    // On the face away from the portal (it lights the approach), near the top; low walls carry it on top. Measured on the kind's own
    // prefab: a standing piece's bounds also hold its health bar and scaffold dressing.
    private void ShowTorch()
    {
        if (torchObject != null || Definition == null || Definition.WallTorch == null || Definition.Prefab == null)
        {
            return;
        }

        Bounds local = BuildPlacement.LocalBounds(Definition.Prefab.transform);
        Vector3 toPortal = HomePortal.Current != null ? HomePortal.Current.transform.position - transform.position : -transform.forward;
        bool outwardIsFront = Vector3.Dot(toPortal, transform.forward) <= 0f;
        torchObject = Instantiate(Definition.WallTorch, transform);
        torchObject.name = TorchName;
        bool low = local.size.y < 1.2f;
        float face = outwardIsFront ? local.max.z + 0.02f : local.min.z - 0.02f;
        torchObject.transform.localPosition = low
            ? new Vector3(local.center.x, local.max.y, local.center.z)
            : new Vector3(local.center.x, local.max.y - 0.45f, face);
        torchObject.transform.localRotation = outwardIsFront ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
    }

    public string SaveKey => "wall-fittings";

    [System.Serializable]
    private struct SavedState
    {
        public bool torch;
    }

    public string CaptureState() => JsonUtility.ToJson(new SavedState { torch = torch });

    public void RestoreState(string state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return;
        }

        torch = JsonUtility.FromJson<SavedState>(state).torch;
        if (torch)
        {
            ShowTorch();
        }
    }
}
