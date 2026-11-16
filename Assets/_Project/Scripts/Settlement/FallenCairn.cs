using UnityEngine;

/// <summary>
/// A cairn near the portal for someone who went along on a raid and hasn't come home (GDD §15.6). It says who it is for and where they
/// were lost, and it stands until they are found. Raised by ResidentSpawner from SettlementState.Fallen, so it needs no saving of its own. Press E to remember them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.Anyone, "a memorial: reading it changes nothing")]
public sealed class FallenCairn : MonoBehaviour, IInteractionName, IInteractionVerb
{
    private const float FirstRing = 9f;
    private const float RingStep = 1.4f;
    private const float GoldenAngle = 137.5f;
    private const int Tries = 8;
    private const float TryAngle = 45f;
    private const float ClearRadius = 1.6f;

    private Interactable interactable;
    private FallenRecord record;

    public string InteractionName => record != null ? $"{record.Resident.DisplayName}'s cairn" : "A cairn";
    public string Verb => "Remember";

    /// <summary>Where the nth cairn stands: round the portal and apart, on the ground, clear of what has been built there.</summary>
    public static Vector3 SpotFor(Vector3 centre, int index) => SpotFor(centre, index, FirstRing, 40f);

    /// <summary>The same for something else raised round the portal, on a ring of its own.</summary>
    public static Vector3 SpotFor(Vector3 centre, int index, float ring, float startAngle)
    {
        Vector3 spot = centre;
        for (int turn = 0; turn < Tries; turn++)
        {
            spot = centre + Quaternion.Euler(0f, startAngle + index * GoldenAngle + turn * TryAngle, 0f) * Vector3.forward * (ring + index * RingStep);
            Terrain terrain = Terrains.Under(spot);
            spot.y = terrain != null ? terrain.SampleHeight(spot) + terrain.transform.position.y : centre.y;
            if (IsClear(spot))
            {
                break;
            }
        }

        return spot;
    }

    private static bool IsClear(Vector3 spot)
    {
        foreach (Collider collider in Physics.OverlapSphere(spot + Vector3.up, ClearRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!(collider is TerrainCollider))
            {
                return false;
            }
        }

        return true;
    }

    public void Bind(FallenRecord fallen)
    {
        record = fallen;
        name = InteractionName;
    }

    private void Awake() => interactable = GetComponent<Interactable>();

    private void OnEnable() => interactable.Completed.AddListener(OnRemembered);

    private void OnDisable() => interactable.Completed.RemoveListener(OnRemembered);

    private void OnRemembered()
    {
        if (record != null)
        {
            string along = record.RaidsAlong > 0 ? $" {record.RaidsAlong} raid{(record.RaidsAlong == 1 ? "" : "s")} at your side." : "";
            GameMessages.Post($"{record.Resident.DisplayName}. Lost in {record.Where}, and still out there.{along}");
        }
    }
}
